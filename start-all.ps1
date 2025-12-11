# Quick Start Script
# Run this script to start all services in separate PowerShell windows

Write-Host "Starting VatIT Orchestrator + Worker System..." -ForegroundColor Green

$rootPath = Get-Location

# Start Validation Worker
Write-Host "Starting Validation Worker on port 8001..." -ForegroundColor Cyan
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$rootPath\src\Workers\Validation\VatIT.Worker.Validation'; dotnet run"

Start-Sleep -Seconds 2

# Start Applicability Worker
Write-Host "Starting Applicability Worker on port 8002..." -ForegroundColor Cyan
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$rootPath\src\Workers\Applicability\VatIT.Worker.Applicability'; dotnet run"

Start-Sleep -Seconds 2

# Start Exemption Worker
Write-Host "Starting Exemption Worker on port 8003..." -ForegroundColor Cyan
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$rootPath\src\Workers\Exemption\VatIT.Worker.Exemption'; dotnet run"

Start-Sleep -Seconds 2

# Start Calculation Worker
Write-Host "Starting Calculation Worker on port 8004..." -ForegroundColor Cyan
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$rootPath\src\Workers\Calculation\VatIT.Worker.Calculation'; dotnet run"

Start-Sleep -Seconds 3

# Start Orchestrator API
Write-Host "Starting Orchestrator API on port 5100..." -ForegroundColor Cyan
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$rootPath\src\Presentation\VatIT.Orchestrator.Api'; dotnet run"

Start-Sleep -Seconds 5

Write-Host ""
Write-Host "All services started!" -ForegroundColor Green
Write-Host ""
Write-Host "Orchestrator API: http://localhost:5100" -ForegroundColor Yellow
Write-Host "Swagger UI: http://localhost:5100/swagger" -ForegroundColor Yellow
Write-Host ""
Write-Host "Test with:" -ForegroundColor White
Write-Host "  Invoke-RestMethod -Uri 'http://localhost:5100/api/transaction/health' -Method Get" -ForegroundColor Gray
