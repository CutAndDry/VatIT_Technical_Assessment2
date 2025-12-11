using VatIT.Domain.Entities;

namespace VatIT.Application.Interfaces;

public interface IOrchestrationService
{
    Task<TransactionResponse> ProcessTransactionAsync(TransactionRequest request, CancellationToken cancellationToken = default);
}
