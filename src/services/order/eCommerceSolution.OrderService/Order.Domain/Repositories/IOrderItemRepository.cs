using Order.Domain.Entities;

namespace Order.Domain.Repositories;

public interface IOrderItemRepository
{
    Task<OrderItem?> GetByIdAsync(string id);
    Task<IEnumerable<OrderItem>> GetByOrderIdAsync(string orderId);
    Task<OrderItem> CreateAsync(OrderItem orderItem);
    Task<bool> UpdateAsync(OrderItem orderItem);
    Task<bool> DeleteAsync(string id);
    Task<bool> DeleteByOrderIdAsync(string orderId);
}