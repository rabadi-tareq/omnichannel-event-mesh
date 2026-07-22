namespace DsgOmnichannel.Contracts.Events;

public record OrderPlacedEvent(
    Guid OrderId,
    string CustomerName,
    string SKU,
    int Quantity,
    decimal TotalAmount,
    DateTime CreatedAtUtc);
