namespace VatIT.Domain.DTOs;

public class CalculationRequestDto
{
    public string TransactionId { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string County { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public List<ItemDto> Items { get; set; } = new();
    public decimal TotalAmount { get; set; }
    public DateTime RequestTimestamp { get; set; } = DateTime.UtcNow;
}

public class CalculationResponseDto
{
    public string TransactionId { get; set; } = string.Empty;
    public List<ItemCalculationDto> Items { get; set; } = new();
    public decimal TotalFees { get; set; }
    public decimal EffectiveRate { get; set; }
    public List<string> AuditLogs { get; set; } = new();
    public DateTime ProcessedTimestamp { get; set; } = DateTime.UtcNow;
    public string GateName { get; set; } = "CALCULATION";
}

public class ItemCalculationDto
{
    public string ItemId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Category { get; set; } = string.Empty;
    public FeesDto Fees { get; set; } = new();
    public decimal TotalFee { get; set; }
}

public class FeesDto
{
    public RateInfoDto? StateRate { get; set; }
    public RateInfoDto? CountyRate { get; set; }
    public RateInfoDto? CityRate { get; set; }
    public RateInfoDto? CategoryModifier { get; set; }
}

public class RateInfoDto
{
    public string Jurisdiction { get; set; } = string.Empty;
    public string? Category { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
}
