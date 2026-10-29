Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-PlainPath([string]$Path) {
    $cursor = [IO.Path]::GetFullPath($Path)
    while ($cursor) {
        if (Test-Path -LiteralPath $cursor) {
            if ((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked installation paths are not supported.' }
        }
        $parent = Split-Path $cursor -Parent
        if ($parent -eq $cursor) { break }
        $cursor = $parent
    }
}

function Test-QuotaPayload([string]$Payload) {
    $root = [IO.Path]::GetFullPath($Payload).TrimEnd('\') + '\'
    Assert-PlainPath $Payload
    $manifest = Join-Path $root 'FILES-SHA256.txt'
    if (-not (Test-Path -LiteralPath $manifest -PathType Leaf)) { throw 'Application payload is missing. Extract the entire release ZIP, then run Install.cmd from its top-level folder.' }
    $entries = @{}
    foreach ($line in Get-Content -LiteralPath $manifest) {
        if ($line -notmatch '^([a-fA-F0-9]{64})  (.+)$') { throw 'Invalid package manifest.' }
        $hash = $matches[1]; $relative = $matches[2]
        if ([IO.Path]::IsPathRooted($relative) -or $relative.Contains(':') -or $relative -match '(^|[\\/])\.\.([\\/]|$)') { throw 'Unsafe package path.' }
        $path = [IO.Path]::GetFullPath((Join-Path $root $relative))
        if (-not $path.StartsWith($root, [StringComparison]::OrdinalIgnoreCase) -or $entries.ContainsKey($path)) { throw 'Invalid or duplicate package path.' }
        Assert-PlainPath $path
        if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $hash) { throw 'Package checksum mismatch. Download and extract the ZIP again.' }
        $entries[$path] = $true
    }
    foreach ($required in @('CodexQuotaTray.App.exe','CodexQuotaTray.App.dll','coreclr.dll')) {
        if (-not $entries.ContainsKey((Join-Path $root $required))) { throw 'Required application files are missing.' }
    }
    foreach ($file in Get-ChildItem -LiteralPath $root -Recurse -Force) {
        if ($file.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked package files are not supported.' }
        if (-not $file.PSIsContainer -and $file.FullName -ne $manifest -and -not $entries.ContainsKey($file.FullName)) { throw 'Unexpected file in application payload.' }
    }
}

function Install-QuotaFiles([string]$Payload, [string]$Destination) {
    Test-QuotaPayload $Payload
    $destinationFull = [IO.Path]::GetFullPath($Destination).TrimEnd('\')
    $sourceFull = [IO.Path]::GetFullPath($Payload).TrimEnd('\')
    if ($sourceFull -eq $destinationFull -or $sourceFull.StartsWith($destinationFull + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Run the installer from the extracted download, outside the installation folder.' }
    Assert-PlainPath $destinationFull
    $parent = Split-Path $destinationFull -Parent
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
    $stage = Join-Path $parent ('CodexQuotaTray.staging-' + [Guid]::NewGuid().ToString('N'))
    $backup = Join-Path $parent ('CodexQuotaTray.backup-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $stage | Out-Null
    Get-ChildItem -LiteralPath $Payload -Force | Copy-Item -Destination $stage -Recurse
    Test-QuotaPayload $stage
    $hadPrevious = Test-Path -LiteralPath $destinationFull
    if ($hadPrevious) {
        if (-not (Test-Path -LiteralPath (Join-Path $destinationFull 'CodexQuotaTray.App.exe'))) { throw 'The installation folder contains unrelated files; it was not replaced.' }
        Move-Item -LiteralPath $destinationFull -Destination $backup
    }
    try { Move-Item -LiteralPath $stage -Destination $destinationFull }
    catch {
        if ($hadPrevious -and -not (Test-Path -LiteralPath $destinationFull)) { Move-Item -LiteralPath $backup -Destination $destinationFull }
        throw
    }
    if ($hadPrevious) { return $backup }
    return $null
}

function Update-QuotaStartup([string]$RunKey, [string]$Executable) {
    $existing = Get-ItemProperty -LiteralPath $RunKey -Name CodexQuotaTray -ErrorAction SilentlyContinue
    if ($existing -and -not [string]::IsNullOrWhiteSpace($existing.CodexQuotaTray)) {
        New-ItemProperty -LiteralPath $RunKey -Name CodexQuotaTray -Value ('"' + $Executable + '" --startup') -PropertyType String -Force | Out-Null
    }
}

function Clear-QuotaTrayHistory([string]$TrayRoot, [string]$Executable, [string]$BackupRoot) {
    $promoted = $false
    foreach ($key in @(Get-ChildItem -LiteralPath $TrayRoot -ErrorAction SilentlyContinue)) {
        $entry = Get-ItemProperty -LiteralPath $key.PSPath
        if (-not ($entry.PSObject.Properties.Name -contains 'ExecutablePath')) { continue }
        if (-not $entry.ExecutablePath -or [IO.Path]::GetFileName($entry.ExecutablePath) -ine 'CodexQuotaTray.App.exe' -or $entry.ExecutablePath -ieq $Executable) { continue }
        if (-not ($entry.PSObject.Properties.Name -contains 'InitialTooltip')) { continue }
        if ($entry.InitialTooltip -notlike 'Codex Quota Tray*' -and $entry.InitialTooltip -notlike 'Sample data*not your account') { continue }
        if (@(Get-ChildItem -LiteralPath $key.PSPath).Count) { continue }
        & "$env:SystemRoot\System32\reg.exe" export $key.Name (Join-Path $BackupRoot ($key.PSChildName + '.reg')) /y | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'Could not back up a tray entry.' }
        if (($entry.PSObject.Properties.Name -contains 'IsPromoted') -and $entry.IsPromoted -eq 1) { $promoted = $true }
        Remove-Item -LiteralPath $key.PSPath
    }
    return $promoted
}

function Resolve-QuotaPayload([string]$InstallerDirectory) {
    $payload = Join-Path $InstallerDirectory 'app'
    if (-not (Test-Path -LiteralPath $payload)) {
        $repository = Split-Path $InstallerDirectory -Parent
        $builder = Join-Path $repository 'prepare-install.ps1'
        if ((Test-Path -LiteralPath $builder -PathType Leaf) -and (Test-Path -LiteralPath (Join-Path $repository 'src/CodexQuotaTray.App/CodexQuotaTray.App.csproj'))) {
            Write-Host 'Source checkout detected. Building the installation payload...'
            $payload = & $builder
        }
    }
    Test-QuotaPayload $payload
    return $payload
}

Export-ModuleMember -Function Test-QuotaPayload, Install-QuotaFiles, Update-QuotaStartup, Clear-QuotaTrayHistory, Resolve-QuotaPayload

