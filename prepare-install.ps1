# Source-checkout helper. Release ZIPs already include their verified app payload.
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$localSdk = Join-Path $PSScriptRoot '.tools/dotnet/dotnet.exe'
$dotnet = if (Test-Path -LiteralPath $localSdk) { $localSdk } else {
    $command = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $command) { throw 'Building from source requires the SDK in global.json. Alternatively, download the release ZIP and run its Install.cmd.' }
    $command.Source
}
$payload = Join-Path $PSScriptRoot ('artifacts/source-install/' + [Guid]::NewGuid().ToString('N') + '/app')
Push-Location $PSScriptRoot
try {
    & $dotnet publish src/CodexQuotaTray.App -c Release -r win-x64 --self-contained true -o $payload '-p:Version=0.1.1-local' | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Source build failed. The installed app has not been changed.' }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LICENSE') -Destination $payload
    foreach ($notice in @('LICENSE.txt','ThirdPartyNotices.txt')) {
        Copy-Item -LiteralPath (Join-Path (Split-Path $dotnet) $notice) -Destination (Join-Path $payload ('DOTNET-' + $notice))
    }
    Get-ChildItem -LiteralPath $payload -Recurse -File | Sort-Object FullName | ForEach-Object {
        $relative = $_.FullName.Substring($payload.Length + 1).Replace('\', '/')
        '{0}  {1}' -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash, $relative
    } | Set-Content -LiteralPath (Join-Path $payload 'FILES-SHA256.txt') -Encoding UTF8
    Write-Output $payload
} finally { Pop-Location }
