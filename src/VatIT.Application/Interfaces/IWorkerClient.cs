using VatIT.Domain.DTOs;

namespace VatIT.Application.Interfaces;

public interface IWorkerClient
{
    Task<ValidationResponseDto> SendValidationRequestAsync(ValidationRequestDto request, CancellationToken cancellationToken = default);
    Task<ApplicabilityResponseDto> SendApplicabilityRequestAsync(ApplicabilityRequestDto request, CancellationToken cancellationToken = default);
    Task<ExemptionResponseDto> SendExemptionRequestAsync(ExemptionRequestDto request, CancellationToken cancellationToken = default);
    Task<CalculationResponseDto> SendCalculationRequestAsync(CalculationRequestDto request, CancellationToken cancellationToken = default);
}
