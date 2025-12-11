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

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

modelBuilder.Entity<StenaReceipt>(entity =>
{
    entity.ToTable("Modtagelse");

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



}

}
