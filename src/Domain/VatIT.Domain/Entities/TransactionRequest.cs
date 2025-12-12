using System.ComponentModel.DataAnnotations;

namespace VatIT.Domain.Entities;


public class TransactionRequest
{
    [Required]
    public string TransactionId { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string MerchantId { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string CustomerId { get; set; } = string.Empty;

    [Required]
    public Destination Destination { get; set; } = new();

    [Required]
    [MinLength(1)]
    public List<Item> Items { get; set; } = new();

    [Range(0.01, double.MaxValue)]
    public decimal TotalAmount { get; set; }

    [Required]
    [StringLength(3, MinimumLength = 3)]
    public string Currency { get; set; } = string.Empty;
}

public class Destination
{
    [Required]
    [StringLength(2, MinimumLength = 2)]
    public string Country { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string State { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string City { get; set; } = string.Empty;
}

public class Item
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Id { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Category { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }
}
