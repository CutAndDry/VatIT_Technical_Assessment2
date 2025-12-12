using VatIT.Domain.DTOs;

namespace VatIT.Worker.Exemption.Services;

public interface IExemptionRuleEngine
{
    Task<ExemptionResponseDto> EvaluateAsync(ExemptionRequestDto request);
}
