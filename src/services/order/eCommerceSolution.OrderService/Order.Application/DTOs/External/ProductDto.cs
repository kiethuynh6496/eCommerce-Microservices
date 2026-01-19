namespace Order.Application.DTOs.External
{
    /// <summary>
    /// Nhận dữ liệu từ Product Service
    /// </summary>
    public class ProductDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public string? Description { get; set; }
    }

    /// <summary>
    /// Request để check stock nhiều products
    /// </summary>
    public class StockCheckRequest
    {
        public string ProductId { get; set; } = string.Empty;
        public int Quantity { get; set; }
    }

    /// <summary>
    /// Response từ stock check
    /// </summary>
    public class StockCheckResponse
    {
        public string ProductId { get; set; } = string.Empty;
        public int RequestedQuantity { get; set; }
        public int AvailableStock { get; set; }
        public bool IsAvailable { get; set; }
        public string ProductName { get; set; } = string.Empty;
    }
}