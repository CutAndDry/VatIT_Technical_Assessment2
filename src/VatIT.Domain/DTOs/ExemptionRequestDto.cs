namespace VatIT.Domain.DTOs;

public class ExemptionRequestDto
{
    public string TransactionId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string MerchantId { get; set; } = string.Empty;
    public List<ItemDto> Items { get; set; } = new();
    public DateTime RequestTimestamp { get; set; } = DateTime.UtcNow;
}

public class ItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class ExemptionResponseDto
{
    public string TransactionId { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public List<string> AppliedExemptions { get; set; } = new();
    public string Message { get; set; } = string.Empty;
    public List<string> AuditLogs { get; set; } = new();
    public DateTime ProcessedTimestamp { get; set; } = DateTime.UtcNow;
    public string GateName { get; set; } = "EXEMPTION_CHECK";
}
