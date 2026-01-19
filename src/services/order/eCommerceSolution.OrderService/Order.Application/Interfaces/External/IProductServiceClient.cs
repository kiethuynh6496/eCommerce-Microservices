using Order.Application.DTOs.External;

namespace Order.Application.Interfaces.External
{
    /// <summary>
    /// Client để gọi Product Service
    /// </summary>
    public interface IProductServiceClient
    {
        /// <summary>
        /// Lấy thông tin 1 sản phẩm
        /// </summary>
        Task<ProductDto?> GetProductByIdAsync(string productId);

        /// <summary>
        /// Lấy nhiều sản phẩm cùng lúc
        /// </summary>
        Task<List<ProductDto>> GetProductsByIdsAsync(List<string> productIds);

        /// <summary>
        /// Check stock availability cho nhiều products
        /// </summary>
        Task<List<StockCheckResponse>> CheckStockAsync(List<StockCheckRequest> items);
    }
}