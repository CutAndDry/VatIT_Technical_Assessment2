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

            // Support a simple test hook: if MerchantId == "FORCEFAIL" then return business failure
            if (string.Equals(request.MerchantId, "FORCEFAIL", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Transaction {TransactionId} forced to fail for testing (MerchantId=FORCEFAIL)", request.TransactionId);
                return StatusCode(422, new { error = "Transaction processing failed (forced)", status = "FAILED", details = new[] { "Forced failure by test flag" }, gates = new[] { new { name = "validation", passed = false, message = "Skipped (forced failure)" } } });
            }

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

            // Business-level failure (e.g. validation/applicability/exemption failed).
            // Return 422 so clients and load-testers can distinguish HTTP success from business success.
            if (response.Status != "CALCULATED")
            {
                _logger.LogWarning("Transaction {TransactionId} completed with non-calculated status {Status}", request.TransactionId, response.Status);
                return StatusCode(422, new { error = "Transaction processing failed", status = response.Status, details = response.AuditTrail, gates = response.Gates });
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
