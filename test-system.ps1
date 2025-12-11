# Test Script - Send sample transaction through the system

Write-Host "Testing VatIT Orchestrator System..." -ForegroundColor Green
Write-Host ""

# Test health endpoints first
Write-Host "Checking service health..." -ForegroundColor Cyan

$services = @(
    @{ Name = "Validation Worker"; Url = "http://localhost:8001/api/validate/health" },
    @{ Name = "Applicability Worker"; Url = "http://localhost:8002/api/applicability/health" },
    @{ Name = "Exemption Worker"; Url = "http://localhost:8003/api/exemption/health" },
    @{ Name = "Calculation Worker"; Url = "http://localhost:8004/api/calculate/health" },
    @{ Name = "Orchestrator API"; Url = "http://localhost:5000/api/transaction/health" }
)

$allHealthy = $true
foreach ($service in $services) {
    try {
        $response = Invoke-RestMethod -Uri $service.Url -Method Get -ErrorAction Stop
        Write-Host "  ✓ $($service.Name): $($response.status)" -ForegroundColor Green
    }
    catch {
        Write-Host "  ✗ $($service.Name): OFFLINE" -ForegroundColor Red
        $allHealthy = $false
    }
}

if (-not $allHealthy) {
    Write-Host ""
    Write-Host "Some services are not running. Please start all services first." -ForegroundColor Red
    Write-Host "Run: .\start-all.ps1" -ForegroundColor Yellow
    exit 1
}

Write-Host ""
Write-Host "Sending transaction request..." -ForegroundColor Cyan

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

try {
    $response = Invoke-RestMethod -Uri "http://localhost:5000/api/transaction/process" -Method Post -Body $body -ContentType "application/json"
    
    Write-Host ""
    Write-Host "Response received!" -ForegroundColor Green
    Write-Host "==================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Transaction ID: $($response.transactionId)" -ForegroundColor White
    Write-Host "Status: $($response.status)" -ForegroundColor $(if ($response.status -eq "CALCULATED") { "Green" } else { "Red" })
    Write-Host ""
    
    Write-Host "Gates:" -ForegroundColor Yellow
    foreach ($gate in $response.gates) {
        $passedSymbol = if ($gate.passed) { "✓" } else { "✗" }
        $color = if ($gate.passed) { "Green" } else { "Red" }
        Write-Host "  $passedSymbol $($gate.name): $($gate.message)" -ForegroundColor $color
    }
    
    if ($response.calculation) {
        Write-Host ""
        Write-Host "Calculation:" -ForegroundColor Yellow
        Write-Host "  Total Fees: `$$($response.calculation.totalFees)" -ForegroundColor White
        Write-Host "  Effective Rate: $([math]::Round($response.calculation.effectiveRate * 100, 2))%" -ForegroundColor White
        
        Write-Host ""
        Write-Host "Item Breakdown:" -ForegroundColor Yellow
        foreach ($item in $response.calculation.items) {
            Write-Host "  Item $($item.itemId) ($($item.category)):" -ForegroundColor Cyan
            if ($item.fees.stateRate) {
                Write-Host "    State: `$$($item.fees.stateRate.amount) ($($item.fees.stateRate.rate * 100)%)" -ForegroundColor Gray
            }
            if ($item.fees.countyRate) {
                Write-Host "    County: `$$($item.fees.countyRate.amount) ($($item.fees.countyRate.rate * 100)%)" -ForegroundColor Gray
            }
            if ($item.fees.cityRate) {
                Write-Host "    City: `$$($item.fees.cityRate.amount) ($($item.fees.cityRate.rate * 100)%)" -ForegroundColor Gray
            }
            if ($item.fees.categoryModifier) {
                Write-Host "    Category: `$$($item.fees.categoryModifier.amount) ($($item.fees.categoryModifier.rate * 100)%)" -ForegroundColor Gray
            }
            Write-Host "    Total: `$$($item.totalFee)" -ForegroundColor White
        }
    }
    
    Write-Host ""
    Write-Host "Audit Trail:" -ForegroundColor Yellow
    foreach ($log in $response.auditTrail) {
        Write-Host "  • $log" -ForegroundColor Gray
    }
    
    Write-Host ""
    Write-Host "Test completed successfully!" -ForegroundColor Green
}
catch {
    Write-Host ""
    Write-Host "Error processing transaction:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}
