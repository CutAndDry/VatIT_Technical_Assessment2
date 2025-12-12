# Visual System Diagram

Last updated: 2025-12-12

Changelog:
- 2025-12-12: Diagram notes updated to indicate where client correlation IDs and benchmarking controls (bulkhead / MaxConnections) are applied.

## Complete System Overview

```
┌──────────────────────────────────────────────────────────────────────┐
│                            CLIENT                                     │
│                      (Postman / curl / etc.)                         │
└────────────────────────────┬─────────────────────────────────────────┘
                             │
                             │ POST /api/transaction/process
                             │ {transactionId, merchantId, items...}
                             │
                             ▼
┌────────────────────────────────────────────────────────────────────────┐
│                     ORCHESTRATOR API (Port 5000)                       │
│                                                                        │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │                  TransactionController                        │   │
│  │  • Receives JSON request                                      │   │
│  │  • Validates input                                            │   │
│  │  • Calls OrchestrationService                                 │   │
│  └───────────────────────┬──────────────────────────────────────┘   │
│                          │                                            │
│  ┌───────────────────────▼──────────────────────────────────────┐   │
│  │              OrchestrationService                             │   │
│  │  ┌────────────────────────────────────────────────────────┐  │   │
│  │  │ 1. Split request → ValidationRequestDto                │  │   │
│  │  │ 2. Call Validation Worker (8001)                       │  │   │
│  │  │ 3. Store response in memory                            │  │   │
│  │  │ 4. Check if passed → if not, STOP & return FAILED     │  │   │
│  │  └────────────────────────────────────────────────────────┘  │   │
│  │  ┌────────────────────────────────────────────────────────┐  │   │
│  │  │ 5. Split request → ApplicabilityRequestDto             │  │   │
│  │  │ 6. Call Applicability Worker (8002)                    │  │   │
│  │  │ 7. Store response in memory                            │  │   │
│  │  │ 8. Check if passed → if not, STOP & return FAILED     │  │   │
│  │  └────────────────────────────────────────────────────────┘  │   │
│  │  ┌────────────────────────────────────────────────────────┐  │   │
│  │  │ 9. Split request → ExemptionRequestDto                 │  │   │
│  │  │ 10. Call Exemption Worker (8003)                       │  │   │
│  │  │ 11. Store response in memory                           │  │   │
│  │  │ 12. Check if passed → if not, STOP & return FAILED    │  │   │
│  │  └────────────────────────────────────────────────────────┘  │   │
│  │  ┌────────────────────────────────────────────────────────┐  │   │
│  │  │ 13. Split request → CalculationRequestDto              │  │   │
│  │  │ 14. Call Calculation Worker (8004)                     │  │   │
│  │  │ 15. Store response in memory                           │  │   │
│  │  │ 16. Assemble final response                            │  │   │
│  │  │ 17. Return CALCULATED status                           │  │   │
│  │  └────────────────────────────────────────────────────────┘  │   │
│  └──────────────────────────────────────────────────────────────┘   │
│                          │                                            │
│  ┌───────────────────────▼──────────────────────────────────────┐   │
│  │                    WorkerClient                               │   │
│  │  • HttpClient for async HTTP calls                            │   │
│  │  • JSON serialization/deserialization                         │   │
│  │  • Configurable worker URLs                                   │   │
│  └───┬──────────┬──────────────┬──────────────┬─────────────────┘   │
└──────┼──────────┼──────────────┼──────────────┼─────────────────────┘
       │          │              │              │
       │ HTTP     │ HTTP         │ HTTP         │ HTTP
       │ POST     │ POST         │ POST         │ POST
       │          │              │              │
       ▼          ▼              ▼              ▼
   ┌────────┐ ┌──────────┐ ┌───────────┐ ┌──────────────┐
   │ GATE 1 │ │  GATE 2  │ │  GATE 3   │ │   GATE 4     │
   │Validate│ │Applicabi-│ │ Exemption │ │ Calculation  │
   │        │ │lity      │ │           │ │              │
   │Port    │ │Port      │ │Port       │ │Port          │
   │8001    │ │8002      │ │8003       │ │8004          │
   └────────┘ └──────────┘ └───────────┘ └──────────────┘
       │          │              │              │
       ▼          ▼              ▼              ▼
```

## Data Flow Sequence

```
┌─────────┐                                    ┌─────────────┐
│         │ 1. POST TransactionRequest        │             │
│ Client  │─────────────────────────────────▶│Orchestrator │
│         │                                    │             │
└─────────┘                                    └──────┬──────┘
                                                      │
                                                      │ 2. Map to ValidationRequestDto
                                                      │
                                               ┌──────▼──────┐
                                               │ Validation  │
                                               │   Worker    │
                                               │  (8001)     │
                                               └──────┬──────┘
                                                      │
                                                      │ 3. ValidationResponseDto
                                                      │
                                               ┌──────▼──────┐
                                               │Orchestrator │
                                               │ (Check if   │
                                               │  passed)    │
                                               └──────┬──────┘
                                                      │
                                                      │ If passed: 4. Map to ApplicabilityRequestDto
                                                      │ If failed: Return FAILED
                                                      │
                                               ┌──────▼──────┐
                                               │Applicability│
                                               │   Worker    │
                                               │  (8002)     │
                                               └──────┬──────┘
                                                      │
                                                      │ 5. ApplicabilityResponseDto
                                                      │
                                               ┌──────▼──────┐
                                               │Orchestrator │
                                               │ (Check if   │
                                               │  passed)    │
                                               └──────┬──────┘
                                                      │
                                                      │ If passed: 6. Map to ExemptionRequestDto
                                                      │ If failed: Return FAILED
                                                      │
                                               ┌──────▼──────┐
                                               │ Exemption   │
                                               │   Worker    │
                                               │  (8003)     │
                                               └──────┬──────┘
                                                      │
                                                      │ 7. ExemptionResponseDto
                                                      │
                                               ┌──────▼──────┐
                                               │Orchestrator │
                                               │ (Check if   │
                                               │  passed)    │
                                               └──────┬──────┘
                                                      │
                                                      │ If passed: 8. Map to CalculationRequestDto
                                                      │ If failed: Return FAILED
                                                      │
                                               ┌──────▼──────┐
                                               │Calculation  │
                                               │   Worker    │
                                               │  (8004)     │
                                               └──────┬──────┘
                                                      │
                                                      │ 9. CalculationResponseDto
                                                      │
                                               ┌──────▼──────┐
                                               │Orchestrator │
                                               │ (Assemble   │
                                               │  response)  │
                                               └──────┬──────┘
                                                      │
┌─────────┐                                    ┌─────▼───────┐
│         │ 10. TransactionResponse           │             │
│ Client  │◀─────────────────────────────────│Orchestrator │
│         │    (CALCULATED or FAILED)         │             │
└─────────┘                                    └─────────────┘
```

## Clean Architecture Layers

```
┌───────────────────────────────────────────────────────────────┐
│                    PRESENTATION LAYER                         │
│                                                               │
│  ┌─────────────────────────────────────────────────────┐    │
│  │        VatIT.Orchestrator.Api                       │    │
│  │  • TransactionController                             │    │
│  │  • Program.cs (DI setup)                             │    │
│  │  • appsettings.json                                  │    │
│  └─────────────────────────────────────────────────────┘    │
└──────────────────────────┬────────────────────────────────────┘
                           │
                           │ depends on
                           ▼
┌───────────────────────────────────────────────────────────────┐
│                   APPLICATION LAYER                           │
│                                                               │
│  ┌─────────────────────────────────────────────────────┐    │
│  │        VatIT.Application                             │    │
│  │  • IOrchestrationService (interface)                 │    │
│  │  • OrchestrationService (implementation)             │    │
│  │  • IWorkerClient (interface)                         │    │
│  └─────────────────────────────────────────────────────┘    │
└──────────────────────────┬────────────────────────────────────┘
                           │
                           │ depends on
                           ▼
┌───────────────────────────────────────────────────────────────┐
│                  INFRASTRUCTURE LAYER                         │
│                                                               │
│  ┌─────────────────────────────────────────────────────┐    │
│  │        VatIT.Infrastructure                          │    │
│  │  • WorkerClient (HTTP implementation)                │    │
│  │  • WorkerEndpoints (configuration)                   │    │
│  └─────────────────────────────────────────────────────┘    │
└──────────────────────────┬────────────────────────────────────┘
                           │
                           │ depends on
                           ▼
┌───────────────────────────────────────────────────────────────┐
│                      DOMAIN LAYER                             │
│                    (No dependencies)                          │
│  ┌─────────────────────────────────────────────────────┐    │
│  │        VatIT.Domain                                  │    │
│  │  • TransactionRequest (entity)                       │    │
│  │  • TransactionResponse (entity)                      │    │
│  │  • ValidationRequestDto, ValidationResponseDto       │    │
│  │  • ApplicabilityRequestDto, ApplicabilityResponseDto │    │
│  │  • ExemptionRequestDto, ExemptionResponseDto         │    │
│  │  • CalculationRequestDto, CalculationResponseDto     │    │
│  └─────────────────────────────────────────────────────┘    │
└───────────────────────────────────────────────────────────────┘
```

## Memory Storage

```
┌─────────────────────────────────────────────────────────────┐
│            OrchestrationService Memory Cache                │
│                                                             │
│  Dictionary<string, object> _responseCache                  │
│                                                             │
│  ┌───────────────────────────────────────────────────┐    │
│  │ Key: "txn_123_validation"                         │    │
│  │ Value: ValidationResponseDto                       │    │
│  │  {                                                 │    │
│  │    transactionId: "txn_123",                      │    │
│  │    isValid: true,                                 │    │
│  │    message: "Valid US address",                   │    │
│  │    auditLogs: [...]                               │    │
│  │  }                                                 │    │
│  └───────────────────────────────────────────────────┘    │
│                                                             │
│  ┌───────────────────────────────────────────────────┐    │
│  │ Key: "txn_123_applicability"                      │    │
│  │ Value: ApplicabilityResponseDto                    │    │
│  │  {                                                 │    │
│  │    transactionId: "txn_123",                      │    │
│  │    isApplicable: true,                            │    │
│  │    merchantVolume: 2300000,                       │    │
│  │    threshold: 100000,                             │    │
│  │    auditLogs: [...]                               │    │
│  │  }                                                 │    │
│  └───────────────────────────────────────────────────┘    │
│                                                             │
│  ┌───────────────────────────────────────────────────┐    │
│  │ Key: "txn_123_exemption"                          │    │
│  │ Value: ExemptionResponseDto                        │    │
│  └───────────────────────────────────────────────────┘    │
│                                                             │
│  ┌───────────────────────────────────────────────────┐    │
│  │ Key: "txn_123_calculation"                        │    │
│  │ Value: CalculationResponseDto                      │    │
│  └───────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────┘
```

## Gate Pattern Flow

```
┌─────────────┐
│   Start     │
└──────┬──────┘
       │
       ▼
┌──────────────────────┐
│   Gate 1: Validate   │
│   Address            │
└──────┬───────┬───────┘
       │       │
    PASS      FAIL
       │       │
       │       └──────────────┐
       ▼                      │
┌──────────────────────┐     │
│   Gate 2:            │     │
│   Applicability      │     │
└──────┬───────┬───────┘     │
       │       │              │
    PASS      FAIL            │
       │       │              │
       │       └──────────┐   │
       ▼                  │   │
┌──────────────────────┐ │   │
│   Gate 3:            │ │   │
│   Exemption Check    │ │   │
└──────┬───────┬───────┘ │   │
       │       │          │   │
    PASS      FAIL        │   │
       │       │          │   │
       │       └──────┐   │   │
       ▼              │   │   │
┌──────────────────┐  │   │   │
│   Gate 4:        │  │   │   │
│   Calculation    │  │   │   │
└──────┬───────────┘  │   │   │
       │              │   │   │
    PASS              │   │   │
       │              │   │   │
       ▼              ▼   ▼   ▼
┌──────────────────────────────┐
│   Response                   │
│   Status: CALCULATED/FAILED  │
└──────────────────────────────┘
```

## Port Assignment

```
┌──────────────────┬──────────┬─────────────────────────┐
│ Service          │ Port     │ Purpose                 │
├──────────────────┼──────────┼─────────────────────────┤
│ Orchestrator     │ 5000     │ Main coordinator        │
│                  │ 5001     │ HTTPS (if configured)   │
├──────────────────┼──────────┼─────────────────────────┤
│ Validation       │ 8001     │ Address validation      │
├──────────────────┼──────────┼─────────────────────────┤
│ Applicability    │ 8002     │ Volume threshold check  │
├──────────────────┼──────────┼─────────────────────────┤
│ Exemption        │ 8003     │ Tax exemption check     │
├──────────────────┼──────────┼─────────────────────────┤
│ Calculation      │ 8004     │ Fee calculation         │
└──────────────────┴──────────┴─────────────────────────┘
```

## Technology Stack Diagram

```
┌─────────────────────────────────────────────────────────┐
│                    .NET 8.0 Runtime                     │
└─────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────┐
│              ASP.NET Core Framework                     │
│  • Web API Controllers                                  │
│  • Dependency Injection                                 │
│  • Configuration Management                             │
│  • Kestrel Web Server                                   │
└─────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────┐
│                 NuGet Packages                          │
│  • Microsoft.Extensions.Http (HttpClient)               │
│  • Swashbuckle.AspNetCore (Swagger)                     │
│  • xUnit (Testing)                                      │
│  • Moq (Mocking)                                        │
│  • FluentAssertions (Test assertions)                   │
└─────────────────────────────────────────────────────────┘
```

This visual diagram shows the complete system architecture with all components, data flows, and relationships clearly illustrated.
