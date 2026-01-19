using Microsoft.EntityFrameworkCore;
using MassTransit;
using Order.Application.Sagas;

namespace Order.Infrastructure.Data;

/// <summary>
/// DbContext riêng cho Saga State persistence (MySQL)
/// Tách biệt với OrderDbContext (MongoDB) để tận dụng ACID của SQL
/// </summary>
public class SagaDbContext : DbContext
{
    public SagaDbContext(DbContextOptions<SagaDbContext> options)
        : base(options)
    {
    }

    // DbSet cho Saga State
    public DbSet<OrderState> OrderStates { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Saga State Machine Instance
        modelBuilder.Entity<OrderState>(entity =>
        {
            entity.ToTable("OrderSagaStates");

            // Primary Key
            entity.HasKey(x => x.CorrelationId);

            // Indexes for performance
            entity.HasIndex(x => x.OrderId);
            entity.HasIndex(x => x.CurrentState);
            entity.HasIndex(x => x.CreatedAt);

            // Properties
            entity.Property(x => x.CorrelationId)
                .IsRequired()
                .HasMaxLength(36);

            entity.Property(x => x.CurrentState)
                .IsRequired()
                .HasMaxLength(64);

            entity.Property(x => x.OrderId)
                .IsRequired();

            entity.Property(x => x.ProductId)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.Quantity)
                .IsRequired();

            entity.Property(x => x.CreatedAt)
                .IsRequired();

            entity.Property(x => x.CompletedAt)
                .IsRequired(false);

            entity.Property(x => x.ErrorMessage)
                .HasMaxLength(1000);

            entity.Property(x => x.RetryCount)
                .HasDefaultValue(0);

            // Optional: Add RowVersion for optimistic concurrency
            entity.Property(x => x.RowVersion)
                .IsRowVersion();
        });

        // Add MassTransit Outbox tables
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}