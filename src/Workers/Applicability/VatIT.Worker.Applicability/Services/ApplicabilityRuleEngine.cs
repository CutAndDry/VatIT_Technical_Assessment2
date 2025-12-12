using VatIT.Domain.DTOs;

namespace VatIT.Worker.Applicability.Services;

public class ApplicabilityRuleEngine : IApplicabilityRuleEngine
{
    private readonly object _sync = new();
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

    public Task ApplyRulesAsync(System.Text.Json.JsonElement rules)
    {
        // rules expected shape: { thresholds: { STATE: number, ... }, merchantVolumes: { merchantId: { STATE: number } } }
        lock (_sync)
        {
            try
            {
                if (rules.ValueKind != System.Text.Json.JsonValueKind.Object) return Task.CompletedTask;
                if (rules.TryGetProperty("thresholds", out var thrObj) && thrObj.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    var newThr = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
                    foreach (var p in thrObj.EnumerateObject())
                    {
                        if (p.Value.TryGetDecimal(out var d)) newThr[p.Name] = d;
                    }
                    if (newThr.Count > 0)
                    {
                        _stateThresholds.Clear();
                        foreach (var kv in newThr) _stateThresholds[kv.Key] = kv.Value;
                    }
                }

                if (rules.TryGetProperty("merchantVolumes", out var mv) && mv.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    var newM = new Dictionary<string, Dictionary<string, decimal>>(StringComparer.OrdinalIgnoreCase);
                    foreach (var m in mv.EnumerateObject())
                    {
                        if (m.Value.ValueKind != System.Text.Json.JsonValueKind.Object) continue;
                        var stateMap = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
                        foreach (var s in m.Value.EnumerateObject())
                        {
                            if (s.Value.TryGetDecimal(out var d)) stateMap[s.Name] = d;
                        }
                        newM[m.Name] = stateMap;
                    }
                    if (newM.Count > 0)
                    {
                        _merchantVolumes.Clear();
                        foreach (var kv in newM) _merchantVolumes[kv.Key] = kv.Value;
                    }
                }
            }
            catch { /* ignore */ }
        }
        return Task.CompletedTask;
    }
}
