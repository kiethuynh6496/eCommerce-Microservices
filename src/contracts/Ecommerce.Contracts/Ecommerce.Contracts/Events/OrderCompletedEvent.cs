namespace Ecommerce.Contracts.Events;

public record OrderCompletedEvent
{
    public Guid OrderId { get; init; }
    public DateTime CompletedAt { get; init; }
}