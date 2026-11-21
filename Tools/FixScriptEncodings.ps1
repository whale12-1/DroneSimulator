param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

$utf8Strict = New-Object System.Text.UTF8Encoding($false, $true)
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$cp1251 = [System.Text.Encoding]::GetEncoding(1251)

$cp1251Files = @(
    'Assets/Scripts/Managers/TrainingManager.cs',
    'Assets/Scripts/Buildings/TrainingGate.cs',
    'Assets/Scripts/TargetObjectScripts/HimarsStrike.cs',
    'Assets/Scripts/TargetObjectScripts/TargetPingSystem.cs',
    'Assets/Scripts/TargetObjectScripts/VehicleDust.cs',
    'Assets/Scripts/TargetObjectScripts/WheelRotator.cs'
)

$mojibakeFiles = @(
    'Assets/Scripts/TargetObjectScripts/VehicleSmartAI.cs',
    'Assets/Scripts/Core/DamageSystem.cs'
)

$backupDir = Join-Path $ProjectRoot 'Logs/encoding_backup'
if (-not $DryRun -and -not (Test-Path $backupDir)) {
    New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
}

function Test-Utf8([byte[]]$bytes) {
    try {
        [void]$utf8Strict.GetString($bytes)
        return $true
    }
    catch {
        return $false
    }
}

function Save-Result([string]$relative, [string]$text, [byte[]]$originalBytes) {
    $full = Join-Path $ProjectRoot $relative
    $orphan = Join-Path $backupDir (($relative -replace '[\\/]', '_') + '.orig')
    if ($DryRun) {
        Write-Output ("DRYRUN  {0}" -f $relative)
        return
    }

    [System.IO.File]::WriteAllBytes($orphan, $originalBytes)
    [System.IO.File]::WriteAllText($full, $text, $utf8NoBom)
    Write-Output ("FIXED   {0}" -f $relative)
}

Write-Output '== cp1251 -> UTF-8 =='
foreach ($relative in $cp1251Files) {
    $full = Join-Path $ProjectRoot $relative
    if (-not (Test-Path $full)) {
        Write-Output ("MISSING {0}" -f $relative)
        continue
    }

    $bytes = [System.IO.File]::ReadAllBytes($full)
    if (Test-Utf8 $bytes) {
        Write-Output ("SKIP    {0} (already UTF-8)" -f $relative)
        continue
    }

    Save-Result $relative $cp1251.GetString($bytes) $bytes
}

Write-Output '== double-encoded UTF-8 -> UTF-8 =='
foreach ($relative in $mojibakeFiles) {
    $full = Join-Path $ProjectRoot $relative
    if (-not (Test-Path $full)) {
        Write-Output ("MISSING {0}" -f $relative)
        continue
    }

    $bytes = [System.IO.File]::ReadAllBytes($full)
    if (-not (Test-Utf8 $bytes)) {
        Write-Output ("SKIP    {0} (not valid UTF-8, run cp1251 pass first)" -f $relative)
        continue
    }

    $text = $utf8Strict.GetString($bytes)
    $repaired = $utf8NoBom.GetString($cp1251.GetBytes($text))

    if ($repaired -eq $text) {
        Write-Output ("SKIP    {0} (nothing to repair)" -f $relative)
        continue
    }

    if (-not (Test-Utf8 $cp1251.GetBytes($text))) {
        Write-Output ("SKIP    {0} (round-trip is not valid UTF-8)" -f $relative)
        continue
    }

    Save-Result $relative $repaired $bytes
}

Write-Output '== verification =='
$all = $cp1251Files + $mojibakeFiles
foreach ($relative in $all) {
    $full = Join-Path $ProjectRoot $relative
    if (-not (Test-Path $full)) { continue }
    $bytes = [System.IO.File]::ReadAllBytes($full)
    $valid = Test-Utf8 $bytes
    $stats = Get-Content -Path $full -Encoding UTF8 | Where-Object { $_ -match 'Header|//' } | Select-Object -First 1
    Write-Output ("{0} utf8={1} sample={2}" -f $relative, $valid, $stats.Trim())
}
