using MassTransit;
using Ecommerce.Contracts.Commands;
using Product.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Product.Application.Consumers;

/// <summary>
/// Consumer xử lý ReleaseInventoryCommand (Compensation/Rollback)
/// Được gọi khi Saga cần rollback transaction
/// Nhiệm vụ: Cộng lại stock đã trừ
/// </summary>
public class ReleaseInventoryConsumer : IConsumer<ReleaseInventoryCommand>
{
    private readonly IProductRepository _productRepository;
    private readonly ILogger<ReleaseInventoryConsumer> _logger;

    public ReleaseInventoryConsumer(
        IProductRepository productRepository,
        ILogger<ReleaseInventoryConsumer> logger)
    {
        _productRepository = productRepository;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ReleaseInventoryCommand> context)
    {
        var command = context.Message;

        _logger.LogInformation(
            "[ReleaseInventoryConsumer] Received compensation command - OrderId: {OrderId}, ProductId: {ProductId}, Quantity: {Quantity}",
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
                    "[ReleaseInventoryConsumer] ⚠ Product {ProductId} not found - cannot release inventory",
                    command.ProductId);

                // Not throwing exception - idempotency
                // Nếu product không tồn tại, coi như đã rollback thành công
                return;
            }

            // ================================================
            // STEP 2: Release Inventory (Cộng lại Stock)
            // ================================================
            var oldStock = product.Stock;
            product.Stock += command.Quantity;
            product.UpdatedAt = DateTime.UtcNow;

            await _productRepository.UpdateAsync(product);

            _logger.LogInformation(
                "[ReleaseInventoryConsumer] ✓ Inventory released (rolled back) - ProductId: {ProductId}, Stock: {OldStock} → {NewStock}",
                command.ProductId, oldStock, product.Stock);

            // Note: Không cần publish event cho compensation
            // Saga sẽ tự động transition sang Failed state
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[ReleaseInventoryConsumer] ✗ Error releasing inventory - OrderId: {OrderId}, ProductId: {ProductId}",
                command.OrderId, command.ProductId);

            throw; // Trigger retry
        }
    }
}