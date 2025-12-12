using VatIT.Domain.DTOs;

namespace VatIT.Worker.Exemption.Services;

public class ExemptionRuleEngine : IExemptionRuleEngine
{
    private readonly object _sync = new();
    private readonly HashSet<string> _exemptCustomers = new()
    {
        "customer_exempt_001",
        "customer_nonprofit_002"
    };

    private readonly Dictionary<string, List<string>> _categoryExemptions = new()
    {
        ["EDUCATION"] = new List<string> { "Educational materials exemption" },
        ["MEDICAL"] = new List<string> { "Medical supplies exemption" }
    };

    public Task<ExemptionResponseDto> EvaluateAsync(ExemptionRequestDto request)
    {
        var response = new ExemptionResponseDto
        {
            TransactionId = request.TransactionId,
            ProcessedTimestamp = DateTime.UtcNow,
            AppliedExemptions = new List<string>()
        };

        var auditLogs = new List<string>();

        if (_exemptCustomers.Contains(request.CustomerId))
        {
            response.AppliedExemptions.Add($"Customer exemption: {request.CustomerId}");
            auditLogs.Add($"Customer {request.CustomerId} has tax-exempt status");
        }
        else
        {
            auditLogs.Add($"Customer {request.CustomerId} does not have tax-exempt status");
        }

        foreach (var item in request.Items)
        {
            if (_categoryExemptions.TryGetValue(item.Category, out var exemptions))
            {
                response.AppliedExemptions.AddRange(exemptions);
                auditLogs.Add($"Item {item.Id} category {item.Category} has exemptions: {string.Join(", ", exemptions)}");
            }
            else
            {
                auditLogs.Add($"Item {item.Id} category {item.Category} has no exemptions");
            }
        }

        response.Passed = true;
        if (response.AppliedExemptions.Any())
        {
            response.Message = $"Applied {response.AppliedExemptions.Count} exemption(s)";
            auditLogs.Add($"Total exemptions applied: {response.AppliedExemptions.Count}");
        }
        else
        {
            response.Message = "No exemptions applied";
            auditLogs.Add("No exemptions applicable to this transaction");
        }

        response.AuditLogs = auditLogs;
        return Task.FromResult(response);
    }

    public Task ApplyRulesAsync(System.Text.Json.JsonElement rules)
    {
        lock (_sync)
        {
            try
            {
                if (rules.ValueKind != System.Text.Json.JsonValueKind.Object) return Task.CompletedTask;
                if (rules.TryGetProperty("exemptCustomers", out var arr) && arr.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    _exemptCustomers.Clear();
                    foreach (var el in arr.EnumerateArray()) if (el.ValueKind == System.Text.Json.JsonValueKind.String) _exemptCustomers.Add(el.GetString()!);
                }

                if (rules.TryGetProperty("categoryExemptions", out var cat) && cat.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    _categoryExemptions.Clear();
                    foreach (var p in cat.EnumerateObject())
                    {
                        var list = new List<string>();
                        if (p.Value.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            foreach (var v in p.Value.EnumerateArray()) if (v.ValueKind == System.Text.Json.JsonValueKind.String) list.Add(v.GetString()!);
                        }
                        _categoryExemptions[p.Name] = list;
                    }
                }
            }
            catch { }
        }
        return Task.CompletedTask;
    }
}
