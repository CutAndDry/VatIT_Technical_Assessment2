using VatIT.Application.Interfaces;
using VatIT.Domain.DTOs;
using VatIT.Domain.Entities;

namespace VatIT.Application.Services;

public class OrchestrationService : IOrchestrationService
{
    private readonly IWorkerClient _workerClient;
    private readonly Dictionary<string, object> _responseCache = new();

    public OrchestrationService(IWorkerClient workerClient)
    {
        _workerClient = workerClient;
    }

    public async Task<TransactionResponse> ProcessTransactionAsync(
        TransactionRequest request, 
        CancellationToken cancellationToken = default)
    {
        var response = new TransactionResponse
        {
            TransactionId = request.TransactionId,
            Status = "PROCESSING"
        };

        try
        {
            // Gate 1: Validation
            var validationRequest = MapToValidationRequest(request);
            var swValidation = System.Diagnostics.Stopwatch.StartNew();
            var validationResponse = await _workerClient.SendValidationRequestAsync(validationRequest, cancellationToken);
            if (validationResponse == null)
            {
                validationResponse = new VatIT.Domain.DTOs.ValidationResponseDto
                {
                    TransactionId = request.TransactionId,
                    IsValid = false,
                    Message = "No validation response",
                    AuditLogs = new List<string> { "Validation worker returned no response" }
                };
            }
            swValidation.Stop();
            
            _responseCache[$"{request.TransactionId}_validation"] = validationResponse;
            response.Gates.Add(new GateResult
            {
                Name = validationResponse.GateName,
                Passed = validationResponse.IsValid,
                Message = validationResponse.Message
            });
            response.AuditTrail.AddRange(validationResponse.AuditLogs);

            // record timing
            response.WorkerTimings ??= new Dictionary<string, double>();
            response.WorkerTimings["validation"] = swValidation.Elapsed.TotalMilliseconds;
            if (!validationResponse.IsValid)
            {
                response.Status = "FAILED";

                // Mark remaining gates as skipped for the response so callers know what didn't run
                response.Gates.Add(new GateResult { Name = "APPLICABILITY", Passed = false, Message = "Skipped due to failed validation" });
                response.Gates.Add(new GateResult { Name = "EXEMPTION_CHECK", Passed = false, Message = "Skipped due to failed validation" });
                response.Gates.Add(new GateResult { Name = "CALCULATION", Passed = false, Message = "Skipped due to failed validation" });

                response.WorkerTimings["applicability"] = 0;
                response.WorkerTimings["exemption"] = 0;
                response.WorkerTimings["calculation"] = 0;

                return response;
            }

            // Gate 2: Applicability
            var applicabilityRequest = MapToApplicabilityRequest(request);
            var swApplicability = System.Diagnostics.Stopwatch.StartNew();
            var applicabilityResponse = await _workerClient.SendApplicabilityRequestAsync(applicabilityRequest, cancellationToken);
            if (applicabilityResponse == null)
            {
                applicabilityResponse = new VatIT.Domain.DTOs.ApplicabilityResponseDto
                {
                    TransactionId = request.TransactionId,
                    IsApplicable = false,
                    Message = "No applicability response",
                    AuditLogs = new List<string> { "Applicability worker returned no response" }
                };
            }
            swApplicability.Stop();
            
            _responseCache[$"{request.TransactionId}_applicability"] = applicabilityResponse;
            response.Gates.Add(new GateResult
            {
                Name = applicabilityResponse.GateName,
                Passed = applicabilityResponse.IsApplicable,
                Message = applicabilityResponse.Message
            });
            response.AuditTrail.AddRange(applicabilityResponse.AuditLogs);

            // record timing
            response.WorkerTimings["applicability"] = swApplicability.Elapsed.TotalMilliseconds;
            if (!applicabilityResponse.IsApplicable)
            {
                response.Status = "FAILED";

                // Mark remaining gates as skipped
                response.Gates.Add(new GateResult { Name = "EXEMPTION_CHECK", Passed = false, Message = "Skipped due to failed applicability" });
                response.Gates.Add(new GateResult { Name = "CALCULATION", Passed = false, Message = "Skipped due to failed applicability" });

                response.WorkerTimings["exemption"] = 0;
                response.WorkerTimings["calculation"] = 0;

                return response;
            }

            // Gate 3: Exemption Check
            var exemptionRequest = MapToExemptionRequest(request);
            var swExemption = System.Diagnostics.Stopwatch.StartNew();
            var exemptionResponse = await _workerClient.SendExemptionRequestAsync(exemptionRequest, cancellationToken);
            if (exemptionResponse == null)
            {
                exemptionResponse = new VatIT.Domain.DTOs.ExemptionResponseDto
                {
                    TransactionId = request.TransactionId,
                    Passed = false,
                    AppliedExemptions = new List<string>(),
                    Message = "No exemption response",
                    AuditLogs = new List<string> { "Exemption worker returned no response" }
                };
            }
            swExemption.Stop();
            
            _responseCache[$"{request.TransactionId}_exemption"] = exemptionResponse;
            response.Gates.Add(new GateResult
            {
                Name = exemptionResponse.GateName,
                Passed = exemptionResponse.Passed,
                Message = exemptionResponse.Message,
                AppliedExemptions = exemptionResponse.AppliedExemptions
            });
            response.AuditTrail.AddRange(exemptionResponse.AuditLogs);

            // record timing
            response.WorkerTimings["exemption"] = swExemption.Elapsed.TotalMilliseconds;
            if (!exemptionResponse.Passed)
            {
                response.Status = "FAILED";

                // Mark calculation as skipped
                response.Gates.Add(new GateResult { Name = "CALCULATION", Passed = false, Message = "Skipped due to failed exemption" });
                response.WorkerTimings["calculation"] = 0;

                return response;
            }

            // Gate 4: Calculation
            var calculationRequest = MapToCalculationRequest(request);
            var swCalculation = System.Diagnostics.Stopwatch.StartNew();
            var calculationResponse = await _workerClient.SendCalculationRequestAsync(calculationRequest, cancellationToken);
            if (calculationResponse == null)
            {
                calculationResponse = new VatIT.Domain.DTOs.CalculationResponseDto
                {
                    TransactionId = request.TransactionId,
                    TotalFees = 0m,
                    EffectiveRate = 0m,
                    Items = new List<VatIT.Domain.DTOs.ItemCalculationDto>(),
                    AuditLogs = new List<string> { "Calculation worker returned no response" }
                };
            }
            swCalculation.Stop();
            
            _responseCache[$"{request.TransactionId}_calculation"] = calculationResponse;
            response.Calculation = MapToCalculationResult(calculationResponse);
            response.AuditTrail.AddRange(calculationResponse.AuditLogs);

            // record timing
            response.WorkerTimings["calculation"] = swCalculation.Elapsed.TotalMilliseconds;

            // If any gate set the status to FAILED earlier, keep it; otherwise mark as CALCULATED
            if (response.Status == "FAILED")
            {
                return response;
            }

            response.Status = "CALCULATED";
            return response;
        }
        catch (Exception ex)
        {
            // Capture exception details in the response audit trail and return a failed response
            response.Status = "ERROR";
            response.AuditTrail.Add($"Error during processing: {ex.Message}");
            response.AuditTrail.Add(ex.ToString());
            return response;
        }
    }

    private ValidationRequestDto MapToValidationRequest(TransactionRequest request)
    {
        return new ValidationRequestDto
        {
            TransactionId = request.TransactionId,
            Country = request.Destination.Country,
            State = request.Destination.State,
            City = request.Destination.City,
            RequestTimestamp = DateTime.UtcNow
        };
    }

    private ApplicabilityRequestDto MapToApplicabilityRequest(TransactionRequest request)
    {
        return new ApplicabilityRequestDto
        {
            TransactionId = request.TransactionId,
            MerchantId = request.MerchantId,
            State = request.Destination.State,
            TotalAmount = request.TotalAmount,
            RequestTimestamp = DateTime.UtcNow
        };
    }

    private ExemptionRequestDto MapToExemptionRequest(TransactionRequest request)
    {
        return new ExemptionRequestDto
        {
            TransactionId = request.TransactionId,
            CustomerId = request.CustomerId,
            MerchantId = request.MerchantId,
            Items = request.Items.Select(i => new ItemDto
            {
                Id = i.Id,
                Category = i.Category,
                Amount = i.Amount
            }).ToList(),
            RequestTimestamp = DateTime.UtcNow
        };
    }

    private CalculationRequestDto MapToCalculationRequest(TransactionRequest request)
    {
        return new CalculationRequestDto
        {
            TransactionId = request.TransactionId,
            State = request.Destination.State,
            County = "Los Angeles County", // Derived from city
            City = request.Destination.City,
            Items = request.Items.Select(i => new ItemDto
            {
                Id = i.Id,
                Category = i.Category,
                Amount = i.Amount
            }).ToList(),
            TotalAmount = request.TotalAmount,
            RequestTimestamp = DateTime.UtcNow
        };
    }

    private CalculationResult MapToCalculationResult(CalculationResponseDto dto)
    {
        return new CalculationResult
        {
            Items = dto.Items.Select(i => new ItemCalculation
            {
                ItemId = i.ItemId,
                Amount = i.Amount,
                Category = i.Category,
                Fees = new Fees
                {
                    StateRate = i.Fees.StateRate != null ? new RateInfo
                    {
                        Jurisdiction = i.Fees.StateRate.Jurisdiction,
                        Rate = i.Fees.StateRate.Rate,
                        Amount = i.Fees.StateRate.Amount
                    } : null,
                    CountyRate = i.Fees.CountyRate != null ? new RateInfo
                    {
                        Jurisdiction = i.Fees.CountyRate.Jurisdiction,
                        Rate = i.Fees.CountyRate.Rate,
                        Amount = i.Fees.CountyRate.Amount
                    } : null,
                    CityRate = i.Fees.CityRate != null ? new RateInfo
                    {
                        Jurisdiction = i.Fees.CityRate.Jurisdiction,
                        Rate = i.Fees.CityRate.Rate,
                        Amount = i.Fees.CityRate.Amount
                    } : null,
                    CategoryModifier = i.Fees.CategoryModifier != null ? new RateInfo
                    {
                        Jurisdiction = i.Fees.CategoryModifier.Jurisdiction,
                        Category = i.Fees.CategoryModifier.Category,
                        Rate = i.Fees.CategoryModifier.Rate,
                        Amount = i.Fees.CategoryModifier.Amount
                    } : null
                },
                TotalFee = i.TotalFee
            }).ToList(),
            TotalFees = dto.TotalFees,
            EffectiveRate = dto.EffectiveRate
        };
    }
}
