namespace DsgOmnichannel.Contracts.Events;

public record OrderPlacedEvent(
    Guid OrderId,
    string StoreId,
    string CustomerName,
    string ProductId,
    int Quantity,
    decimal TotalAmount,
    DateTime CreatedAt);
