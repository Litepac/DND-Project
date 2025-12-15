using DNDProject.Api.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DNDProject.Api.Data
{
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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ------------------------
            // Modtagelse -> StenaReceipt
            // ------------------------
            modelBuilder.Entity<StenaReceipt>(entity =>
            {
                entity.ToTable("Modtagelse", "dbo");
                entity.HasKey(x => x.Id);

                entity.Property(x => x.Id).HasColumnName("Id");
                entity.Property(x => x.CustomerKey).HasColumnName("LevNr");
                entity.Property(x => x.CustomerName).HasColumnName("Navn");
                entity.Property(x => x.ReceiptDate).HasColumnName("KoeresDato");
                entity.Property(x => x.ItemNumber).HasColumnName("Varenummer");
                entity.Property(x => x.ItemText).HasColumnName("Varebeskrivelse");
                entity.Property(x => x.Unit).HasColumnName("Enhed");
                entity.Property(x => x.Amount).HasColumnName("Antal");

                entity.Property(x => x.PurchaseOrderNumber).HasColumnName("KoebsordreNummer");
            });

            // ------------------------
            // Kørselsordrer -> StenaKoerselsordre
            // VIGTIGT: tabelnavnet SKAL matche det du kan SELECT'e i SSMS
            // ------------------------
            modelBuilder.Entity<StenaKoerselsordre>(entity =>
            {
                entity.ToTable("Kørselsordrer", "dbo"); // <- hvis din tabel i SSMS hedder dbo.Kørselsordrer

                entity.HasKey(x => x.Nr);

                entity.Property(x => x.Nr).HasColumnName("Nr");
                entity.Property(x => x.Lev_nr).HasColumnName("Lev_nr");
                entity.Property(x => x.Navn).HasColumnName("Navn");
                entity.Property(x => x.Varenr).HasColumnName("Varenr");
                entity.Property(x => x.Beskrivelse).HasColumnName("Beskrivelse");
                entity.Property(x => x.Indhold).HasColumnName("Indhold");
                entity.Property(x => x.Indholdsbeskrivelse).HasColumnName("Indholdsbeskrivelse");
                entity.Property(x => x.Frekvens).HasColumnName("Frekvens");
                entity.Property(x => x.Uge_dag).HasColumnName("Uge_dag");
                entity.Property(x => x.Start_dato).HasColumnName("Start_dato");

                // ✅ DETTE ER GRUNDEN TIL DIN FEJL: ø !
                entity.Property(x => x.PurchaseOrderNumber).HasColumnName("Købsordrenr");
            });

            // ---------------------------------------------
            // Kapacitet_og_enhed_opdateret -> ContainerCapacity
            // ---------------------------------------------
            modelBuilder.Entity<ContainerCapacity>(entity =>
            {
                entity.ToTable("Kapacitet_og_enhed_opdateret", "dbo");
                entity.HasKey(x => x.ItemNumber);

                entity.Property(x => x.ItemNumber).HasColumnName("Varenummer");
                entity.Property(x => x.Capacity).HasColumnName("Kapacitet");
                entity.Property(x => x.Unit).HasColumnName("Enhed");
            });
        }
    }
}
