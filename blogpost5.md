# Blogpost 5 – Data Access (ORM, Entity Framework & LINQ)

> **Note om database og værktøjer**  
> Systemet anvender **Microsoft SQL Server** som database, som tilgås og administreres via  
> **SQL Server Management Studio (SSMS)**.  
> Entity Framework Core bruges som ORM oven på SQL Server.  
> Projektet anvender **ikke SQLite** – alle eksempler og mappings er baseret på en eksisterende
> SQL Server-database med faste tabeller og schemas (dbo).
---

## Hvordan en ORM ændrer måden vi arbejder med data på

I vores projekt har vi introduceret **Entity Framework Core** som ORM (Object–Relational Mapper). I praksis betyder det, at vi ikke længere arbejder med databasen via håndskrevet SQL i hver feature, men i stedet arbejder gennem:

- **C#-modeller (entities)** som repræsenterer tabeller
- **DbContext** som repræsenterer vores forbindelse + “entry point” til tabellerne
- **LINQ** queries som EF oversætter til SQL automatisk

Det giver nogle klare ændringer i måden vi arbejder på:

### Mindre “SQL-lim” i applikationslogikken

I stedet for at bygge SQL-strings og selv holde styr på mapping fra rows → objekter, kan vi udtrykke data-behov som C# queries. Det gør koden mere læsbar og lettere at vedligeholde.

### Strong typing og compile-time sikkerhed

Når vi skriver queries i LINQ, er det typed C#. Hvis vi refererer til et felt, der ikke findes, får vi compile-fejl. Det er en stor fordel i forhold til raw SQL, hvor fejl ofte først opdages ved runtime.

### Central mapping via OnModelCreating

En vigtig del i vores løsning er, at vi har en “Stena”-database hvor tabeller/kolonner ikke matcher vores C#-navne. ORM’en hjælper her fordi vi kan definere mapping ét sted og derefter arbejde “rent” i C# resten af projektet.

Vi har konkret brugt EF Core til at mappe Stena-tabeller som fx:

- `dbo.Modtagelse` → `StenaReceipt`
- `dbo.Kørselsordrer` → `StenaKoerselsordre`
- `dbo.Kapacitet_og_enhed_opdateret` → `ContainerCapacity`

Det betyder, at vores controllers og services kan bruge `StenaReceipts` / `StenaKoerselsordrer` osv. direkte, uden at kende de præcise tabelnavne eller kolonnenavne i SQL.

---

## Eksempler på refaktorering til Entity Framework

### DbContext og DbSet – “tabeller som properties”

Vores `AppDbContext` indeholder både domæne-tabeller (vores egne) og Stena-tabeller (legacy/operational data). Her er et udsnit af konfigurationen:

```csharp
public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Container> Containers => Set<Container>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<PickupEvent> PickupEvents => Set<PickupEvent>();

    // Stena-data
    public DbSet<StenaReceipt> StenaReceipts { get; set; } = null!;
    public DbSet<StenaKoerselsordre> StenaKoerselsordrer { get; set; } = null!;
    public DbSet<ContainerCapacity> ContainerCapacities { get; set; } = null!;
}
```

Pointen er: når en ny feature skal bruge data, afhænger den ikke af SQL, men af en “typed gateway”

## Mapping af legacy tabeller i OnModelCreating

I OnModelCreating() mapper vi Stena-tabellernes struktur til vores entities. Det er helt centralt for projektet, fordi vores Stena-data har danske tabel/kolonnenavne.

Eksempel: mapping af dbo.Modtagelse til entity StenaReceipt:

```csharp
modelBuilder.Entity<StenaReceipt>(entity =>
{
    entity.ToTable("Modtagelse", "dbo");
    entity.HasKey(x => x.Id);

    entity.Property(x => x.CustomerKey).HasColumnName("LevNr");
    entity.Property(x => x.CustomerName).HasColumnName("Navn");
    entity.Property(x => x.ReceiptDate).HasColumnName("KoeresDato");
    entity.Property(x => x.Unit).HasColumnName("Enhed");
    entity.Property(x => x.Amount).HasColumnName("Antal");
    entity.Property(x => x.PurchaseOrderNumber).HasColumnName("KoebsordreNummer");
});
```

I praksis betyder det, at resten af systemet kan arbejde på en “domæne-venlig” måde:

- ReceiptDate i C# (selvom kolonnen hedder KoeresDato)
- CustomerKey (selvom kolonnen hedder LevNr)
- PurchaseOrderNumber (selvom kolonnen hedder KoebsordreNummer)
- Det reducerer fejl og gør query-koden meget mere læselig.

## Design-time DbContextFactory (migrations)

Vi har også et eksempel på EF refaktorering i form af en design-time factory til Identity/Auth databasen. Den bruges typisk til migrations/CLI tooling.

```csharp
public sealed class AuthDbContextFactory : IDesignTimeDbContextFactory<AuthDbContext>
{
    public AuthDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var cs = config.GetConnectionString("AuthConnection")
                 ?? throw new InvalidOperationException("Missing ConnectionStrings:AuthConnection");

        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseSqlServer(cs)
            .Options;

        return new AuthDbContext(options);
    }
}
```

Det er et godt eksempel på “EF-first” workflow: i stedet for at have ad-hoc scripts til databasen, kan vi lave migrations/ændringer med EF tooling på en reproducerbar måde.

## LINQ vs traditionel SQL (med konkrete eksempler)

Traditionel tilgang (SQL-tænkning)

I en klassisk SQL-baseret tilgang ville man typisk skrive noget i stil med:

```csharp
SELECT LevNr, Navn, COUNT(*) AS ReceiptCount
FROM dbo.Modtagelse
WHERE Enhed = 'KG'
GROUP BY LevNr, Navn
ORDER BY ReceiptCount DESC;
```

Det fungerer fint, men det kræver:

- at du kender præcis tabel/kolonnenavne (Modtagelse, LevNr, Enhed)
- at du selv mapper resultater til C#-objekter
- at queryen ligger som en “string” i koden (sværere at refaktorere sikkert)

## LINQ tilgang (vores måde med EF Core)

I vores API arbejder vi i stedet med typed LINQ. Et konkret eksempel fra vores StenaCustomerDashboardController er, at vi først henter relevante felter og derefter laver gruppering/aggregation:

```csharp
var raw = await _db.StenaReceipts
    .Select(r => new
    {
        r.CustomerKey,
        r.CustomerName,
        r.ReceiptDate,
        r.Unit,
        r.Amount
    })
    .ToListAsync();

var data = raw
    .Where(r => r.CustomerKey != null)
    .GroupBy(r => new { r.CustomerKey, r.CustomerName })
    .Select(g =>
    {
        var weightKg = g
            .Where(x => x.Unit == "KG")
            .Sum(x => ParseAmountToDecimal(x.Amount));

        var receiptCount = g.Count();
        var avgPerReceipt = receiptCount == 0 ? 0m : weightKg / receiptCount;

        return new CustomerSummaryDto
        {
            CustomerKey = g.Key.CustomerKey?.ToString() ?? string.Empty,
            CustomerName = g.Key.CustomerName,
            TotalWeightKg = (float)weightKg,
            ReceiptCount = receiptCount,
            FirstDate = g.Min(x => x.ReceiptDate),
            LastDate = g.Max(x => x.ReceiptDate),
            AverageWeightPerReceiptKg = (float)avgPerReceipt,
            IsLowAverageWeight = avgPerReceipt < 100m
        };
    })
    .OrderByDescending(x => x.TotalWeightKg)
    .ToList();
```

Forskellen er, at vi udtrykker hele data-behovet i C# og EF håndterer forbindelsen og object-mapping. Samtidig kan vi lave logik (fx parse af tal med danske decimaler) på en kontrolleret måde.

## LINQ som “query builder” + EF som SQL-generator

Et andet konkret eksempel fra vores MLController viser LINQ’s styrke til at udtrykke filtre og projektion, uden at skrive SQL:

```csharp
var raw = await _db.StenaReceipts.AsNoTracking()
    .Where(r =>
        r.Unit == "KG"
        && r.CustomerKey != null
        && r.CustomerKey == custKey
        && r.ReceiptDate >= f
        && r.ReceiptDate <= t
        && r.PurchaseOrderNumber != null
        && residualOrders.Contains(r.PurchaseOrderNumber.Value)
    )
    .Select(r => new
    {
        Dt = r.ReceiptDate,
        AmountStr = r.Amount,
        CustomerName = r.CustomerName,
        PurchaseOrder = r.PurchaseOrderNumber!.Value
    })
    .ToListAsync();
```

Her ville SQL-approachen kræve JOINs/IN clauses og manuel mapping. LINQ-versionen er mere læsbar og sikker, og vi kan nemt udvide den.
