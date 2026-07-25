using MassTransit;

namespace DsgOmnichannel.Infrastructure.Persistence.Sagas;

/// <summary>
/// Saga state instance for the order fulfillment workflow.
/// Persisted via EF Core using <see cref="CorrelationId"/> as the primary key.
/// </summary>
public class OrderState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = string.Empty;
    public DateTime OrderPlacedDate { get; set; }
    public string StoreId { get; set; } = string.Empty;
    public string? FailureReason { get; set; }
    public DateTime? FaultedAt { get; set; }
}
