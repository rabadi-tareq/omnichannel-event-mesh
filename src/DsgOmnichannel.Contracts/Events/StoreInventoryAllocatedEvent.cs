namespace DsgOmnichannel.Contracts.Events;

public record StoreInventoryAllocatedEvent(
    Guid OrderId,
    string StoreId,
    string SKU,
    int QuantityAllocated,
    DateTime AllocatedAtUtc);
