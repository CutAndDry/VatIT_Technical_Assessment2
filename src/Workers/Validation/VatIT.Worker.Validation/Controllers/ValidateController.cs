using Microsoft.AspNetCore.Mvc;
using VatIT.Domain.DTOs;

namespace VatIT.Worker.Validation.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ValidateController : ControllerBase
{
    private readonly ILogger<ValidateController> _logger;
    private HashSet<string> _validStates = new()
    {
        "CA", "NY", "TX", "FL", "IL", "PA", "OH", "GA", "NC", "MI"
    };

    private Dictionary<string, HashSet<string>> _validCities = new()
    {
        ["CA"] = new HashSet<string> { "Los Angeles", "San Francisco", "San Diego", "Sacramento" },
        ["NY"] = new HashSet<string> { "New York", "Buffalo", "Rochester", "Albany" },
        ["TX"] = new HashSet<string> { "Houston", "Dallas", "Austin", "San Antonio" }
    };

    private readonly VatIT.Worker.Validation.Services.RemoteRulesService _rulesService;

    public ValidateController(ILogger<ValidateController> logger, VatIT.Worker.Validation.Services.RemoteRulesService rulesService)
    {
        _logger = logger;
        _rulesService = rulesService;
    }

    [HttpPost]
    public ActionResult<ValidationResponseDto> Validate([FromBody] ValidationRequestDto request)
    {
        _logger.LogInformation("Validating address for transaction {TransactionId}", request.TransactionId);

        var response = new ValidationResponseDto
        {
            TransactionId = request.TransactionId,
            ProcessedTimestamp = DateTime.UtcNow
        };

        var auditLogs = new List<string>();

        // Validate country
        if (string.IsNullOrEmpty(request.Country) || request.Country.ToUpper() != "US")
        {
            response.IsValid = false;
            response.Message = "Invalid country. Only US addresses are supported.";
            auditLogs.Add($"Address validation failed: Invalid country '{request.Country}'");
            response.AuditLogs = auditLogs;
            return Ok(response);
        }

        auditLogs.Add("Country validation passed: US");

        // Attempt to override validation lists from remote rules
        try
        {
            var latest = _rulesService.Latest;
            if (latest.HasValue)
            {
                if (latest.Value.TryGetProperty("validStates", out var vs) && vs.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    _validStates = new HashSet<string>(vs.EnumerateArray().Select(x => x.GetString() ?? string.Empty));
                }
                if (latest.Value.TryGetProperty("validCities", out var vc) && vc.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    var dict = new Dictionary<string, HashSet<string>>();
                    foreach (var p in vc.EnumerateObject())
                    {
                        if (p.Value.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            dict[p.Name] = new HashSet<string>(p.Value.EnumerateArray().Select(x => x.GetString() ?? string.Empty));
                        }
                    }
                    if (dict.Count>0) _validCities = dict;
                }
            }
        }
        catch { /* ignore */ }

        // Validate state
        if (!_validStates.Contains(request.State))
        {
            response.IsValid = false;
            response.Message = $"Invalid state: {request.State}";
            auditLogs.Add($"Address validation failed: Invalid state '{request.State}'");
            response.AuditLogs = auditLogs;
            return Ok(response);
        }

        auditLogs.Add($"State validation passed: {request.State}");

        // Validate city (if configured for the state)
        if (_validCities.TryGetValue(request.State, out var cities))
        {
            if (!cities.Contains(request.City))
            {
                response.IsValid = false;
                response.Message = $"Invalid city: {request.City} for state {request.State}";
                auditLogs.Add($"Address validation failed: Invalid city '{request.City}' for state '{request.State}'");
                response.AuditLogs = auditLogs;
                return Ok(response);
            }

            auditLogs.Add($"City validation passed: {request.City}");
        }

        // Simulate cache lookup
        auditLogs.Add("Address validated via cache");

        response.IsValid = true;
        response.Message = $"Valid {request.Country} address";
        response.AuditLogs = auditLogs;

        _logger.LogInformation("Address validation passed for transaction {TransactionId}", request.TransactionId);

        return Ok(response);
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", service = "validation-worker", port = 8001 });
    }
}
