# Runs M10 twice and reports whether the ceiling tiling changed between runs.
#
# M10 used to multiply the existing tiling every time it ran, so the ceiling got
# twice as finely tiled on each pass (16 -> 32 -> 64). The pass claimed to be
# idempotent and was not. This script is the check that would have caught it.
#
# Run it after touching anything in M10 that reads a material's current value.
#
# NOTE: Keep this file ASCII-only (PowerShell 5.1 reads a BOM-less .ps1 as ANSI).
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$unity   = 'C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe'
$project = 'C:\Users\hvnes\YomawariByoin'
$logDir  = Join-Path $project 'unity_logs'
$matPath = Join-Path $project 'Assets\Materials\Mat_Ceiling_Bright.mat'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null

function Get-Tiling {
    # The _BaseMap scale sits two lines below the property name in the YAML.
    $lines = Get-Content -Path $matPath
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '- _BaseMap:') {
            for ($j = $i; $j -lt [Math]::Min($i + 4, $lines.Count); $j++) {
                if ($lines[$j] -match 'm_Scale: \{x: ([0-9.]+)') { return $Matches[1] }
            }
        }
    }
    return 'not found'
}

function Invoke-M10 {
    param([string]$log)
    foreach ($f in @('Temp\UnityLockfile', 'Temp\ArtifactDB-lock')) {
        $p = Join-Path $project $f
        if (Test-Path $p) { Remove-Item $p -Force -ErrorAction SilentlyContinue }
    }
    $logPath = Join-Path $logDir $log
    $proc = Start-Process -FilePath $unity -PassThru -Wait -ArgumentList @(
        '-batchmode', '-projectPath', $project,
        '-executeMethod', 'M10RealMaterialsPass.RunBatch',
        '-logFile', $logPath, '-quit'
    )
    if ($proc.ExitCode -ne 0) {
        Write-Host "M10 failed (exit $($proc.ExitCode)) - see $logPath" -ForegroundColor Red
        Get-Content -Path $logPath -Encoding UTF8 |
            Select-String -Pattern 'error CS|Exception' | Select-Object -First 8 |
            ForEach-Object { Write-Host $_.Line -ForegroundColor Red }
        exit $proc.ExitCode
    }
}

$before = Get-Tiling
Write-Host "tiling before      : $before"

Invoke-M10 'chk_m10_first.log'
$first = Get-Tiling
Write-Host "tiling after run 1 : $first"

Invoke-M10 'chk_m10_second.log'
$second = Get-Tiling
Write-Host "tiling after run 2 : $second"

if ($first -eq $second) {
    Write-Host "PASS - M10 is idempotent (tiling stable at $second)" -ForegroundColor Green
    exit 0
}
Write-Host "FAIL - tiling changed between runs ($first -> $second)" -ForegroundColor Red
exit 1
