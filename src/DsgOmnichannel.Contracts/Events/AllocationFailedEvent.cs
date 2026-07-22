namespace DsgOmnichannel.Contracts.Events;

public record AllocationFailedEvent(
    Guid OrderId,
    string StoreId,
    string SKU,
    string Reason,
    DateTime FailedAtUtc);
