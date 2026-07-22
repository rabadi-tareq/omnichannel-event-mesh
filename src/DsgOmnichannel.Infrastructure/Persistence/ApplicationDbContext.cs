using DsgOmnichannel.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DsgOmnichannel.Infrastructure.Persistence;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AuditLog>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.EventType).HasMaxLength(100).IsRequired();
            builder.Property(x => x.Details).HasMaxLength(1000);
        });
    }
}