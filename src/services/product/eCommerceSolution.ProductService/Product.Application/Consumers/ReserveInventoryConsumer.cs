using MassTransit;
using Ecommerce.Contracts.Commands;
using Ecommerce.Contracts.Events;
using Product.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Product.Application.Consumers;

/// <summary>
/// Consumer xử lý ReserveInventoryCommand từ Order Saga
/// Nhiệm vụ:
/// 1. Check stock availability
/// 2. Reserve (trừ) stock nếu đủ
/// 3. Publish InventoryReservedEvent hoặc InventoryReservationFailedEvent
/// </summary>
public class ReserveInventoryConsumer : IConsumer<ReserveInventoryCommand>
{
    private readonly IProductRepository _productRepository;
    private readonly ILogger<ReserveInventoryConsumer> _logger;

    public ReserveInventoryConsumer(
        IProductRepository productRepository,
        ILogger<ReserveInventoryConsumer> logger)
    {
        _productRepository = productRepository;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ReserveInventoryCommand> context)
    {
        var command = context.Message;

        _logger.LogInformation(
            "[ReserveInventoryConsumer] Received command - OrderId: {OrderId}, ProductId: {ProductId}, Quantity: {Quantity}",
            command.OrderId, command.ProductId, command.Quantity);

        try
        {
            // ================================================
            // STEP 1: Get Product
            // ================================================
            var product = await _productRepository.GetByIdAsync(command.ProductId);

            if (product == null)
            {
                _logger.LogWarning(
                    "[ReserveInventoryConsumer] ✗ Product {ProductId} not found",
                    command.ProductId);

                await PublishReservationFailed(context,
                    command.OrderId,
                    command.ProductId,
                    $"Product {command.ProductId} not found");
                return;
            }

            // ================================================
            // STEP 2: Check Stock Availability
            // ================================================
            if (product.Stock < command.Quantity)
            {
                _logger.LogWarning(
                    "[ReserveInventoryConsumer] ✗ Insufficient stock - ProductId: {ProductId}, Requested: {Requested}, Available: {Available}",
                    command.ProductId, command.Quantity, product.Stock);

                await PublishReservationFailed(context,
                    command.OrderId,
                    command.ProductId,
                    $"Insufficient stock. Requested: {command.Quantity}, Available: {product.Stock}");
                return;
            }

            // ================================================
            // STEP 3: Reserve Inventory (Trừ Stock)
            // ================================================
            var oldStock = product.Stock;
            product.Stock -= command.Quantity;
            product.UpdatedAt = DateTime.UtcNow;

            await _productRepository.UpdateAsync(product);

            _logger.LogInformation(
                "[ReserveInventoryConsumer] ✓ Inventory reserved - ProductId: {ProductId}, Stock: {OldStock} → {NewStock}",
                command.ProductId, oldStock, product.Stock);

            // ================================================
            // STEP 4: Publish Success Event
            // ================================================
            await context.Publish<InventoryReservedEvent>(new
            {
                CorrelationId = command.CorrelationId,
                OrderId = command.OrderId,
                ProductId = command.ProductId,
                Quantity = command.Quantity,
                ReservedAt = DateTime.UtcNow
            });

            _logger.LogInformation(
                "[ReserveInventoryConsumer] ✓ Published InventoryReservedEvent - OrderId: {OrderId}",
                command.OrderId);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Xử lý concurrent updates
            _logger.LogError(ex,
                "[ReserveInventoryConsumer] ✗ Concurrency conflict for ProductId: {ProductId}",
                command.ProductId);

            await PublishReservationFailed(context,
                command.OrderId,
                command.ProductId,
                "Concurrency conflict - please retry");

            throw; // Trigger retry
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[ReserveInventoryConsumer] ✗ Error reserving inventory - OrderId: {OrderId}, ProductId: {ProductId}",
                command.OrderId, command.ProductId);

            await PublishReservationFailed(context,
                command.OrderId,
                command.ProductId,
                $"Error: {ex.Message}");

            throw; // Trigger retry
        }
    }

    private async Task PublishReservationFailed(
        ConsumeContext context,
        Guid orderId,
        string productId,
        string reason)
    {
        await context.Publish<InventoryReservationFailedEvent>(new
        {
            CorrelationId = context.CorrelationId,
            OrderId = orderId,
            ProductId = productId,
            Reason = reason,
            FailedAt = DateTime.UtcNow
        });

        _logger.LogInformation(
            "[ReserveInventoryConsumer] ✓ Published InventoryReservationFailedEvent - OrderId: {OrderId}, Reason: {Reason}",
            orderId, reason);
    }
}