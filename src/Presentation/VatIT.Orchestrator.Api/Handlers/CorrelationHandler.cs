using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;

namespace VatIT.Orchestrator.Api.Handlers;

public class CorrelationHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private const string HeaderName = "X-Correlation-ID";

    public CorrelationHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var correlationId = _httpContextAccessor.HttpContext?.Request?.Headers[HeaderName].FirstOrDefault();
        if (!string.IsNullOrEmpty(correlationId))
        {
            // Add header if not already present
            if (!request.Headers.Contains(HeaderName))
            {
                request.Headers.Add(HeaderName, correlationId);
            }
        }

        return base.SendAsync(request, cancellationToken);
    }
}
