namespace DsgOmnichannel.IntegrationTests.Helpers;

/// <summary>
/// Fluent builder for constructing order submission payloads in integration tests.
/// Provides sensible defaults so each test only needs to override what matters.
/// </summary>
public sealed class OrderRequestBuilder
{
    private string _storeId = "STORE-001";
    private string _customerName = "Test Customer";
    private string _productId = "PROD-001";
    private int _quantity = 1;
    private decimal _totalAmount = 99.99m;

    public OrderRequestBuilder WithStoreId(string storeId)
    {
        _storeId = storeId;
        return this;
    }

    public OrderRequestBuilder WithCustomerName(string customerName)
    {
        _customerName = customerName;
        return this;
    }

    public OrderRequestBuilder WithProductId(string productId)
    {
        _productId = productId;
        return this;
    }

    public OrderRequestBuilder WithQuantity(int quantity)
    {
        _quantity = quantity;
        return this;
    }

    public OrderRequestBuilder WithTotalAmount(decimal totalAmount)
    {
        _totalAmount = totalAmount;
        return this;
    }

    public object Build() => new
    {
        StoreId = _storeId,
        CustomerName = _customerName,
        ProductId = _productId,
        Quantity = _quantity,
        TotalAmount = _totalAmount
    };
}
