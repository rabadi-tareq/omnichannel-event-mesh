using DsgOmnichannel.Domain.Entities;
using DsgOmnichannel.Infrastructure.Persistence.Sagas;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace DsgOmnichannel.Infrastructure.Persistence;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<StoreInventory> StoreInventories => Set<StoreInventory>();
    public DbSet<OrderState> OrderStates => Set<OrderState>();
    public DbSet<OrderStatusHistory> OrderStatusHistories => Set<OrderStatusHistory>();

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

        modelBuilder.Entity<OrderState>(builder =>
        {
            builder.ToTable("OrderState", "dbo");
            builder.HasKey(x => x.CorrelationId);
            builder.Property(x => x.CorrelationId).ValueGeneratedNever();
            builder.Property(x => x.CurrentState).HasMaxLength(64).IsRequired();
            builder.Property(x => x.StoreId).HasMaxLength(50);
        });

        modelBuilder.Entity<OrderStatusHistory>(builder =>
        {
            builder.ToTable("OrderStatusHistory");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Status).HasMaxLength(64).IsRequired();
            builder.Property(x => x.Reason).HasMaxLength(500);
        });
    }
}
