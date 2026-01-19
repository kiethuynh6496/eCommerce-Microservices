using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Order.Application.DTOs.External;
using Order.Application.Interfaces.External;

namespace Order.Infrastructure.ExternalServices
{
    public class ProductServiceClient : IProductServiceClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ProductServiceClient> _logger;

        public ProductServiceClient(HttpClient httpClient, ILogger<ProductServiceClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<ProductDto?> GetProductByIdAsync(string productId)
        {
            try
            {
                _logger.LogInformation("Calling Product Service: GET /api/products/{ProductId}", productId);

                var response = await _httpClient.GetAsync($"api/products/{productId}");

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("Product {ProductId} not found", productId);
                    return null;
                }

                response.EnsureSuccessStatusCode();

                var product = await response.Content.ReadFromJsonAsync<ProductDto>();

                _logger.LogInformation("Successfully retrieved product {ProductId}: {ProductName}",
                    productId, product?.Name);

                return product;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error when calling Product Service for product {ProductId}. Message: {Message}",
                    productId, ex.Message);
                throw new Exception($"Unable to connect to Product Service: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error calling Product Service for product {ProductId}", productId);
                throw;
            }
        }

        public async Task<List<ProductDto>> GetProductsByIdsAsync(List<string> productIds)
        {
            try
            {
                _logger.LogInformation("Calling Product Service: POST /api/products/bulk with {Count} product IDs",
                    productIds.Count);

                var response = await _httpClient.PostAsJsonAsync("api/products/bulk", productIds);

                response.EnsureSuccessStatusCode();

                var products = await response.Content.ReadFromJsonAsync<List<ProductDto>>();

                _logger.LogInformation("Successfully retrieved {Count} products from Product Service",
                    products?.Count ?? 0);

                return products ?? new List<ProductDto>();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error when calling Product Service bulk endpoint. Message: {Message}",
                    ex.Message);
                throw new Exception($"Unable to connect to Product Service: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error calling Product Service bulk endpoint");
                throw;
            }
        }

        public async Task<List<StockCheckResponse>> CheckStockAsync(List<StockCheckRequest> items)
        {
            try
            {
                _logger.LogInformation("Calling Product Service: POST /api/products/check-stock with {Count} items",
                    items.Count);

                var response = await _httpClient.PostAsJsonAsync("api/products/check-stock", items);

                response.EnsureSuccessStatusCode();

                var stockResults = await response.Content.ReadFromJsonAsync<List<StockCheckResponse>>();

                var unavailableItems = stockResults?.Where(r => !r.IsAvailable).ToList();
                if (unavailableItems?.Any() == true)
                {
                    _logger.LogWarning("Stock check found {Count} unavailable items", unavailableItems.Count);
                }

                return stockResults ?? new List<StockCheckResponse>();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error when calling Product Service check-stock endpoint. Message: {Message}",
                    ex.Message);
                throw new Exception($"Unable to connect to Product Service: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error calling Product Service check-stock endpoint");
                throw;
            }
        }
    }
}