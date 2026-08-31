$ErrorActionPreference = "Stop"

Push-Location (Join-Path $PSScriptRoot "..")
try {
    dotnet build
    dotnet test --no-build
    dotnet run --project "src\CitySim.Headless"
}
finally {
    Pop-Location
}
