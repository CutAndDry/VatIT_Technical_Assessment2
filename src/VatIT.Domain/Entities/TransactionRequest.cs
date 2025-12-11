namespace VatIT.Domain.Entities;

public class TransactionRequest
{
    public string TransactionId { get; set; } = string.Empty;
    public string MerchantId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public Destination Destination { get; set; } = new();
    public List<Item> Items { get; set; } = new();
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
}

public class Destination
{
    public string Country { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
}

public class Item
{
    public string Id { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}
