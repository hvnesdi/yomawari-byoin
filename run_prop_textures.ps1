# Builds materials for the scanned props, re-places them, rebakes, captures.
#
# The props were placed with no materials at all for a while - they rendered as
# flat coloured blocks, which is the opposite of the reason for using scanned
# models. M12 wires the downloaded textures up; M11 re-runs so the instances
# pick up the fallback path for models with no material slots.
#
# NOTE: Keep this file ASCII-only (PowerShell 5.1 reads a BOM-less .ps1 as ANSI).
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$unity   = 'C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe'
$project = 'C:\Users\hvnes\YomawariByoin'
$logDir  = Join-Path $project 'unity_logs'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null

function Step {
    param([string]$method, [string]$log, [int]$timeout = 45)

    foreach ($f in @('Temp\UnityLockfile', 'Temp\ArtifactDB-lock')) {
        $p = Join-Path $project $f
        if (Test-Path $p) { Remove-Item $p -Force -ErrorAction SilentlyContinue }
    }
    $logPath = Join-Path $logDir $log
    if (Test-Path $logPath) { Remove-Item $logPath -Force }

    Write-Host ""
    Write-Host "=== $method ===" -ForegroundColor Cyan
    $started = Get-Date
    $proc = Start-Process -FilePath $unity -PassThru -ArgumentList @(
        '-batchmode', '-projectPath', $project,
        '-executeMethod', $method, '-logFile', $logPath, '-quit'
    )
    if (-not $proc.WaitForExit($timeout * 60 * 1000)) {
        Write-Host "TIMEOUT - killing Unity" -ForegroundColor Red
        try { $proc.Kill() } catch {}
        exit 199
    }
    $mins = [math]::Round(((Get-Date) - $started).TotalMinutes, 1)
    Write-Host "Unity exit: $($proc.ExitCode)  ($mins min)"

    if (Test-Path $logPath) {
        $text = Get-Content -Path $logPath -Encoding UTF8
        $text | Select-String -Pattern 'error CS|Compilation failed|Exception' |
            Select-Object -First 12 | ForEach-Object { Write-Host $_.Line -ForegroundColor Red }
        # Match on ASCII markers only - this file must stay ASCII-only, so the
        # pattern cannot contain the Japanese words that appear in the log.
        $text | Select-String -Pattern 'PH_|MS\.png|\[M12\]|\[Props\]|\[Bake\]|\?' |
            ForEach-Object { Write-Host "  $($_.Line)" }
    }
    if ($proc.ExitCode -ne 0) { Write-Host "FAILED at $method" -ForegroundColor Red; exit $proc.ExitCode }
}

Step 'M12ScannedPropMaterialsPass.RunBatch' 'p_m12_mats.log'
Step 'M11PolyHavenPropsPass.RunBatch'       'p_m11_props.log'
Step 'M9BakedLightingPass.BakeAll'          'p_m9_bake.log'
Step 'HospitalPropsAndCharacters.CaptureFour' 'p_capture.log'

Write-Host ""
Write-Host "Done." -ForegroundColor Green
exit 0
