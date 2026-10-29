$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot '../installer/Install.Core.psm1') -Force
$root = Join-Path $PSScriptRoot ('../artifacts/installer-tests/' + [Guid]::NewGuid().ToString('N'))
$payload = Join-Path $root 'payload'
$destination = Join-Path $root 'Programs/CodexQuotaTray'
New-Item -ItemType Directory -Path $payload -Force | Out-Null
$script:checks = 0
function Check($Value, $Label) { if (-not $Value) { throw $Label }; $script:checks++; Write-Host "PASS $Label" }
function Manifest {
    Get-ChildItem -LiteralPath $payload -File | Where-Object Name -ne 'FILES-SHA256.txt' | ForEach-Object {
        '{0}  {1}' -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash, $_.Name
    } | Set-Content -LiteralPath (Join-Path $payload 'FILES-SHA256.txt')
}
foreach ($name in @('CodexQuotaTray.App.exe','CodexQuotaTray.App.dll','coreclr.dll','old-file.txt')) { 'version1' | Set-Content (Join-Path $payload $name) }
Manifest
$backup = Install-QuotaFiles $payload $destination
Check ($null -eq $backup -and (Test-Path (Join-Path $destination 'CodexQuotaTray.App.exe'))) 'fresh fixed-folder installation'
Remove-Item -LiteralPath (Join-Path $payload 'old-file.txt')
'version2' | Set-Content (Join-Path $payload 'CodexQuotaTray.App.dll')
Manifest
$backup = Install-QuotaFiles $payload $destination
Check ((Get-Content (Join-Path $destination 'CodexQuotaTray.App.dll')) -eq 'version2') 'update uses same destination'
Check (-not (Test-Path (Join-Path $destination 'old-file.txt'))) 'obsolete files absent after update'
Check ((Get-Content (Join-Path $backup 'CodexQuotaTray.App.dll')) -eq 'version1') 'previous installation retained'
'tampered' | Set-Content (Join-Path $payload 'CodexQuotaTray.App.dll')
$rejected = $false
try { Install-QuotaFiles $payload $destination | Out-Null } catch { $rejected = $true }
Check $rejected 'corrupt payload rejected'
Check ((Get-Content (Join-Path $destination 'CodexQuotaTray.App.dll')) -eq 'version2') 'failed verification preserves installed files'
Manifest
'unexpected' | Set-Content (Join-Path $payload 'extra.txt')
$rejected = $false
try { Test-QuotaPayload $payload } catch { $rejected = $true }
Check $rejected 'unlisted file rejected'
Remove-Item -LiteralPath (Join-Path $payload 'extra.txt')
('a' * 64 + '  ../escape.exe') | Set-Content (Join-Path $payload 'FILES-SHA256.txt')
$rejected = $false
try { Test-QuotaPayload $payload } catch { $rejected = $true }
Check $rejected 'manifest traversal rejected'
Manifest
$rejected = $false
try { Install-QuotaFiles $payload $payload | Out-Null } catch { $rejected = $true }
Check $rejected 'source installation overlap rejected'
$runKey = 'HKCU:\Software\CodexQuotaTray-InstallerTest-' + [Guid]::NewGuid().ToString('N')
try {
    New-Item -Path $runKey | Out-Null
    Update-QuotaStartup $runKey 'C:\Example\CodexQuotaTray.App.exe'
    Check ($null -eq (Get-ItemProperty -LiteralPath $runKey -Name CodexQuotaTray -ErrorAction SilentlyContinue)) 'startup remains opt-in'
    New-ItemProperty -LiteralPath $runKey -Name CodexQuotaTray -Value 'old command' | Out-Null
    New-ItemProperty -LiteralPath $runKey -Name UnrelatedApp -Value 'untouched' | Out-Null
    Update-QuotaStartup $runKey 'C:\Example With Spaces\CodexQuotaTray.App.exe'
    Check ((Get-ItemProperty -LiteralPath $runKey).CodexQuotaTray -eq '"C:\Example With Spaces\CodexQuotaTray.App.exe" --startup') 'existing startup migrates with quoted path'
    Check ((Get-ItemProperty -LiteralPath $runKey).UnrelatedApp -eq 'untouched') 'unrelated startup preserved'
} finally { Remove-Item -LiteralPath $runKey }
$trayKey = 'HKCU:\Software\CodexQuotaTray-TrayTest-' + [Guid]::NewGuid().ToString('N')
$trayBackup = Join-Path $root 'tray-backup'
New-Item -ItemType Directory -Path $trayBackup | Out-Null
try {
    foreach ($id in @('old','current','unrelated','unverified')) {
        $key = Join-Path $trayKey $id
        New-Item -Path $key -Force | Out-Null
        $path = switch ($id) { 'old' { 'C:\Old\CodexQuotaTray.App.exe' }; 'current' { 'C:\Fixed\CodexQuotaTray.App.exe' }; 'unrelated' { 'C:\Other\Other.exe' }; default { 'C:\Other\CodexQuotaTray.App.exe' } }
        New-ItemProperty -LiteralPath $key -Name ExecutablePath -Value $path | Out-Null
        New-ItemProperty -LiteralPath $key -Name InitialTooltip -Value $(if ($id -eq 'unverified') { 'Other application' } else { 'Codex Quota Tray - Disconnected' }) | Out-Null
        New-ItemProperty -LiteralPath $key -Name IsPromoted -Value 1 -PropertyType DWord | Out-Null
    }
    $promoted = Clear-QuotaTrayHistory $trayKey 'C:\Fixed\CodexQuotaTray.App.exe' $trayBackup
    Check ($promoted -and -not (Test-Path (Join-Path $trayKey 'old'))) 'old identified tray entry removed and visibility retained'
    Check (Test-Path (Join-Path $trayBackup 'old.reg')) 'removed tray entry backed up'
    Check ((Test-Path (Join-Path $trayKey 'current')) -and (Test-Path (Join-Path $trayKey 'unrelated')) -and (Test-Path (Join-Path $trayKey 'unverified'))) 'current and unrelated tray entries preserved'
} finally {
    Get-ChildItem -LiteralPath $trayKey -ErrorAction SilentlyContinue | ForEach-Object { Remove-Item -LiteralPath $_.PSPath }
    Remove-Item -LiteralPath $trayKey
}
Write-Host "$script:checks installer checks passed. Test files: $root"
