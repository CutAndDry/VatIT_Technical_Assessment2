using Microsoft.AspNetCore.Mvc;
using VatIT.Domain.DTOs;

namespace VatIT.Worker.Exemption.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExemptionController : ControllerBase
{
    private readonly ILogger<ExemptionController> _logger;

    // Simulated exemption data
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

    public ExemptionController(ILogger<ExemptionController> logger)
    {
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<ExemptionResponseDto>> CheckExemption([FromBody] ExemptionRequestDto request)
    {
        _logger.LogInformation("Checking exemptions for transaction {TransactionId}", request.TransactionId);

        var response = new ExemptionResponseDto
        {
            TransactionId = request.TransactionId,
            ProcessedTimestamp = DateTime.UtcNow,
            AppliedExemptions = new List<string>()
        };

        var auditLogs = new List<string>();

        // Check customer-level exemptions
        if (_exemptCustomers.Contains(request.CustomerId))
        {
            response.AppliedExemptions.Add($"Customer exemption: {request.CustomerId}");
            auditLogs.Add($"Customer {request.CustomerId} has tax-exempt status");
        }
        else
        {
            auditLogs.Add($"Customer {request.CustomerId} does not have tax-exempt status");
        }

        // Check category-level exemptions
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

        // Exemption check passes even if no exemptions found (unlike validation/applicability)
        // This gate only fails if there's a system error or invalid data
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

        _logger.LogInformation(
            "Exemption check completed for transaction {TransactionId}: {ExemptionsCount} exemptions applied", 
            request.TransactionId, 
            response.AppliedExemptions.Count);

        return Ok(response);
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", service = "exemption-worker", port = 8003 });
    }
}
