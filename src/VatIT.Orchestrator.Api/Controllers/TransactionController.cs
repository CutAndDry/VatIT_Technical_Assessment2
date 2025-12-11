using Microsoft.AspNetCore.Mvc;
using VatIT.Application.Interfaces;
using VatIT.Domain.Entities;

namespace VatIT.Orchestrator.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TransactionController : ControllerBase
{
    private readonly IOrchestrationService _orchestrationService;
    private readonly ILogger<TransactionController> _logger;

    public TransactionController(
        IOrchestrationService orchestrationService,
        ILogger<TransactionController> logger)
    {
        _orchestrationService = orchestrationService;
        _logger = logger;
    }

    [HttpPost("process")]
    public async Task<ActionResult<TransactionResponse>> ProcessTransaction(
        [FromBody] TransactionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Processing transaction {TransactionId}", request.TransactionId);
            
            var response = await _orchestrationService.ProcessTransactionAsync(request, cancellationToken);

            _logger.LogInformation(
                "Transaction {TransactionId} completed with status {Status}", 
                request.TransactionId, 
                response.Status);

            if (response.Status == "ERROR")
            {
                _logger.LogError("Transaction {TransactionId} failed during processing: {Details}", request.TransactionId, response.AuditTrail.LastOrDefault());
                return StatusCode(500, new { error = "An error occurred processing the transaction", details = response.AuditTrail });
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing transaction {TransactionId}", request.TransactionId);
            return StatusCode(500, new { error = "An error occurred processing the transaction", details = ex.Message });
        }
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", service = "orchestrator" });
    }
}
