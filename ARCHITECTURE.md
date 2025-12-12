# Architecture Overview

Last updated: 2025-12-12

Changelog:
- 2025-12-12: Architecture notes updated to mention correlation propagation, HttpClient/Polly tuning, and the `BENCHMARK_MODE` used for local load testing.

## System Architecture

This system implements an **Orchestrator + Worker Pattern** with **Clean Architecture** principles for processing fee calculations through a series of validation gates.

## High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                         Client Application                       │
│                    (HTTP Client / Postman / etc.)               │
└────────────────────────────┬────────────────────────────────────┘
                             │
                             │ HTTP POST /api/transaction/process
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│                      Orchestrator API                            │
│                   (ASP.NET Core Web API)                         │
│                        Port: 5000                                │
│                                                                  │
│  ┌──────────────────────────────────────────────────┐          │
│  │         TransactionController                     │          │
│  └──────────────────┬───────────────────────────────┘          │
│                     │                                            │
│  ┌──────────────────▼───────────────────────────────┐          │
│  │      OrchestrationService (Application Layer)     │          │
│  │  • Splits request into 4 DTOs                     │          │
│  │  • Calls workers sequentially                     │          │
│  │  • Stores partial responses in memory             │          │
│  │  • Assembles final response                       │          │
│  └──────────────────┬───────────────────────────────┘          │
│                     │                                            │
│  ┌──────────────────▼───────────────────────────────┐          │
│  │      WorkerClient (Infrastructure Layer)          │          │
│  │  • HTTP communication with workers                │          │
│  │  • Serialization/deserialization                  │          │
│  └─────┬────────┬───────────┬───────────┬───────────┘          │
└────────┼────────┼───────────┼───────────┼──────────────────────┘
         │        │           │           │
         │ Async  │ Async     │ Async     │ Async
         │ HTTP   │ HTTP      │ HTTP      │ HTTP
         │        │           │           │
    ┌────▼───┐ ┌─▼──────┐ ┌──▼──────┐ ┌──▼──────────┐
    │ Gate 1 │ │ Gate 2 │ │ Gate 3  │ │   Gate 4    │
    │Validate│ │Applica-│ │Exemption│ │ Calculation │
    │        │ │bility  │ │         │ │             │
    │Port    │ │Port    │ │Port     │ │Port         │
    │8001    │ │8002    │ │8003     │ │8004         │
    └────────┘ └────────┘ └─────────┘ └─────────────┘
```

## Clean Architecture Layers

### 1. Domain Layer (`VatIT.Domain`)
**Responsibility:** Core business entities and data structures

```
VatIT.Domain/
├── Entities/
│   ├── TransactionRequest.cs      # Input domain model
│   ├── TransactionResponse.cs     # Output domain model
│   ├── Destination.cs
│   ├── Item.cs
│   └── ...
└── DTOs/
    ├── ValidationRequestDto.cs    # Gate 1 DTO
    ├── ValidationResponseDto.cs
    ├── ApplicabilityRequestDto.cs # Gate 2 DTO
    ├── ApplicabilityResponseDto.cs
    ├── ExemptionRequestDto.cs     # Gate 3 DTO
    ├── ExemptionResponseDto.cs
    ├── CalculationRequestDto.cs   # Gate 4 DTO
    └── CalculationResponseDto.cs
```

**Key Features:**
- No dependencies on other layers
- Pure C# POCOs
- Contains all audit trail properties

### 2. Application Layer (`VatIT.Application`)
**Responsibility:** Business logic and orchestration

```
VatIT.Application/
├── Interfaces/
│   ├── IOrchestrationService.cs   # Main orchestration contract
│   └── IWorkerClient.cs           # Worker communication contract
└── Services/
    └── OrchestrationService.cs    # Orchestration implementation
        ├── ProcessTransactionAsync()
        ├── MapToValidationRequest()
        ├── MapToApplicabilityRequest()
        ├── MapToExemptionRequest()
        ├── MapToCalculationRequest()
        └── MapToCalculationResult()
```

**Key Features:**
- Implements gate pattern logic
- Sequential gate processing with early termination
- In-memory response caching
- Request/response mapping

### 3. Infrastructure Layer (`VatIT.Infrastructure`)
**Responsibility:** External service communication

```
VatIT.Infrastructure/
├── Configuration/
│   └── WorkerEndpoints.cs         # Worker URL configuration
└── Services/
    └── WorkerClient.cs            # HTTP client implementation
        ├── SendValidationRequestAsync()
        ├── SendApplicabilityRequestAsync()
        ├── SendExemptionRequestAsync()
        └── SendCalculationRequestAsync()
```

**Key Features:**
- HttpClient-based async communication
- JSON serialization/deserialization
- Configurable worker endpoints

### 4. Presentation Layer (`VatIT.Orchestrator.Api`)
**Responsibility:** HTTP API endpoints

```
VatIT.Orchestrator.Api/
├── Controllers/
│   └── TransactionController.cs   # API endpoints
├── Program.cs                     # DI configuration
└── appsettings.json               # Configuration
```

**Key Features:**
- RESTful API design
- Swagger/OpenAPI documentation
- Dependency injection setup

## Worker Services

Each worker is an independent ASP.NET Core Web API listening on its own port:

### Gate 1: Validation Worker (Port 8001)
**Responsibility:** Address validation

```
Validates:
- Country (must be "US")
- State (must be in valid states list)
- City (must be valid for the state)

Response includes:
- IsValid (bool)
- Message (string)
- AuditLogs (List<string>)
```

### Gate 2: Applicability Worker (Port 8002)
**Responsibility:** Check if merchant exceeds volume threshold

```
Checks:
- Merchant volume in state
- State threshold
- Volume >= Threshold

Response includes:
- IsApplicable (bool)
- MerchantVolume (decimal)
- Threshold (decimal)
- Message (string)
- AuditLogs (List<string>)
```

### Gate 3: Exemption Worker (Port 8003)
**Responsibility:** Check for tax exemptions

```
Checks:
- Customer exemptions
- Category-based exemptions
- Item-specific exemptions

Response includes:
- Passed (bool)
- AppliedExemptions (List<string>)
- Message (string)
- AuditLogs (List<string>)
```

### Gate 4: Calculation Worker (Port 8004)
**Responsibility:** Calculate fees with detailed breakdown

```
Calculates:
- State rate fees
- County rate fees
- City rate fees
- Category modifier fees
- Per-item totals
- Overall total fees
- Effective rate

Response includes:
- Items (List<ItemCalculationDto>)
- TotalFees (decimal)
- EffectiveRate (decimal)
- AuditLogs (List<string>)
```

## Data Flow

### Request Flow

```
1. Client sends TransactionRequest
   ↓
2. Orchestrator receives request
   ↓
3. Orchestrator maps to ValidationRequestDto
   ↓
4. Sends async HTTP POST to Validation Worker (8001)
   ↓
5. Validation Worker processes and returns ValidationResponseDto
   ↓
6. Orchestrator stores response in memory
   ↓
7. If validation passes:
   ↓
8. Orchestrator maps to ApplicabilityRequestDto
   ↓
9. Sends async HTTP POST to Applicability Worker (8002)
   ↓
10. (Repeat for Exemption and Calculation workers)
    ↓
11. Orchestrator assembles final TransactionResponse
    ↓
12. Returns response to client
```

### Gate Pattern Logic

```csharp
// Pseudo-code for gate pattern
async Task<TransactionResponse> ProcessTransaction(TransactionRequest request)
{
    var response = new TransactionResponse();
    
    // Gate 1: Validation
    var validationResult = await CallValidationWorker(request);
    response.Gates.Add(validationResult);
    if (!validationResult.Passed)
        return MarkAsFailed(response); // STOP HERE
    
    // Gate 2: Applicability
    var applicabilityResult = await CallApplicabilityWorker(request);
    response.Gates.Add(applicabilityResult);
    if (!applicabilityResult.Passed)
        return MarkAsFailed(response); // STOP HERE
    
    // Gate 3: Exemption
    var exemptionResult = await CallExemptionWorker(request);
    response.Gates.Add(exemptionResult);
    if (!exemptionResult.Passed)
        return MarkAsFailed(response); // STOP HERE
    
    // Gate 4: Calculation
    var calculationResult = await CallCalculationWorker(request);
    response.Calculation = calculationResult;
    
    return MarkAsCalculated(response);
}
```

## Async Non-Blocking Design

### Key Async Features

1. **Async Controller Actions**
   ```csharp
   [HttpPost("process")]
   public async Task<ActionResult<TransactionResponse>> ProcessTransaction(
       [FromBody] TransactionRequest request,
       CancellationToken cancellationToken)
   ```

2. **Async Service Layer**
   ```csharp
   public async Task<TransactionResponse> ProcessTransactionAsync(
       TransactionRequest request, 
       CancellationToken cancellationToken)
   ```

3. **Async HTTP Communication**
   ```csharp
   public async Task<ValidationResponseDto> SendValidationRequestAsync(
       ValidationRequestDto request, 
       CancellationToken cancellationToken)
   ```

4. **Non-Blocking I/O**
   - All HTTP calls use `HttpClient.PostAsync()`
   - All waits use `await` (no `.Result` or `.Wait()`)
   - Proper `CancellationToken` propagation

## In-Memory Response Storage

```csharp
private readonly Dictionary<string, object> _responseCache = new();

// Store partial responses
_responseCache[$"{request.TransactionId}_validation"] = validationResponse;
_responseCache[$"{request.TransactionId}_applicability"] = applicabilityResponse;
_responseCache[$"{request.TransactionId}_exemption"] = exemptionResponse;
_responseCache[$"{request.TransactionId}_calculation"] = calculationResponse;
```

**Purpose:**
- Debugging and diagnostics
- Audit trail reconstruction
- Response caching for repeated requests
- System analysis

## Audit Trail

Every DTO includes an `AuditLogs` property:

```csharp
public List<string> AuditLogs { get; set; } = new();
```

**Audit logs capture:**
- Decision points
- Validation results
- Data lookups
- Calculations performed
- Timestamps

**Example audit trail:**
```
"Country validation passed: US"
"State validation passed: CA"
"Address validated via cache"
"State threshold for CA: $100,000"
"Merchant volume: $2.3M in CA"
"No exemptions applicable"
"Processing item item_1 ($100.00, SOFTWARE)"
"State rate (CA): 6.00% = $6.00"
"Total fees calculated: $9.50"
```

## Scalability Considerations

### Current Architecture
- Synchronous orchestration (sequential gates)
- In-memory response storage
- Single orchestrator instance

### Production Enhancements
1. **Message Queue Integration**
   - Replace HTTP with RabbitMQ/Azure Service Bus
   - Truly async, event-driven processing
   
2. **Distributed Caching**
   - Replace in-memory cache with Redis
   - Share state across orchestrator instances
   
3. **Horizontal Scaling**
   - Multiple orchestrator instances
   - Load balancer in front
   - Multiple worker instances per gate
   
4. **Database Persistence**
   - Store transactions in database
   - Persistent audit trails
   - Query capabilities

## Testing Strategy

### Unit Tests (`VatIT.Tests`)

1. **Orchestration Tests**
   - All gates pass scenario
   - Gate failure scenarios
   - Request mapping validation

2. **Worker Logic Tests**
   - Validation rules
   - Applicability calculations
   - Exemption checks
   - Fee calculations

3. **Integration Tests**
   - End-to-end scenarios
   - Data flow validation

**Total: 22 unit tests covering all critical paths**

## Deployment

### Development
```powershell
.\start-all.ps1  # Starts all 5 services
.\test-system.ps1  # Tests the system
```

### Production Considerations
- Containerize each service (Docker)
- Kubernetes orchestration
- Service mesh (Istio/Linkerd)
- API Gateway (Kong/APIM)
- Monitoring (Application Insights)
- Distributed tracing (OpenTelemetry)

## Configuration

### Worker Endpoints (appsettings.json)
```json
{
  "WorkerEndpoints": {
    "ValidationWorkerUrl": "http://localhost:8001",
    "ApplicabilityWorkerUrl": "http://localhost:8002",
    "ExemptionWorkerUrl": "http://localhost:8003",
    "CalculationWorkerUrl": "http://localhost:8004"
  }
}
```

### Port Assignments
- Orchestrator: 5000 (HTTP), 5001 (HTTPS)
- Validation Worker: 8001
- Applicability Worker: 8002
- Exemption Worker: 8003
- Calculation Worker: 8004

## Security Considerations

**Not implemented (out of scope), but recommended:**
- API authentication (JWT/OAuth2)
- HTTPS enforcement
- Rate limiting
- Input validation
- CORS configuration
- API keys for worker communication

## Summary

This architecture provides:
- ✅ **Clean separation of concerns**
- ✅ **Async non-blocking processing**
- ✅ **Gate pattern with early termination**
- ✅ **Comprehensive audit trails**
- ✅ **In-memory response caching**
- ✅ **Testable and maintainable code**
- ✅ **Production-ready foundation**
