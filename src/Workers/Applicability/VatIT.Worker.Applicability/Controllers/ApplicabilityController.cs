using Microsoft.AspNetCore.Mvc;
using VatIT.Domain.DTOs;

namespace VatIT.Worker.Applicability.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ApplicabilityController : ControllerBase
{
    private readonly ILogger<ApplicabilityController> _logger;
    private readonly VatIT.Worker.Applicability.Services.IApplicabilityRuleEngine _ruleEngine;
    
    // Simulated merchant volume data (seeded with sample MER-1..MER-5 for benchmarks)
    private readonly Dictionary<string, Dictionary<string, decimal>> _merchantVolumes = new()
    {


        //replace this commented block with below data to create 100% pass for benchmarking

       /* ["MER-1"] = new Dictionary<string, decimal>
        {
            ["CA"] = 200000m,
            ["NY"] = 600000m,
            ["TX"] = 300000m
        },
        ["MER-2"] = new Dictionary<string, decimal>
        {
            ["CA"] = 150000m,
            ["NY"] = 700000m,
            ["TX"] = 350000m
        */

        ["merchant_456"] = new Dictionary<string, decimal>
        {
            ["CA"] = 2300000m,
            ["NY"] = 500000m,
            ["TX"] = 150000m
        },
        // MER-1 and MER-2 intentionally have no volume (will fail applicability) results in 40% fail rate for benchmarks
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

    public ApplicabilityController(ILogger<ApplicabilityController> logger, VatIT.Worker.Applicability.Services.IApplicabilityRuleEngine ruleEngine)
    {
        _logger = logger;
        _ruleEngine = ruleEngine;
    }

    [HttpPost]
    public async Task<ActionResult<ApplicabilityResponseDto>> CheckApplicability([FromBody] ApplicabilityRequestDto request)
    {
        _logger.LogInformation("Checking applicability for transaction {TransactionId}", request.TransactionId);

        var response = await _ruleEngine.EvaluateAsync(request);

        _logger.LogInformation(
            "Applicability check completed for transaction {TransactionId}: {Result}", 
            request.TransactionId, 
            response.IsApplicable);

        return Ok(response);
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", service = "applicability-worker", port = 8002 });
    }
}
