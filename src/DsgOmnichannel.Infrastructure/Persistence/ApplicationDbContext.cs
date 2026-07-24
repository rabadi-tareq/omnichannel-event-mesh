using DsgOmnichannel.Domain.Entities;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace DsgOmnichannel.Infrastructure.Persistence;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<StoreInventory> StoreInventories => Set<StoreInventory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.AddTransactionalOutboxEntities();

        modelBuilder.Entity<AuditLog>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.EventType).HasMaxLength(100).IsRequired();
            builder.Property(x => x.Details).HasMaxLength(1000);
        });

        modelBuilder.Entity<Order>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.StoreId).HasMaxLength(50).IsRequired();
            builder.Property(x => x.CustomerName).HasMaxLength(100).IsRequired();
            builder.Property(x => x.ProductId).HasMaxLength(50);
            builder.Property(x => x.TotalAmount).HasPrecision(18, 2);
        });

        modelBuilder.Entity<StoreInventory>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.StoreId).HasMaxLength(50).IsRequired();
            builder.Property(x => x.ProductId).HasMaxLength(50).IsRequired();
        });
    }
}
