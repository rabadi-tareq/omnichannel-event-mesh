namespace DsgOmnichannel.Contracts.Events;

public record StoreInventoryAllocatedEvent(
    Guid OrderId,
    string StoreId,
    string ProductId,
    int Quantity,
    DateTime AllocatedAtUtc);
