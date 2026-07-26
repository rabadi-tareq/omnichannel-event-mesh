using DsgOmnichannel.Contracts.Events;
using DsgOmnichannel.Infrastructure.Persistence.Sagas;
using MassTransit;

namespace DsgOmnichannel.Worker.Sagas;

/// <summary>
/// MassTransit state machine that orchestrates the order fulfillment saga.
/// </summary>
public class OrderStateMachine : MassTransitStateMachine<OrderState>
{
    public OrderStateMachine()
    {
        InstanceState(x => x.CurrentState);

        Event(() => OrderPlaced, x => x.CorrelateById(context => context.Message.OrderId));
        Event(() => AllocationFailed, x => x.CorrelateById(context => context.Message.OrderId));
        Event(() => OrderPickedUp, x => x.CorrelateById(context => context.Message.OrderId));

        Initially(
            When(OrderPlaced)
                .Then(context =>
                {
                    context.Saga.OrderPlacedDate = context.Message.CreatedAt;
                    context.Saga.StoreId = context.Message.StoreId;
                })
                .TransitionTo(Processing));

        During(Processing,
            When(AllocationFailed)
                .Then(context =>
                {
                    // Slice 2.2 fix: saga owns only its own state fields.
                    // Order.Status is exclusively owned by OrderPlacedEventConsumer,
                    // which already committed "AllocationFailed" before publishing this event.
                    // Removing the inner DbContext scope eliminates the split-transaction gap
                    // (Known Gap #6) — there is now exactly one transaction per authority.
                    context.Saga.FailureReason = context.Message.Reason;
                    context.Saga.FaultedAt = context.Message.FailedAtUtc;
                })
                .Finalize());

        During(Processing,
            When(OrderPickedUp)
                .Finalize());

        SetCompletedWhenFinalized();
    }

    public State Processing { get; private set; } = null!;

    public Event<OrderPlacedEvent> OrderPlaced { get; private set; } = null!;
    public Event<AllocationFailedEvent> AllocationFailed { get; private set; } = null!;
    public Event<OrderPickedUpEvent> OrderPickedUp { get; private set; } = null!;
}
