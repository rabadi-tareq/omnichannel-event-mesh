using DsgOmnichannel.Contracts.Events;
using DsgOmnichannel.Infrastructure.Persistence;
using DsgOmnichannel.Infrastructure.Persistence.Sagas;
using MassTransit;
using Microsoft.EntityFrameworkCore;

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
                .ThenAsync(async context =>
                {
                    context.Saga.FailureReason = context.Message.Reason;
                    context.Saga.FaultedAt = context.Message.FailedAtUtc;

                    var serviceProvider = context.GetPayload<IServiceProvider>();
                    using var scope = serviceProvider.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    var order = await dbContext.Orders.FindAsync(context.Message.OrderId);
                    if (order != null)
                    {
                        order.Status = "AllocationFailed";
                        await dbContext.SaveChangesAsync();
                    }
                })
                .TransitionTo(Faulted));

                        During(Processing,
                            When(OrderPickedUp)
                                .Finalize());

                        SetCompletedWhenFinalized();
                    }

                    public State Processing { get; private set; } = null!;
                    public State Faulted { get; private set; } = null!;

                    public Event<OrderPlacedEvent> OrderPlaced { get; private set; } = null!;
                    public Event<AllocationFailedEvent> AllocationFailed { get; private set; } = null!;
                    public Event<OrderPickedUpEvent> OrderPickedUp { get; private set; } = null!;
}
