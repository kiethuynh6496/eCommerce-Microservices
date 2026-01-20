namespace Ecommerce.Contracts.Events;

public record InventoryReservedEvent
{
    public Guid OrderId { get; init; }
    public Guid CorrelationId { get; init; }
    public string ProductId { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public DateTime ReservedAt { get; init; }
}