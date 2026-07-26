namespace DsgOmnichannel.Contracts.Events;

public record OrderCancelledEvent(
    Guid OrderId,
    string StoreId,
    string ProductId,
    int Quantity,
    DateTime CancelledAtUtc);
