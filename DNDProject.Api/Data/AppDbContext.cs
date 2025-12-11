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
        public DbSet<StenaReceipt> StenaReceipts => Set<StenaReceipt>();
        public DbSet<ContainerCapacity> ContainerCapacities => Set<ContainerCapacity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ------------------------
            // Modtagelse -> StenaReceipt
            // ------------------------
            modelBuilder.Entity<StenaReceipt>(entity =>
            {
                entity.ToTable("Modtagelse");

                entity.HasKey(x => x.Id);

                entity.Property(x => x.Id)
                      .HasColumnName("Id");

                entity.Property(x => x.CustomerKey)
                      .HasColumnName("LevNr");

                entity.Property(x => x.CustomerName)
                      .HasColumnName("Navn");

                entity.Property(x => x.ReceiptDate)
                      .HasColumnName("KoeresDato");

                entity.Property(x => x.ItemNumber)
                      .HasColumnName("Varenummer");

                entity.Property(x => x.ItemText)
                      .HasColumnName("Varebeskrivelse");

                entity.Property(x => x.Unit)
                      .HasColumnName("Enhed");

                entity.Property(x => x.Amount)
                      .HasColumnName("Antal");
            });

            // ---------------------------------------------
            // Kapacitet_og_enhed_opdateret -> ContainerCapacity
            // ---------------------------------------------
            modelBuilder.Entity<ContainerCapacity>(entity =>
            {
                entity.ToTable("Kapacitet_og_enhed_opdateret");

                entity.HasKey(x => x.ItemNumber);

                entity.Property(x => x.ItemNumber)
                      .HasColumnName("Varenummer");

                entity.Property(x => x.Capacity)
                      .HasColumnName("Kapacitet");

                entity.Property(x => x.Unit)
                      .HasColumnName("Enhed");
            });
        }
    }
}
