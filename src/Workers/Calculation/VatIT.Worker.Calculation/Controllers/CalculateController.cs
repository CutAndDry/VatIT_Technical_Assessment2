using Microsoft.AspNetCore.Mvc;
using VatIT.Domain.DTOs;

namespace VatIT.Worker.Calculation.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CalculateController : ControllerBase
{
    private readonly ILogger<CalculateController> _logger;

    // Tax rate tables (defaults; may be overridden by remote rules)
    private Dictionary<string, decimal> _stateRates = new()
    {
        ["CA"] = 0.06m,
        ["NY"] = 0.04m,
        ["TX"] = 0.0625m,
        ["FL"] = 0.06m
    };

    private Dictionary<string, decimal> _countyRates = new()
    {
        ["Los Angeles County"] = 0.0025m,
        ["Orange County"] = 0.0025m,
        ["San Diego County"] = 0.005m
    };

    private Dictionary<string, decimal> _cityRates = new()
    {
        ["Los Angeles"] = 0.0225m,
        ["San Francisco"] = 0.0175m,
        ["San Diego"] = 0.01m
    };

    private Dictionary<string, decimal> _categoryModifiers = new()
    {
        ["SOFTWARE"] = 0.01m,
        ["PHYSICAL_GOODS"] = 0.005m,
        ["DIGITAL_SERVICES"] = 0.015m
    };

    private readonly VatIT.Worker.Calculation.Services.RemoteRulesService _rulesService;

    public CalculateController(ILogger<CalculateController> logger, VatIT.Worker.Calculation.Services.RemoteRulesService rulesService)
    {
        _logger = logger;
        _rulesService = rulesService;
    }

    [HttpPost]
    public async Task<ActionResult<CalculationResponseDto>> Calculate([FromBody] CalculationRequestDto request)
    {
        _logger.LogInformation("Calculating fees for transaction {TransactionId}", request.TransactionId);

        var response = new CalculationResponseDto
        {
            TransactionId = request.TransactionId,
            Items = new List<ItemCalculationDto>(),
            ProcessedTimestamp = DateTime.UtcNow
        };

        var auditLogs = new List<string>();

        decimal totalFees = 0m;

        // Attempt to override rates from remote rules if available
        try
        {
            var latest = _rulesService.Latest;
            if (latest.HasValue)
            {
                if (latest.Value.TryGetProperty("stateRates", out var sr) && sr.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    var dict = new Dictionary<string, decimal>();
                    foreach (var p in sr.EnumerateObject()) if (p.Value.TryGetDecimal(out var d)) dict[p.Name] = d;
                    if (dict.Count>0) _stateRates = dict;
                }
                if (latest.Value.TryGetProperty("countyRates", out var cr) && cr.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    var dict = new Dictionary<string, decimal>();
                    foreach (var p in cr.EnumerateObject()) if (p.Value.TryGetDecimal(out var d)) dict[p.Name] = d;
                    if (dict.Count>0) _countyRates = dict;
                }
                if (latest.Value.TryGetProperty("cityRates", out var cir) && cir.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    var dict = new Dictionary<string, decimal>();
                    foreach (var p in cir.EnumerateObject()) if (p.Value.TryGetDecimal(out var d)) dict[p.Name] = d;
                    if (dict.Count>0) _cityRates = dict;
                }
                if (latest.Value.TryGetProperty("categoryModifiers", out var cm) && cm.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    var dict = new Dictionary<string, decimal>();
                    foreach (var p in cm.EnumerateObject()) if (p.Value.TryGetDecimal(out var d)) dict[p.Name] = d;
                    if (dict.Count>0) _categoryModifiers = dict;
                }
            }
        }
        catch { /* ignore remote parsing errors */ }

        // Calculate fees for each item
        foreach (var item in request.Items)
        {
            var itemCalc = new ItemCalculationDto
            {
                ItemId = item.Id,
                Amount = item.Amount,
                Category = item.Category,
                Fees = new FeesDto()
            };

            auditLogs.Add($"Processing item {item.Id} (${item.Amount}, {item.Category})");

            // State rate
            if (_stateRates.TryGetValue(request.State, out var stateRate))
            {
                var stateAmount = item.Amount * stateRate;
                itemCalc.Fees.StateRate = new RateInfoDto
                {
                    Jurisdiction = request.State,
                    Rate = stateRate,
                    Amount = Math.Round(stateAmount, 2)
                };
                auditLogs.Add($"  State rate ({request.State}): {stateRate:P2} = ${stateAmount:F2}");
            }

            // County rate
            if (!string.IsNullOrEmpty(request.County) && _countyRates.TryGetValue(request.County, out var countyRate))
            {
                var countyAmount = item.Amount * countyRate;
                itemCalc.Fees.CountyRate = new RateInfoDto
                {
                    Jurisdiction = request.County,
                    Rate = countyRate,
                    Amount = Math.Round(countyAmount, 2)
                };
                auditLogs.Add($"  County rate ({request.County}): {countyRate:P2} = ${countyAmount:F2}");
            }

            // City rate
            if (!string.IsNullOrEmpty(request.City) && _cityRates.TryGetValue(request.City, out var cityRate))
            {
                var cityAmount = item.Amount * cityRate;
                itemCalc.Fees.CityRate = new RateInfoDto
                {
                    Jurisdiction = request.City,
                    Rate = cityRate,
                    Amount = Math.Round(cityAmount, 2)
                };
                auditLogs.Add($"  City rate ({request.City}): {cityRate:P2} = ${cityAmount:F2}");
            }

            // Category modifier
            if (_categoryModifiers.TryGetValue(item.Category, out var categoryRate))
            {
                var categoryAmount = item.Amount * categoryRate;
                itemCalc.Fees.CategoryModifier = new RateInfoDto
                {
                    Jurisdiction = request.State,
                    Category = item.Category,
                    Rate = categoryRate,
                    Amount = Math.Round(categoryAmount, 2)
                };
                auditLogs.Add($"  Category modifier ({item.Category}): {categoryRate:P2} = ${categoryAmount:F2}");
            }

            // Calculate total fee for this item
            itemCalc.TotalFee = 
                (itemCalc.Fees.StateRate?.Amount ?? 0) +
                (itemCalc.Fees.CountyRate?.Amount ?? 0) +
                (itemCalc.Fees.CityRate?.Amount ?? 0) +
                (itemCalc.Fees.CategoryModifier?.Amount ?? 0);

            itemCalc.TotalFee = Math.Round(itemCalc.TotalFee, 2);
            totalFees += itemCalc.TotalFee;

            auditLogs.Add($"  Item total fee: ${itemCalc.TotalFee:F2}");

            response.Items.Add(itemCalc);
        }

        response.TotalFees = Math.Round(totalFees, 2);
        response.EffectiveRate = request.TotalAmount > 0 
            ? Math.Round(totalFees / request.TotalAmount, 4) 
            : 0;

        auditLogs.Add($"Total fees calculated: ${response.TotalFees:F2}");
        auditLogs.Add($"Effective rate: {response.EffectiveRate:P2}");

        response.AuditLogs = auditLogs;

        _logger.LogInformation(
            "Calculation completed for transaction {TransactionId}: Total fees ${TotalFees}", 
            request.TransactionId, 
            response.TotalFees);

        return Ok(response);
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", service = "calculation-worker", port = 8004 });
    }
}
