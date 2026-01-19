using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Order.Application.Sagas;

namespace OrderService.Infrastructure.Data.Configurations;

public class OrderStateConfiguration : SagaClassMap<OrderState>
{
    protected override void Configure(EntityTypeBuilder<OrderState> entity, ModelBuilder model)
    {
        entity.ToTable("OrderStates");

        entity.HasKey(x => x.CorrelationId);

        entity.Property(x => x.CurrentState).HasMaxLength(64).IsRequired();
        entity.Property(x => x.ProductId).HasMaxLength(50);
        entity.Property(x => x.CustomerId).HasMaxLength(50);
        entity.Property(x => x.ErrorMessage).HasMaxLength(500);

        entity.HasIndex(x => x.OrderId);
        entity.HasIndex(x => x.CurrentState);
    }
}