param([string]$Version = '0.1.0')
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Expected a numeric three-part version.' }
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$localSdk = Join-Path $PSScriptRoot '.tools/dotnet/dotnet.exe'
$dotnet = if (Test-Path -LiteralPath $localSdk) { $localSdk } else { (Get-Command dotnet -ErrorAction Stop).Source }
Push-Location $PSScriptRoot
try {
    if (git status --porcelain) { throw 'Commit source changes before packaging a release.' }
    $revision = git rev-parse HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Cannot determine source revision.' }
    $destination = Join-Path $PSScriptRoot "artifacts/releases/v$Version"
    if (Test-Path -LiteralPath $destination) { throw 'Release directory already exists; choose a new version or preserve it elsewhere first.' }
    $packageName = "CodexQuotaTray-v$Version-win-x64"
    $payload = Join-Path $destination $packageName
    New-Item -ItemType Directory -Path $payload -Force | Out-Null
    & $dotnet build -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    & $dotnet run --project tests/CodexQuotaTray.Tests -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
    & $dotnet publish src/CodexQuotaTray.App -c Release -r win-x64 --self-contained true -o $payload "-p:Version=$Version" "-p:SourceRevisionId=$revision"
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    Copy-Item -LiteralPath LICENSE -Destination $payload
    Copy-Item -LiteralPath "docs/releases/v$Version.md" -Destination (Join-Path $payload 'README.txt')
    $sdkRoot = Split-Path $dotnet
    Copy-Item -LiteralPath (Join-Path $sdkRoot 'LICENSE.txt') -Destination (Join-Path $payload 'DOTNET-LICENSE.txt')
    Copy-Item -LiteralPath (Join-Path $sdkRoot 'ThirdPartyNotices.txt') -Destination (Join-Path $payload 'DOTNET-ThirdPartyNotices.txt')
    $info = @(
        "Product: Codex Quota Tray $Version", "Source: https://github.com/thegreatkingbear/codex-quota-tray",
        "Commit: $revision", "SDK: $(& $dotnet --version)", 'Target: win-x64 / net10.0-windows / self-contained',
        "Build UTC: $([DateTimeOffset]::UtcNow.ToString('O'))", 'Signing: unsigned',
        'Verification: Windows Release build and executable test harness passed.',
        'Reproducibility: deterministic compiler enabled; bit-for-bit package reproducibility not verified.'
    )
    $info | Set-Content -LiteralPath (Join-Path $payload 'BUILD-INFO.txt') -Encoding utf8
    Copy-Item -LiteralPath (Join-Path $payload 'BUILD-INFO.txt') -Destination $destination
    Get-ChildItem -LiteralPath $payload -Recurse -File | Sort-Object FullName | ForEach-Object {
        '{0}  {1}' -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), [IO.Path]::GetRelativePath($payload, $_.FullName).Replace('\', '/')
    } | Set-Content -LiteralPath (Join-Path $payload 'FILES-SHA256.txt') -Encoding utf8
    $zip = Join-Path $destination "$packageName.zip"
    Compress-Archive -LiteralPath $payload -DestinationPath $zip -CompressionLevel Optimal
    '{0}  {1}' -f (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant(), [IO.Path]::GetFileName($zip) |
        Set-Content -LiteralPath (Join-Path $destination 'SHA256SUMS.txt') -Encoding utf8
    Write-Output "Release package: $zip"
} finally { Pop-Location }
