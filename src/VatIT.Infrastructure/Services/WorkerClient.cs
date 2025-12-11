using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using VatIT.Application.Interfaces;
using VatIT.Domain.DTOs;
using VatIT.Infrastructure.Configuration;

namespace VatIT.Infrastructure.Services;

public class WorkerClient : IWorkerClient
{
    private readonly HttpClient _httpClient;
    private readonly WorkerEndpoints _endpoints;
    private readonly JsonSerializerOptions _jsonOptions;

    public WorkerClient(HttpClient httpClient, IOptions<WorkerEndpoints> endpoints)
    {
        _httpClient = httpClient;
        _endpoints = endpoints.Value;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task<ValidationResponseDto> SendValidationRequestAsync(
        ValidationRequestDto request, 
        CancellationToken cancellationToken = default)
    {
        var url = $"{_endpoints.ValidationWorkerUrl}/api/validate";
        return await SendRequestAsync<ValidationRequestDto, ValidationResponseDto>(url, request, cancellationToken);
    }

    public async Task<ApplicabilityResponseDto> SendApplicabilityRequestAsync(
        ApplicabilityRequestDto request, 
        CancellationToken cancellationToken = default)
    {
        var url = $"{_endpoints.ApplicabilityWorkerUrl}/api/applicability";
        return await SendRequestAsync<ApplicabilityRequestDto, ApplicabilityResponseDto>(url, request, cancellationToken);
    }

    public async Task<ExemptionResponseDto> SendExemptionRequestAsync(
        ExemptionRequestDto request, 
        CancellationToken cancellationToken = default)
    {
        var url = $"{_endpoints.ExemptionWorkerUrl}/api/exemption";
        return await SendRequestAsync<ExemptionRequestDto, ExemptionResponseDto>(url, request, cancellationToken);
    }

    public async Task<CalculationResponseDto> SendCalculationRequestAsync(
        CalculationRequestDto request, 
        CancellationToken cancellationToken = default)
    {
        var url = $"{_endpoints.CalculationWorkerUrl}/api/calculate";
        return await SendRequestAsync<CalculationRequestDto, CalculationResponseDto>(url, request, cancellationToken);
    }

    private async Task<TResponse> SendRequestAsync<TRequest, TResponse>(
        string url, 
        TRequest request, 
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(request, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        try
        {
            var response = await _httpClient.PostAsync(url, content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"Worker request to '{url}' failed with status {(int)response.StatusCode}: {response.ReasonPhrase}. Response body: {body}");
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<TResponse>(responseJson, _jsonOptions)
                ?? throw new InvalidOperationException("Failed to deserialize response");
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException($"Worker request to '{url}' timed out.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"HTTP request error while calling worker at '{url}': {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Unexpected error while calling worker at '{url}': {ex.Message}", ex);
        }
    }
}
