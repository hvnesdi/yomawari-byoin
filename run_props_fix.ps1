# Re-runs the two prop passes after the spacing fix, then rebakes.
#
# The bake HAS to follow: it burns the position of every static object into the
# lightmaps, so moving props and not rebaking leaves shadows of objects that
# are no longer there.
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
        $text | Select-String -Pattern '\[Props\]|\[Detail\]|\[Bake\]' |
            ForEach-Object { Write-Host "  $($_.Line)" }
    }
    if ($proc.ExitCode -ne 0) { Write-Host "FAILED at $method" -ForegroundColor Red; exit $proc.ExitCode }
}

Step 'M6CorridorDetailPass.RunBatch'  'f_m6_detail.log'
Step 'M11PolyHavenPropsPass.RunBatch' 'f_m11_props.log'
Step 'M9BakedLightingPass.BakeAll'    'f_m9_bake.log'
Step 'HospitalPropsAndCharacters.CaptureFour' 'f_capture.log'

Write-Host ""
Write-Host "Done." -ForegroundColor Green
exit 0
