using MassTransit;

namespace Order.Application.Sagas;

/// <summary>
/// Saga State cho Order creation flow
/// Tracks trạng thái của distributed transaction
/// </summary>
public class OrderState : SagaStateMachineInstance
{
    /// <summary>
    /// Correlation ID - Unique identifier cho saga instance
    /// </summary>
    public Guid CorrelationId { get; set; }

    /// <summary>
    /// Current state của saga (Initial, Pending, Completed, Failed, etc.)
    /// </summary>
    public string CurrentState { get; set; } = null!;

    // ============================================
    // Business Data
    // ============================================

    /// <summary>
    /// Order ID từ MongoDB
    /// </summary>
    public Guid OrderId { get; set; }

    /// <summary>
    /// Product ID cần reserve inventory
    /// </summary>
    public string ProductId { get; set; } = null!;

    /// <summary>
    /// Số lượng cần reserve
    /// </summary>
    public int Quantity { get; set; }

    // ============================================
    // Timestamps
    // ============================================

    /// <summary>
    /// Thời điểm saga được khởi tạo
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Thời điểm saga hoàn thành (success hoặc fail)
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    // ============================================
    // Error Handling
    // ============================================

    /// <summary>
    /// Error message nếu saga failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Số lần retry (cho compensation logic)
    /// </summary>
    public int RetryCount { get; set; }

    // ============================================
    // Concurrency Control
    // ============================================

    /// <summary>
    /// Row version cho optimistic concurrency control
    /// Prevents lost updates trong concurrent scenarios
    /// </summary>
    public byte[]? RowVersion { get; set; }
}