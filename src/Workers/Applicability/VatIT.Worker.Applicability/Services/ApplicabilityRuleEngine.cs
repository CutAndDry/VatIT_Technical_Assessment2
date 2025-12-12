using VatIT.Domain.DTOs;

namespace VatIT.Worker.Applicability.Services;

public class ApplicabilityRuleEngine : IApplicabilityRuleEngine
{
    // Seeded data mirrors previous controller logic
    private readonly Dictionary<string, Dictionary<string, decimal>> _merchantVolumes = new()
    {
        ["merchant_456"] = new Dictionary<string, decimal>
        {
            ["CA"] = 2300000m,
            ["NY"] = 500000m,
            ["TX"] = 150000m
        },
        ["MER-1"] = new Dictionary<string, decimal>
        {
            ["CA"] = 0m,
            ["NY"] = 0m,
            ["TX"] = 0m
        },
        ["MER-2"] = new Dictionary<string, decimal>
        {
            ["CA"] = 0m,
            ["NY"] = 0m,
            ["TX"] = 0m
        },
        ["MER-3"] = new Dictionary<string, decimal>
        {
            ["CA"] = 300000m,
            ["NY"] = 800000m,
            ["TX"] = 400000m
        },
        ["MER-4"] = new Dictionary<string, decimal>
        {
            ["CA"] = 120000m,
            ["NY"] = 550000m,
            ["TX"] = 260000m
        },
        ["MER-5"] = new Dictionary<string, decimal>
        {
            ["CA"] = 500000m,
            ["NY"] = 900000m,
            ["TX"] = 600000m
        }
    };

    private readonly Dictionary<string, decimal> _stateThresholds = new()
    {
        ["CA"] = 100000m,
        ["NY"] = 500000m,
        ["TX"] = 250000m,
        ["FL"] = 100000m
    };

    public Task<ApplicabilityResponseDto> EvaluateAsync(ApplicabilityRequestDto request)
    {
        var response = new ApplicabilityResponseDto
        {
            TransactionId = request.TransactionId,
            ProcessedTimestamp = DateTime.UtcNow
        };

        var auditLogs = new List<string>();

        // Threshold
        if (!_stateThresholds.TryGetValue(request.State, out var threshold))
        {
            threshold = 100000m;
            auditLogs.Add($"Using default threshold for state {request.State}: ${threshold:N0}");
        }
        else
        {
            auditLogs.Add($"State threshold for {request.State}: ${threshold:N0}");
        }

        response.Threshold = threshold;

        decimal merchantVolume = 0;
        if (_merchantVolumes.TryGetValue(request.MerchantId, out var stateVolumes))
        {
            if (stateVolumes.TryGetValue(request.State, out merchantVolume))
            {
                auditLogs.Add($"Retrieved merchant volume for {request.MerchantId} in {request.State}: ${merchantVolume:N0}");
            }
            else
            {
                auditLogs.Add($"No volume data found for merchant {request.MerchantId} in state {request.State}");
            }
        }
        else
        {
            auditLogs.Add($"Merchant {request.MerchantId} not found in volume database");
        }

        response.MerchantVolume = merchantVolume;

        if (merchantVolume >= threshold)
        {
            response.IsApplicable = true;
            response.Message = $"Merchant above ${threshold:N0} threshold in {request.State}";
            auditLogs.Add($"Applicability check passed: Merchant volume ${merchantVolume:N0} >= threshold ${threshold:N0}");
        }
        else
        {
            response.IsApplicable = false;
            response.Message = $"Merchant below ${threshold:N0} threshold in {request.State}";
            auditLogs.Add($"Applicability check failed: Merchant volume ${merchantVolume:N0} < threshold ${threshold:N0}");
        }

        response.AuditLogs = auditLogs;
        return Task.FromResult(response);
    }
}
