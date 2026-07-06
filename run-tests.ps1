$ErrorActionPreference = "Stop"

$dotnet = "C:\Program Files\dotnet\dotnet.exe"
if (-not (Test-Path $dotnet)) {
    $dotnet = "dotnet"
}

& $dotnet run --project .\tests\PharmaExt.Tests\PharmaExt.Tests.csproj
exit $LASTEXITCODE
