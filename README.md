# VatIT Technical Assessment - Orchestrator + Worker Pattern

## Overview

This is a **Clean Architecture** implementation of an async, non-blocking orchestrator + worker pattern system for processing fee calculations through a series of validation gates.

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Orchestrator API                          │
│                   (ASP.NET Core Web API)                     │
│                      Port: 5000/5001                         │
└──────────────┬──────────────────────────────────────────────┘
               │
               │  Async HTTP Calls
               │
       ┌───────┴───────┬───────────┬───────────┐
       │               │           │           │
       ▼               ▼           ▼           ▼
┌─────────────┐ ┌─────────┐ ┌─────────┐ ┌──────────┐
│ Validation  │ │Applicabi│ │Exemption│ │Calculation│
│   Worker    │ │lity     │ │ Worker  │ │  Worker   │
│  Port 8001  │ │Worker   │ │Port 8003│ │Port 8004  │
│             │ │Port 8002│ │         │ │           │
└─────────────┘ └─────────┘ └─────────┘ └──────────┘
```

### Gate Pattern

The system implements a **gate pattern** where processing stops immediately if any gate fails:

1. **Validation Gate** (Port 8001): Validates address information
2. **Applicability Gate** (Port 8002): Checks if merchant meets volume thresholds
3. **Exemption Gate** (Port 8003): Checks for applicable tax exemptions
4. **Calculation Gate** (Port 8004): Calculates fees with detailed breakdowns

## Project Structure

```
VatIT_Technical_Assessment2/
├── src/
│   ├── VatIT.Domain/                 # Domain entities and DTOs
│   ├── VatIT.Application/            # Business logic and interfaces
│   ├── VatIT.Infrastructure/         # HTTP client and external services
│   ├── VatIT.Orchestrator.Api/       # Main orchestrator API
│   ├── VatIT.Worker.Validation/      # Address validation worker
│   ├── VatIT.Worker.Applicability/   # Applicability check worker
│   ├── VatIT.Worker.Exemption/       # Exemption check worker
│   └── VatIT.Worker.Calculation/     # Fee calculation worker
└── tests/
    └── VatIT.Tests/                  # Unit and integration tests
```

## Prerequisites

- .NET 8.0 SDK
- Visual Studio 2022 or VS Code
- PowerShell (Windows)

## Getting Started

### 1. Restore Dependencies

```powershell
dotnet restore
```

### 2. Build the Solution

```powershell
dotnet build
```

### 3. Run Unit Tests

```powershell
dotnet test
```

### 4. Start All Services

You need to start all 5 services in separate terminal windows:

**Terminal 1 - Validation Worker:**
```powershell
cd src\Workers\Validation\VatIT.Worker.Validation
dotnet run
```

**Terminal 2 - Applicability Worker:**
```powershell
cd src\Workers\Applicability\VatIT.Worker.Applicability
dotnet run
```

**Terminal 3 - Exemption Worker:**
```powershell
cd src\Workers\Exemption\VatIT.Worker.Exemption
dotnet run
```

**Terminal 4 - Calculation Worker:**
```powershell
cd src\Workers\Calculation\VatIT.Worker.Calculation
dotnet run
```

**Terminal 5 - Orchestrator API:**
```powershell
cd src\Presentation\VatIT.Orchestrator.Api
dotnet run
```

### 5. Test the System

Once all services are running, you can test the orchestrator:

**Using PowerShell:**
```powershell
$body = @{
    transactionId = "txn_123"
    merchantId = "merchant_456"
    customerId = "customer_789"
    destination = @{
        country = "US"
        state = "CA"
        city = "Los Angeles"
    }
    items = @(
        @{
            id = "item_1"
            category = "SOFTWARE"
            amount = 100.00
        },
        @{
            id = "item_2"
            category = "PHYSICAL_GOODS"
            amount = 50.00
        }
    )
    totalAmount = 150.00
    currency = "USD"
} | ConvertTo-Json -Depth 10

Invoke-RestMethod -Uri "http://localhost:5000/api/transaction/process" -Method Post -Body $body -ContentType "application/json"
```

**Using curl:**
```bash
curl -X POST http://localhost:5000/api/transaction/process \
  -H "Content-Type: application/json" \
  -d '{
    "transactionId": "txn_123",
    "merchantId": "merchant_456",
    "customerId": "customer_789",
    "destination": {
      "country": "US",
      "state": "CA",
      "city": "Los Angeles"
    },
    "items": [
      {
        "id": "item_1",
        "category": "SOFTWARE",
        "amount": 100.00
      },
      {
        "id": "item_2",
        "category": "PHYSICAL_GOODS",
        "amount": 50.00
      }
    ],
    "totalAmount": 150.00,
    "currency": "USD"
  }'
```

## API Endpoints

### Orchestrator API (Port 5000/5001)

- **POST** `/api/transaction/process` - Process a transaction through all gates
- **GET** `/api/transaction/health` - Health check

### Worker Services

Each worker exposes:
- **POST** `/api/{endpoint}` - Process gate-specific request
- **GET** `/api/{endpoint}/health` - Health check

## Sample Response

```json
{
  "transactionId": "txn_123",
  "status": "CALCULATED",
  "gates": [
    {
      "name": "ADDRESS_VALIDATION",
      "passed": true,
      "message": "Valid US address"
    },
    {
      "name": "APPLICABILITY",
      "passed": true,
      "message": "Merchant above $100K threshold in CA"
    },
    {
      "name": "EXEMPTION_CHECK",
      "passed": true,
      "message": "No exemptions applied",
      "appliedExemptions": []
    }
  ],
  "calculation": {
    "items": [
      {
        "itemId": "item_1",
        "amount": 100.00,
        "category": "SOFTWARE",
        "fees": {
          "stateRate": {
            "jurisdiction": "CA",
            "rate": 0.06,
            "amount": 6.00
          },
          "countyRate": {
            "jurisdiction": "Los Angeles County",
            "rate": 0.0025,
            "amount": 0.25
          },
          "cityRate": {
            "jurisdiction": "Los Angeles",
            "rate": 0.0225,
            "amount": 2.25
          },
          "categoryModifier": {
            "jurisdiction": "CA",
            "category": "SOFTWARE",
            "rate": 0.01,
            "amount": 1.00
          }
        },
        "totalFee": 9.50
      }
    ],
    "totalFees": 9.50,
    "effectiveRate": 0.095
  },
  "auditTrail": [
    "Country validation passed: US",
    "State validation passed: CA",
    "Address validated via cache",
    "State threshold for CA: $100,000",
    "Merchant volume: $2.3M in CA",
    "No exemptions applicable",
    "Processing item item_1 ($100.00, SOFTWARE)",
    "Total fees calculated: $9.50"
  ]
}
```

## Key Features

### ✅ Clean Architecture
- **Domain Layer**: Pure business entities and DTOs
- **Application Layer**: Business logic and interfaces
- **Infrastructure Layer**: External service communication
- **Presentation Layer**: API controllers

### ✅ Async Non-Blocking
- All worker communications are fully async
- Uses `Task<T>` and `async/await` throughout
- HttpClient-based communication between services

### ✅ Gate Pattern
- Sequential gate processing
- Stops immediately on gate failure
- Each gate validates specific business rules

### ✅ Audit Trail
- Every gate logs its decisions
- Complete audit trail included in response
- Timestamps on all operations

### ✅ In-Memory Response Storage
- Orchestrator caches partial responses
- Enables debugging and analysis
- Memory-efficient for production use

### ✅ Comprehensive Testing
- Unit tests for orchestration logic
- Unit tests for each worker's business logic
- Integration tests for end-to-end scenarios
- Uses xUnit, Moq, and FluentAssertions

## Configuration

Worker endpoints are configured in `appsettings.json`:

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

## Development Notes

### Simulated Data

The workers contain simulated data for demonstration:

- **Validation Worker**: Validates US addresses in specific states/cities
- **Applicability Worker**: Merchant `merchant_456` has $2.3M volume in CA
- **Exemption Worker**: No exemptions for the sample customer
- **Calculation Worker**: Fixed rate tables for CA jurisdiction

### Error Handling

- Failed gates stop processing immediately
- HTTP errors are propagated to the orchestrator
- All errors are logged with context

## Future Enhancements

- Add message queue (RabbitMQ/Azure Service Bus) for true async processing
- Implement Redis for distributed response caching
- Add authentication and authorization
- Implement retry policies with Polly
- Add distributed tracing (Application Insights/OpenTelemetry)
- Database persistence for audit trails
- Rate limiting and throttling
- API versioning

## License

MIT License - Feel free to use this as a reference or starting point for your own projects.

## Author

Technical Assessment Solution for VatIT
