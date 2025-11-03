using DNDProject.Api.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DNDProject.Api.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser> // <— ændret her
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Container>   Containers   => Set<Container>();
    public DbSet<Customer>    Customers    => Set<Customer>();
    public DbSet<PickupEvent> PickupEvents => Set<PickupEvent>();
    public DbSet<StenaReceipt> StenaReceipts => Set<StenaReceipt>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b); // <— vigtigt for Identity

        // Container
        b.Entity<Container>()
            .Property(x => x.Type).HasMaxLength(100);

        // Container -> Customer (valgfrit, kan være null)
        b.Entity<Container>()
            .HasOne<Customer>()
            .WithMany(c => c.Containers)
            .HasForeignKey(c => c.CustomerId)
            .OnDelete(DeleteBehavior.SetNull);

        // PickupEvent (historik)
        b.Entity<PickupEvent>()
            .HasIndex(x => new { x.ContainerId, x.Timestamp });

        b.Entity<PickupEvent>()
            .HasOne(e => e.Container)
            .WithMany() // vi holder events “udenfor” Container-klassen for nu
            .HasForeignKey(e => e.ContainerId)
            .OnDelete(DeleteBehavior.Cascade);

        // Seed: containere (som du havde)
        b.Entity<Container>().HasData(
            new Container
            {
                Id = 1,
                Type = "Plast",
                Material = ContainerMaterial.Plast,
                SizeLiters = 2500,
                WeeklyAmountKg = 120,
                LastFillPct = 82,
                PreferredPickupFrequencyDays = 14
            },
            new Container
            {
                Id = 2,
                Type = "Jern",
                Material = ContainerMaterial.Jern,
                SizeLiters = 7000,
                WeeklyAmountKg = 900,
                LastFillPct = 46,
                PreferredPickupFrequencyDays = 21
            }
        );

        // Lille seed af historik (så du har noget at se)
        var now = DateTime.UtcNow.Date;
        b.Entity<PickupEvent>().HasData(
            new PickupEvent { Id = 1, ContainerId = 1, Timestamp = now.AddDays(-21), FillPct = 88, WeightKg = 110 },
            new PickupEvent { Id = 2, ContainerId = 1, Timestamp = now.AddDays(-7),  FillPct = 92, WeightKg = 125 },
            new PickupEvent { Id = 3, ContainerId = 2, Timestamp = now.AddDays(-14), FillPct = 51, WeightKg = 480 }
        );
    }
}
