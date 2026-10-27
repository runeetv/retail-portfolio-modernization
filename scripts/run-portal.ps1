# Runs the CustomerPortal web app.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
dotnet run --project (Join-Path $root 'src/CustomerPortal/CustomerPortal.Web/CustomerPortal.Web.csproj')
