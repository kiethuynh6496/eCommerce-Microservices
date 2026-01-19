using Ecommerce.Contracts.Commands;
using Ecommerce.Contracts.Events;
using MassTransit;

namespace Order.Application.Sagas;

public class OrderStateMachine : MassTransitStateMachine<OrderState>
{
    public OrderStateMachine()
    {
        InstanceState(x => x.CurrentState);

        Event(() => OrderCreated, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => InventoryReserved, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => InventoryReservationFailed, x => x.CorrelateById(m => m.Message.OrderId));

        Initially(
            When(OrderCreated)
                .Then(context =>
                {
                    context.Saga.OrderId = context.Message.OrderId;
                    context.Saga.ProductId = context.Message.ProductId;
                    context.Saga.Quantity = context.Message.Quantity;
                    context.Saga.CreatedAt = context.Message.CreatedAt;
                })
                .Send(context => new Uri("queue:reserve-inventory"),
                    context => new ReserveInventoryCommand
                    {
                        OrderId = context.Saga.OrderId,
                        ProductId = context.Saga.ProductId,
                        Quantity = context.Saga.Quantity
                    })
                .TransitionTo(InventoryReservationPending)
        );

        During(InventoryReservationPending,
            When(InventoryReserved)
                .Then(context =>
                {
                    context.Saga.CompletedAt = DateTime.UtcNow;
                })
                .Publish(context => new OrderCompletedEvent
                {
                    CompletedAt = DateTime.UtcNow
                })
                .Finalize()
        );

        During(InventoryReservationPending,
            When(InventoryReservationFailed)
                .Then(context =>
                {
                    context.Saga.ErrorMessage = context.Message.Reason;
                })
                .Publish(context => new OrderFailedEvent
                {
                    Reason = context.Message.Reason,
                    FailedAt = DateTime.UtcNow
                })
                .TransitionTo(Failed)
        );

        SetCompletedWhenFinalized();
    }

    public State InventoryReservationPending { get; private set; } = null!;
    public State Failed { get; private set; } = null!;

    public Event<OrderCreatedEvent> OrderCreated { get; private set; } = null!;
    public Event<InventoryReservedEvent> InventoryReserved { get; private set; } = null!;
    public Event<InventoryReservationFailedEvent> InventoryReservationFailed { get; private set; } = null!;
}