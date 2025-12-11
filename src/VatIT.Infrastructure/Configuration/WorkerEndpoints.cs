namespace VatIT.Infrastructure.Configuration;

public class WorkerEndpoints
{
    public string ValidationWorkerUrl { get; set; } = "http://localhost:8001";
    public string ApplicabilityWorkerUrl { get; set; } = "http://localhost:8002";
    public string ExemptionWorkerUrl { get; set; } = "http://localhost:8003";
    public string CalculationWorkerUrl { get; set; } = "http://localhost:8004";
}
