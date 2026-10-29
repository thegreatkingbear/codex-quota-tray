$ErrorActionPreference = 'Stop'
$installLock = $null
try {
    Import-Module (Join-Path $PSScriptRoot 'Install.Core.psm1') -Force
    if (-not [Environment]::Is64BitOperatingSystem -or $env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { throw 'This package supports Windows x64 only.' }
    $destination = Join-Path $env:LOCALAPPDATA 'Programs\CodexQuotaTray'
    $exe = Join-Path $destination 'CodexQuotaTray.App.exe'
    Write-Host "Install/update location: $destination"
    Write-Host 'Existing preferences and your startup choice will be preserved.'
    Write-Host 'Old Quota Tray notification entries will be backed up and cleaned up.'
    Write-Host 'No downloads, administrator access, or Windows policy changes are required.'
    if ((Read-Host 'Continue? [Y/N]') -notmatch '^(y|yes)$') { exit 0 }
    $settingsFolder = Join-Path $env:LOCALAPPDATA 'CodexQuotaTray'
    New-Item -ItemType Directory -Path $settingsFolder -Force | Out-Null
    $installLock = [IO.File]::Open((Join-Path $settingsFolder 'install.lock'), [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    while (@(Get-Process CodexQuotaTray.App -ErrorAction SilentlyContinue).Count) {
        Write-Host 'Choose Exit in the existing Quota Tray menu before continuing.'
        if ((Read-Host 'Press Enter after exiting, or type Q to cancel') -match '^[qQ]$') { exit 0 }
    }
    $payload = Join-Path $PSScriptRoot 'app'
    Test-QuotaPayload $payload
    $runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
    $backupRoot = Join-Path $env:LOCALAPPDATA ('CodexQuotaTray\maintenance-backups\install-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null
    $oldRun = Get-ItemProperty -LiteralPath $runKey -Name CodexQuotaTray -ErrorAction SilentlyContinue
    @{Value=$(if ($oldRun) { $oldRun.CodexQuotaTray } else { $null })} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $backupRoot 'startup.json') -Encoding UTF8
    $previous = Install-QuotaFiles $payload $destination
    Update-QuotaStartup $runKey $exe
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut((Join-Path ([Environment]::GetFolderPath('Programs')) 'Codex Quota Tray.lnk'))
    $shortcut.TargetPath = $exe; $shortcut.WorkingDirectory = $destination; $shortcut.Save()
    $promoted = $false
    $trayRoot = 'HKCU:\Control Panel\NotifyIconSettings'
    try { $promoted = Clear-QuotaTrayHistory $trayRoot $exe $backupRoot }
    catch { Write-Warning 'Installed successfully, but some old tray entries could not be cleaned up.' }
    Start-Process -FilePath $exe -WindowStyle Hidden
    if ($promoted) {
        for ($attempt = 0; $attempt -lt 10; $attempt++) {
            Start-Sleep -Milliseconds 500
            $current = @(Get-ChildItem -LiteralPath $trayRoot -ErrorAction SilentlyContinue | Where-Object { (Get-ItemProperty -LiteralPath $_.PSPath).ExecutablePath -ieq $exe })
            if ($current.Count) {
                foreach ($key in $current) { New-ItemProperty -LiteralPath $key.PSPath -Name IsPromoted -Value 1 -PropertyType DWord -Force | Out-Null }
                break
            }
        }
    }
    Write-Host 'Installation complete. Open Codex Quota Tray from the Start menu.'
    Write-Host 'Future updates: exit the app, extract the new ZIP, and run Install.cmd again.'
    Write-Host "Registry backup: $backupRoot"
    if ($previous) { Write-Host "Previous application files retained at: $previous" }
} catch { Write-Host ('Installation failed: ' + $_.Exception.Message) -ForegroundColor Red; exit 1 }
finally { if ($installLock) { $installLock.Dispose() } }

