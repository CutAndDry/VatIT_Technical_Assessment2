namespace VatIT.Domain.Entities;

public class TransactionResponse
{
    public string TransactionId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public List<GateResult> Gates { get; set; } = new();
    public CalculationResult? Calculation { get; set; }
    public List<string> AuditTrail { get; set; } = new();
    // Per-worker timings in milliseconds (e.g. { "validation": 12.3 })
    public Dictionary<string, double>? WorkerTimings { get; set; }
}

public class GateResult
{
    public string Name { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string>? AppliedExemptions { get; set; }
}

public class CalculationResult
{
    public List<ItemCalculation> Items { get; set; } = new();
    public decimal TotalFees { get; set; }
    public decimal EffectiveRate { get; set; }
}

public class ItemCalculation
{
    public string ItemId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Category { get; set; } = string.Empty;
    public Fees Fees { get; set; } = new();
    public decimal TotalFee { get; set; }
}

public class Fees
{
    public RateInfo? StateRate { get; set; }
    public RateInfo? CountyRate { get; set; }
    public RateInfo? CityRate { get; set; }
    public RateInfo? CategoryModifier { get; set; }
}

public class RateInfo
{
    public string Jurisdiction { get; set; } = string.Empty;
    public string? Category { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
}
