namespace Ecommerce.Contracts.Commands;

public record ReserveInventoryCommand
{
    public Guid OrderId { get; init; }
    public string ProductId { get; init; } = string.Empty;
    public int Quantity { get; init; }
}