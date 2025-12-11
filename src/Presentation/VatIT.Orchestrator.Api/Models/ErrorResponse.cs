using System.Collections.Generic;

namespace VatIT.Orchestrator.Api.Models;

public class ErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public string? Code { get; set; }
    public IEnumerable<string>? Details { get; set; }
    public object? Meta { get; set; }
}
