# Runs the FulfillmentAPI (Swagger at /swagger).
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
dotnet run --project (Join-Path $root 'src/FulfillmentAPI/FulfillmentAPI.Api/FulfillmentAPI.Api.csproj')
