using Product.Application.DTOs;

namespace Product.Application.Interfaces
{
    public interface IProductService
    {
        Task<ProductDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
        Task<List<ProductDto>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<ProductDto> CreateAsync(CreateProductDto dto, CancellationToken cancellationToken = default);
        Task<ProductDto> UpdateAsync(string id, UpdateProductDto dto, CancellationToken cancellationToken = default);
    }
}
