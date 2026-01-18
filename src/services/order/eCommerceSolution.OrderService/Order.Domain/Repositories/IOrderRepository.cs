using Order.Domain.Entities;

namespace Order.Domain.Repositories;

public interface IOrderRepository
{
    Task<Entities.Order?> GetByIdAsync(string id);
    Task<IEnumerable<Entities.Order>> GetByUserIdAsync(string userId);
    Task<Entities.Order> CreateAsync(Entities.Order order);
    Task<bool> UpdateAsync(Entities.Order order);
    Task<bool> DeleteAsync(string id);
    Task<IEnumerable<Entities.Order>> GetByStatusAsync(OrderStatus status);
}