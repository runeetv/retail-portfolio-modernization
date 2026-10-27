# Provisions LegacyRetailDB: creates schema + procedures and seeds deterministic data.
# Usage:
#   ./provision-db.ps1            # create + seed (idempotent)
#   ./provision-db.ps1 -Reset     # drop + recreate + seed
param([switch]$Reset)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$proj = Join-Path $root 'database/provisioning/DbProvisioner/DbProvisioner.csproj'

$args = @()
if ($Reset) { $args += '--reset' }

Write-Host "Provisioning LegacyRetailDB (Reset=$Reset)..." -ForegroundColor Cyan
dotnet run --project $proj -- @args
