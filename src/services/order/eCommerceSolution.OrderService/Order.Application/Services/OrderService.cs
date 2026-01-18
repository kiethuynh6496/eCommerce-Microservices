using Order.Application.DTOs;
using Order.Domain.Entities;
using Order.Domain.Repositories;

namespace OrderService.Application.Services;

public class OrderAppService : IOrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IOrderItemRepository _orderItemRepository;

    public OrderAppService(
        IOrderRepository orderRepository,
        IOrderItemRepository orderItemRepository)
    {
        _orderRepository = orderRepository;
        _orderItemRepository = orderItemRepository;
    }

    public async Task<OrderDto?> GetOrderByIdAsync(string id)
    {
        var order = await _orderRepository.GetByIdAsync(id);
        if (order == null) return null;

        return MapToDto(order);
    }

    public async Task<IEnumerable<OrderDto>> GetOrdersByUserIdAsync(string userId)
    {
        var orders = await _orderRepository.GetByUserIdAsync(userId);
        return orders.Select(MapToDto);
    }

    public async Task<OrderDto> CreateOrderAsync(CreateOrderDto createOrderDto)
    {
        var order = new Order.Domain.Entities.Order
        {
            UserId = createOrderDto.UserId,
            CustomerName = createOrderDto.CustomerName,
            CustomerEmail = createOrderDto.CustomerEmail,
            ShippingAddress = createOrderDto.ShippingAddress,
            PhoneNumber = createOrderDto.PhoneNumber,
            Notes = createOrderDto.Notes,
            Status = OrderStatus.Pending
        };

        // Calculate total amount and create order items
        decimal totalAmount = 0;
        var orderItems = new List<OrderItem>();

        foreach (var itemDto in createOrderDto.OrderItems)
        {
            var totalPrice = itemDto.Quantity * itemDto.UnitPrice;
            totalAmount += totalPrice;

            var orderItem = new OrderItem
            {
                OrderId = order.Id,
                ProductId = itemDto.ProductId,
                ProductName = itemDto.ProductName,
                Quantity = itemDto.Quantity,
                UnitPrice = itemDto.UnitPrice,
                TotalPrice = totalPrice
            };

            orderItems.Add(orderItem);
        }

        order.TotalAmount = totalAmount;
        order.OrderItems = orderItems;

        var createdOrder = await _orderRepository.CreateAsync(order);

        // Create order items
        foreach (var item in orderItems)
        {
            await _orderItemRepository.CreateAsync(item);
        }

        return MapToDto(createdOrder);
    }

    public async Task<bool> UpdateOrderAsync(string id, UpdateOrderDto updateOrderDto)
    {
        var order = await _orderRepository.GetByIdAsync(id);
        if (order == null) return false;

        // Only allow updates for pending or processing orders
        if (order.Status != OrderStatus.Pending && order.Status != OrderStatus.Processing)
        {
            return false;
        }

        order.ShippingAddress = updateOrderDto.ShippingAddress;
        order.PhoneNumber = updateOrderDto.PhoneNumber;
        order.Notes = updateOrderDto.Notes;
        order.UpdatedAt = DateTime.UtcNow;

        return await _orderRepository.UpdateAsync(order);
    }

    public async Task<bool> UpdateOrderStatusAsync(string id, UpdateOrderStatusDto updateStatusDto)
    {
        var order = await _orderRepository.GetByIdAsync(id);
        if (order == null) return false;

        order.Status = updateStatusDto.Status;
        order.UpdatedAt = DateTime.UtcNow;

        if (updateStatusDto.Status == OrderStatus.Delivered)
        {
            order.CompletedAt = DateTime.UtcNow;
        }
        else if (updateStatusDto.Status == OrderStatus.Cancelled)
        {
            order.CancelledAt = DateTime.UtcNow;
        }

        return await _orderRepository.UpdateAsync(order);
    }

    public async Task<bool> DeleteOrderAsync(string id)
    {
        var order = await _orderRepository.GetByIdAsync(id);
        if (order == null) return false;

        // Delete order items first
        await _orderItemRepository.DeleteByOrderIdAsync(id);

        // Delete order
        return await _orderRepository.DeleteAsync(id);
    }

    private OrderDto MapToDto(Order.Domain.Entities.Order order)
    {
        return new OrderDto
        {
            Id = order.Id,
            UserId = order.UserId,
            CustomerName = order.CustomerName,
            CustomerEmail = order.CustomerEmail,
            ShippingAddress = order.ShippingAddress,
            PhoneNumber = order.PhoneNumber,
            Status = order.Status,
            TotalAmount = order.TotalAmount,
            Notes = order.Notes,
            CreatedAt = order.CreatedAt,
            UpdatedAt = order.UpdatedAt,
            CompletedAt = order.CompletedAt,
            CancelledAt = order.CancelledAt,
            OrderItems = order.OrderItems.Select(item => new OrderItemDto
            {
                Id = item.Id,
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                TotalPrice = item.TotalPrice
            }).ToList()
        };
    }
}