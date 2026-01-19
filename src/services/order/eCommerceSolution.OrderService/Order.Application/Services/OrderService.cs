using MassTransit;
using Order.Application.DTOs;
using Order.Application.DTOs.External;
using Order.Application.Interfaces.External;
using Order.Domain.Entities;
using Order.Domain.Repositories;
using Ecommerce.Contracts.Events;

namespace Order.Application.Services;

public class OrderAppService : IOrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IOrderItemRepository _orderItemRepository;
    private readonly IProductServiceClient _productServiceClient;
    private readonly IPublishEndpoint _publishEndpoint;

    public OrderAppService(
        IOrderRepository orderRepository,
        IOrderItemRepository orderItemRepository,
        IProductServiceClient productServiceClient,
        IPublishEndpoint publishEndpoint)
    {
        _orderRepository = orderRepository;
        _orderItemRepository = orderItemRepository;
        _productServiceClient = productServiceClient;
        _publishEndpoint = publishEndpoint;
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
        // ================================================
        // STEP 1: Validate Input
        // ================================================
        if (!createOrderDto.OrderItems.Any())
        {
            throw new ArgumentException("Order must contain at least one item");
        }

        // ================================================
        // STEP 2: Get Product IDs and call Product Service
        // ================================================
        var productIds = createOrderDto.OrderItems
            .Select(x => x.ProductId)
            .Distinct()
            .ToList();

        // Get products from Product Service
        var products = await _productServiceClient.GetProductsByIdsAsync(productIds);

        // ================================================
        // STEP 3: Validate all products exist
        // ================================================
        if (products.Count != productIds.Count)
        {
            var foundIds = products.Select(p => p.Id).ToList();
            var missingIds = productIds.Except(foundIds).ToList();

            throw new Exception($"Products not found: {string.Join(", ", missingIds)}");
        }

        // ================================================
        // STEP 4: Check stock availability (Initial Check)
        // ================================================
        var stockCheckRequests = createOrderDto.OrderItems.Select(item => new StockCheckRequest
        {
            ProductId = item.ProductId,
            Quantity = item.Quantity
        }).ToList();

        var stockResults = await _productServiceClient.CheckStockAsync(stockCheckRequests);

        var unavailableItems = stockResults.Where(r => !r.IsAvailable).ToList();
        if (unavailableItems.Any())
        {
            var errorMessages = unavailableItems.Select(item =>
                $"{item.ProductName}: requested {item.RequestedQuantity}, available {item.AvailableStock}"
            );

            throw new Exception($"Insufficient stock: {string.Join("; ", errorMessages)}");
        }

        // ================================================
        // STEP 5: Create Order Entity (Status = Pending)
        // ================================================
        var order = new Order.Domain.Entities.Order
        {
            UserId = createOrderDto.UserId,
            CustomerName = createOrderDto.CustomerName,
            CustomerEmail = createOrderDto.CustomerEmail,
            ShippingAddress = createOrderDto.ShippingAddress,
            PhoneNumber = createOrderDto.PhoneNumber,
            Notes = createOrderDto.Notes,
            Status = OrderStatus.Pending // Waiting for Saga to reserve inventory
        };

        // Calculate total amount and create order items with Product Service data
        decimal totalAmount = 0;
        var orderItems = new List<OrderItem>();

        foreach (var itemDto in createOrderDto.OrderItems)
        {
            // Get product info from Product Service response
            var product = products.First(p => p.Id == itemDto.ProductId);

            var totalPrice = itemDto.Quantity * product.Price;
            totalAmount += totalPrice;

            var orderItem = new OrderItem
            {
                OrderId = order.Id,
                ProductId = itemDto.ProductId,
                ProductName = product.Name,        // From Product Service
                Quantity = itemDto.Quantity,
                UnitPrice = product.Price,         // From Product Service
                TotalPrice = totalPrice
            };

            orderItems.Add(orderItem);
        }

        order.TotalAmount = totalAmount;
        order.OrderItems = orderItems;

        // ================================================
        // STEP 6: Save to MongoDB
        // ================================================
        var createdOrder = await _orderRepository.CreateAsync(order);

        // Create order items
        foreach (var item in orderItems)
        {
            await _orderItemRepository.CreateAsync(item);
        }

        // ================================================
        // STEP 7: SAGA INTEGRATION - Publish OrderCreatedEvent
        // ================================================
        // NOTE: Chỉ publish event cho single-product orders để đơn giản hóa Phase 4
        // Multi-product orders sẽ được handle trong Phase nâng cao
        var firstItem = orderItems.First();

        await _publishEndpoint.Publish<OrderCreatedEvent>(new
        {
            CorrelationId = Guid.NewGuid(), // Saga correlation ID
            OrderId = Guid.Parse(createdOrder.Id),
            ProductId = firstItem.ProductId,
            Quantity = firstItem.Quantity,
            CustomerId = createdOrder.UserId,
            TotalAmount = createdOrder.TotalAmount,
            CreatedAt = createdOrder.CreatedAt
        });

        Console.WriteLine($"[OrderService] ✓ Order {createdOrder.Id} created with status: {createdOrder.Status}");
        Console.WriteLine($"[OrderService] ✓ Published OrderCreatedEvent - CorrelationId: {createdOrder.Id}");
        Console.WriteLine($"[OrderService] → Saga will now reserve inventory for ProductId: {firstItem.ProductId}");

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

        var oldStatus = order.Status;
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

        var result = await _orderRepository.UpdateAsync(order);

        if (result)
        {
            Console.WriteLine($"[OrderService] ✓ Order {id} status changed: {oldStatus} → {order.Status}");
        }

        return result;
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