param([switch]$Publish)
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$localSdk = Join-Path $PSScriptRoot '.tools/dotnet/dotnet.exe'
if (Test-Path -LiteralPath $localSdk) {
    $dotnet = $localSdk
    $env:DOTNET_ROOT = Split-Path $localSdk
} else {
    $dotnet = (Get-Command dotnet -ErrorAction Stop).Source
}
Push-Location $PSScriptRoot
try {
    & $dotnet build --configuration Release
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
    & $dotnet run --project tests/CodexQuotaTray.Tests --configuration Release
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
    if ($Publish) {
        & $dotnet publish src/CodexQuotaTray.App --configuration Release --runtime win-x64 --self-contained true --output artifacts/win-x64
        if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
        $root = Join-Path $PSScriptRoot 'artifacts/win-x64'
        Get-ChildItem -LiteralPath $root -File | Where-Object Name -ne 'SHA256SUMS.txt' | Sort-Object Name | ForEach-Object {
            '{0}  {1}' -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $_.Name
        } | Set-Content -LiteralPath (Join-Path $root 'SHA256SUMS.txt') -Encoding utf8
    }
} finally { Pop-Location }
