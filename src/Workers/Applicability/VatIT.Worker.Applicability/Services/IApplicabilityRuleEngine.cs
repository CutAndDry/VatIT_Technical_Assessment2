using VatIT.Domain.DTOs;

namespace VatIT.Worker.Applicability.Services;

public interface IApplicabilityRuleEngine
{
    Task<ApplicabilityResponseDto> EvaluateAsync(ApplicabilityRequestDto request);
    Task ApplyRulesAsync(System.Text.Json.JsonElement rules);
}
