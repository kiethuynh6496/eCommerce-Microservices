namespace Product.Domain.Repositories;

public interface IProductRepository
{
    Task<Entities.Product?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<List<Entities.Product>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Entities.Product> AddAsync(Entities.Product product, CancellationToken cancellationToken = default);
    Task UpdateAsync(Entities.Product product, CancellationToken cancellationToken = default);
}
