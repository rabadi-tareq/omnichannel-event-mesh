namespace DsgOmnichannel.Contracts.Events;

public record AllocationFailedEvent(
    Guid OrderId,
    string StoreId,
    string ProductId,
    string Reason,
    DateTime FailedAtUtc);
