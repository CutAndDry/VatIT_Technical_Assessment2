using Microsoft.AspNetCore.Mvc;
using VatIT.Domain.DTOs;

namespace VatIT.Worker.Exemption.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExemptionController : ControllerBase
{
    private readonly ILogger<ExemptionController> _logger;
    private readonly VatIT.Worker.Exemption.Services.IExemptionRuleEngine _ruleEngine;

    public ExemptionController(ILogger<ExemptionController> logger, VatIT.Worker.Exemption.Services.IExemptionRuleEngine ruleEngine)
    {
        _logger = logger;
        _ruleEngine = ruleEngine;
    }

    [HttpPost]
    public async Task<ActionResult<ExemptionResponseDto>> CheckExemption([FromBody] ExemptionRequestDto request)
    {
        _logger.LogInformation("Checking exemptions for transaction {TransactionId}", request.TransactionId);

        var response = await _ruleEngine.EvaluateAsync(request);

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
