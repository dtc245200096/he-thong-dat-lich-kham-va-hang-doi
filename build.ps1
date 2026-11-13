Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " Building Clinic Queue System (.NET 10 Solutions) ...    " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$projects = @(
    "src/Shared.Contracts/Shared.Contracts.csproj",
    "src/Appointment.Api/Appointment.Api.csproj",
    "src/Queue.Worker/Queue.Worker.csproj",
    "src/Notification.Service/Notification.Service.csproj",
    "src/Patient.Client/Patient.Client.csproj"
)

$hasError = $false
foreach ($proj in $projects) {
    Write-Host "`n>>> Building $proj ..." -ForegroundColor Yellow
    dotnet build $proj
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed for $proj" -ForegroundColor Red
        $hasError = $true
        break
    }
}

if (-not $hasError) {
    Write-Host "`n==========================================================" -ForegroundColor Green
    Write-Host " ALL PROJECTS BUILT SUCCESSFULLY!                        " -ForegroundColor Green
    Write-Host "==========================================================" -ForegroundColor Green
} else {
    exit 1
}

