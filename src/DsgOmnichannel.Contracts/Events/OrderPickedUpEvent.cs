namespace DsgOmnichannel.Contracts.Events;

public record OrderPickedUpEvent(
    Guid OrderId,
    string StoreId,
    string AssociateId,
    DateTime PickedUpAtUtc);
