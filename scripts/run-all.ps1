<#
.SYNOPSIS
    Launches the full LegacyRetailPortfolio: provisions the database (if needed),
    then starts FulfillmentAPI, CustomerPortal, and (on Windows) OrderDesk together,
    each in its own window.

.PARAMETER Reset
    Drop + recreate + reseed the database before launching.

.PARAMETER SkipProvision
    Skip the database provisioning step entirely.

.PARAMETER NoOrderDesk
    Do not launch the OrderDesk WPF desktop app (useful on non-Windows hosts).

.EXAMPLE
    ./scripts/run-all.ps1
    ./scripts/run-all.ps1 -Reset
    ./scripts/run-all.ps1 -SkipProvision -NoOrderDesk
#>
param(
    [switch]$Reset,
    [switch]$SkipProvision,
    [switch]$NoOrderDesk
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

$provisioner = Join-Path $root 'database/provisioning/DbProvisioner/DbProvisioner.csproj'
$fulfillment = Join-Path $root 'src/FulfillmentAPI/FulfillmentAPI.Api/FulfillmentAPI.Api.csproj'
$portal      = Join-Path $root 'src/CustomerPortal/CustomerPortal.Web/CustomerPortal.Web.csproj'
$orderdesk   = Join-Path $root 'src/OrderDesk/OrderDesk.Desktop/OrderDesk.Desktop.csproj'

$fulfillmentUrl = 'http://localhost:5201'
$portalUrl      = 'http://localhost:5101'

$isWindows = $env:OS -eq 'Windows_NT'

function Write-Step($msg) { Write-Host "==> $msg" -ForegroundColor Cyan }

# 1. Provision the database -------------------------------------------------
if (-not $SkipProvision) {
    Write-Step "Provisioning LegacyRetailDB (Reset=$Reset)..."
    $provArgs = @('run', '--project', $provisioner)
    if ($Reset) { $provArgs += @('--', '--reset') }
    & dotnet @provArgs
    if ($LASTEXITCODE -ne 0) { throw "Database provisioning failed (exit $LASTEXITCODE)." }
} else {
    Write-Step "Skipping database provisioning."
}

# 2. Build once up front so each app starts with --no-build ----------------
Write-Step "Building solution..."
& dotnet build (Join-Path $root 'LegacyRetailPortfolio.slnx') -v quiet
if ($LASTEXITCODE -ne 0) { throw "Build failed (exit $LASTEXITCODE)." }

# Helper: start a project in a new window (cross-platform best effort).
function Start-App($title, $project, $extraArgs) {
    $argList = @('run', '--project', $project, '--no-build')
    if ($extraArgs) { $argList += $extraArgs }

    if ($isWindows) {
        # Open a new PowerShell window per app so logs are visible and independent.
        $inner = "`$host.UI.RawUI.WindowTitle='$title'; dotnet $($argList -join ' ')"
        Start-Process -FilePath 'powershell.exe' `
            -ArgumentList @('-NoExit', '-Command', $inner) `
            -WorkingDirectory $root | Out-Null
    } else {
        # Non-Windows: start detached in the background.
        Start-Process -FilePath 'dotnet' -ArgumentList $argList -WorkingDirectory $root | Out-Null
    }
    Write-Host "    launched: $title" -ForegroundColor Green
}

# 3. Launch the services ----------------------------------------------------
Write-Step "Starting FulfillmentAPI -> $fulfillmentUrl/swagger"
Start-App 'FulfillmentAPI' $fulfillment @('--urls', $fulfillmentUrl)

Write-Step "Starting CustomerPortal -> $portalUrl"
Start-App 'CustomerPortal' $portal @('--urls', $portalUrl)

if (-not $NoOrderDesk) {
    if ($isWindows) {
        Write-Step "Starting OrderDesk (WPF desktop)..."
        Start-App 'OrderDesk' $orderdesk $null
    } else {
        Write-Warning "OrderDesk is a WPF app and only runs on Windows. Skipping (use -NoOrderDesk to silence)."
    }
}

Write-Host ""
Write-Step "All requested applications launched."
Write-Host "  FulfillmentAPI : $fulfillmentUrl/swagger   (X-Api-Key: dev-fulfillment-key)" -ForegroundColor Yellow
Write-Host "  CustomerPortal : $portalUrl                 (login user1 / Password123!)"      -ForegroundColor Yellow
if (-not $NoOrderDesk -and $isWindows) {
    Write-Host "  OrderDesk      : desktop window"                                           -ForegroundColor Yellow
}
Write-Host ""
Write-Host "Each app runs in its own window. Close those windows (or Ctrl+C in them) to stop." -ForegroundColor DarkGray
