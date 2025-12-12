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
        [FromBody] TransactionRequest request, // json object to DTO model binding
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Processing transaction {TransactionId}", request.TransactionId);

            // Support a simple test hook: if MerchantId == "FORCEFAIL" then return business failure
            // This returns the canonical TransactionResponse shape with status FAILED so callers can parse consistently.
            if (string.Equals(request.MerchantId, "FORCEFAIL", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Transaction {TransactionId} forced to fail for testing (MerchantId=FORCEFAIL)", request.TransactionId);
                var forcedResponse = new VatIT.Domain.Entities.TransactionResponse
                {
                    TransactionId = request.TransactionId,
                    Status = "FAILED",
                    Gates = new List<VatIT.Domain.Entities.GateResult>
                    {
                        new VatIT.Domain.Entities.GateResult { Name = "FORCEFAIL", Passed = false, Message = "Forced failure by test flag" }
                    },
                    AuditTrail = new List<string> { "Forced failure by test flag" }
                };
                return StatusCode(422, forcedResponse);
            }

            var response = await _orchestrationService.ProcessTransactionAsync(request, cancellationToken);

            _logger.LogInformation(
                "Transaction {TransactionId} completed with status {Status}", 
                request.TransactionId, 
                response.Status);

            if (response.Status == "ERROR")
            {
                _logger.LogError("Transaction {TransactionId} failed during processing: {Details}", request.TransactionId, response.AuditTrail.LastOrDefault());
                // Return canonical TransactionResponse with status ERROR and audit trail for diagnostics
                return StatusCode(500, response);
            }

            // Business-level failure (e.g. validation/applicability/exemption failed).
            // Return 422 so clients and load-testers can distinguish HTTP success from business success.
            if (response.Status != "CALCULATED")
            {
                _logger.LogWarning("Transaction {TransactionId} completed with non-calculated status {Status}", request.TransactionId, response.Status);
                // Return canonical TransactionResponse (status will be FAILED) so clients can parse gates and auditTrail consistently
                return StatusCode(422, response);
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing transaction {TransactionId}", request.TransactionId);
            return StatusCode(500, new VatIT.Orchestrator.Api.Models.ErrorResponse
            {
                Error = "An error occurred processing the transaction",
                Code = "UNHANDLED_EXCEPTION",
                Details = new[] { ex.Message }
            });
        }
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", service = "orchestrator" });
    }
}
