# Publish & Run Script (Release, no symbols)
# Publishes each project to a local publish folder without PDBs and starts them with Production environment

Write-Host "Publishing and starting VatIT Orchestrator + Worker System (Release, no PDBs)..." -ForegroundColor Green

$rootPath = Get-Location
$publishRoot = Join-Path $rootPath "publish"

if (Test-Path $publishRoot) {
    Write-Host "Cleaning existing publish folder..." -ForegroundColor Yellow
    Remove-Item $publishRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $publishRoot | Out-Null

function Publish-Project($projPath, $name) {
    Write-Host "Publishing $name..." -ForegroundColor Cyan
    dotnet publish $projPath -c Release -o (Join-Path $publishRoot $name) /p:DebugType=None /p:DebugSymbols=false
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Publish failed for $name"
        exit 1
    }
}

function Start-Published($name, $dllName, $delaySeconds) {
    $folder = Join-Path $publishRoot $name
    if (!(Test-Path $folder)) { Write-Error "Publish folder missing: $folder"; exit 1 }

    Write-Host "Starting $name from $folder..." -ForegroundColor Cyan
    $command = "cd '$folder'; `$env:ASPNETCORE_ENVIRONMENT='Production'; dotnet $dllName"
    Start-Process powershell -ArgumentList "-NoExit", "-Command", $command
    Start-Sleep -Seconds $delaySeconds
}

# Projects and DLL names
$projects = @(
    @{ Path = "src/Workers/Validation/VatIT.Worker.Validation/VatIT.Worker.Validation.csproj"; Name = "Validation"; Dll = "VatIT.Worker.Validation.dll"; Delay = 2 },
    @{ Path = "src/Workers/Applicability/VatIT.Worker.Applicability/VatIT.Worker.Applicability.csproj"; Name = "Applicability"; Dll = "VatIT.Worker.Applicability.dll"; Delay = 2 },
    @{ Path = "src/Workers/Exemption/VatIT.Worker.Exemption/VatIT.Worker.Exemption.csproj"; Name = "Exemption"; Dll = "VatIT.Worker.Exemption.dll"; Delay = 2 },
    @{ Path = "src/Workers/Calculation/VatIT.Worker.Calculation/VatIT.Worker.Calculation.csproj"; Name = "Calculation"; Dll = "VatIT.Worker.Calculation.dll"; Delay = 3 },
    @{ Path = "src/Presentation/VatIT.Orchestrator.Api/VatIT.Orchestrator.Api.csproj"; Name = "Orchestrator"; Dll = "VatIT.Orchestrator.Api.dll"; Delay = 5 }
)

# Publish all projects
foreach ($p in $projects) {
    Publish-Project -projPath $p.Path -name $p.Name
}

# Start published apps
foreach ($p in $projects) {
    Start-Published -name $p.Name -dllName $p.Dll -delaySeconds $p.Delay
}

Write-Host "All services published and started (Release, Production)." -ForegroundColor Green
Write-Host "Orchestrator API: http://localhost:5100" -ForegroundColor Yellow
Write-Host "Use your load test against http://localhost:5100/api/transaction/process" -ForegroundColor White
