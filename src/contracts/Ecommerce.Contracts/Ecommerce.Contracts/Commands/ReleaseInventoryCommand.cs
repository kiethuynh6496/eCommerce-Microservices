namespace Ecommerce.Contracts.Commands;

// Dùng cho compensation/rollback
public record ReleaseInventoryCommand
{
    public Guid OrderId { get; init; }
    public string ProductId { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public string Reason { get; init; } = string.Empty;
}