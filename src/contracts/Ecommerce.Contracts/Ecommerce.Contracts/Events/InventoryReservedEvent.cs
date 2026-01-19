namespace Ecommerce.Contracts.Events;

public record OrderCreatedEvent
{
    public Guid OrderId { get; init; }
    public string ProductId { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public decimal Price { get; init; }
    public string CustomerId { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}