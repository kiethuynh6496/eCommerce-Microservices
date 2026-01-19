using Order.Application.DTOs;

namespace Order.Application.Services;

public interface IOrderService
{
    Task<OrderDto?> GetOrderByIdAsync(string id);
    Task<IEnumerable<OrderDto>> GetOrdersByUserIdAsync(string userId);
    Task<OrderDto> CreateOrderAsync(CreateOrderDto createOrderDto);
    Task<bool> UpdateOrderAsync(string id, UpdateOrderDto updateOrderDto);
    Task<bool> UpdateOrderStatusAsync(string id, UpdateOrderStatusDto updateStatusDto);
    Task<bool> DeleteOrderAsync(string id);
}