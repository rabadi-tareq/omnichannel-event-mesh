namespace DsgOmnichannel.Domain.Entities;

public class StoreInventory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string StoreId { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; }
}
