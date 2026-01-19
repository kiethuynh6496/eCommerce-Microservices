namespace Ecommerce.Contracts.Events;

public record InventoryReservationFailedEvent
{
    public Guid OrderId { get; init; }
    public string ProductId { get; init; } = string.Empty;
    public int RequestedQuantity { get; init; }
    public int AvailableQuantity { get; init; }
    public string Reason { get; init; } = string.Empty;
    public DateTime FailedAt { get; init; }
}