# Runs the OrderDesk WPF desktop app (Windows only).
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
dotnet run --project (Join-Path $root 'src/OrderDesk/OrderDesk.Desktop/OrderDesk.Desktop.csproj')
