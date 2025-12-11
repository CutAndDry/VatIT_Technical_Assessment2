namespace VatIT.Domain.DTOs;

public class ApplicabilityRequestDto
{
    public string TransactionId { get; set; } = string.Empty;
    public string MerchantId { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public DateTime RequestTimestamp { get; set; } = DateTime.UtcNow;
}

public class ApplicabilityResponseDto
{
    public string TransactionId { get; set; } = string.Empty;
    public bool IsApplicable { get; set; }
    public string Message { get; set; } = string.Empty;
    public decimal MerchantVolume { get; set; }
    public decimal Threshold { get; set; }
    public List<string> AuditLogs { get; set; } = new();
    public DateTime ProcessedTimestamp { get; set; } = DateTime.UtcNow;
    public string GateName { get; set; } = "APPLICABILITY";
}
