namespace VatIT.Domain.DTOs;

public class ValidationRequestDto
{
    public string TransactionId { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public DateTime RequestTimestamp { get; set; } = DateTime.UtcNow;
}

public class ValidationResponseDto
{
    public string TransactionId { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> AuditLogs { get; set; } = new();
    public DateTime ProcessedTimestamp { get; set; } = DateTime.UtcNow;
    public string GateName { get; set; } = "ADDRESS_VALIDATION";
}
