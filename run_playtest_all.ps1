# Plays every floor and saves a gameplay screenshot of each.
#
# run_playtest.ps1 only ever covered 1F. Brightness is scaled per floor (the
# basement is 0.75x), so 1F looking right says nothing about whether the
# basement is pitch black. This runs all four.
#
# Screenshots land in Screenshots\PlayMode_<scene>.png.
#
# NOTE: Keep this file ASCII-only (PowerShell 5.1 reads a BOM-less .ps1 as ANSI).
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$unity   = 'C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe'
$project = 'C:\Users\hvnes\YomawariByoin'
$logDir  = Join-Path $project 'unity_logs'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null

$scenes = @('Hospital', 'Hospital2F', 'Hospital3F', 'HospitalBasement')
$failed = @()

foreach ($scene in $scenes) {
    foreach ($f in @('Temp\UnityLockfile', 'Temp\ArtifactDB-lock')) {
        $p = Join-Path $project $f
        if (Test-Path $p) { Remove-Item $p -Force -ErrorAction SilentlyContinue }
    }

    $log = Join-Path $logDir "play_$scene.log"
    if (Test-Path $log) { Remove-Item $log -Force }

    Write-Host ""
    Write-Host "=== $scene ===" -ForegroundColor Cyan

    # The runner reads this to decide which scene to open.
    $env:SHOUTOU_SCENE = $scene

    # No -quit. With it, Unity terminates the moment RunBatch returns, which is
    # before play mode has ticked even once - the run reports success having
    # checked nothing. PlayModeBatchRunner exits by itself when it is done.
    $proc = Start-Process -FilePath $unity -PassThru -ArgumentList @(
        '-batchmode', '-projectPath', $project,
        '-executeMethod', 'PlayModeBatchRunner.RunBatch',
        '-logFile', $log
    )
    if (-not $proc.WaitForExit(15 * 60 * 1000)) {
        Write-Host "TIMEOUT - killing Unity" -ForegroundColor Red
        try { $proc.Kill() } catch {}
        $failed += $scene
        continue
    }

    if (Test-Path $log) {
        $text = Get-Content -Path $log -Encoding UTF8
        $pass = ($text | Select-String -Pattern '\[PASS\]').Count
        $fail = ($text | Select-String -Pattern '\[FAIL\]').Count
        Write-Host "  PASS $pass / FAIL $fail  (exit $($proc.ExitCode))"
        $text | Select-String -Pattern '\[FAIL\]|error CS' |
            Select-Object -First 8 | ForEach-Object { Write-Host "  $($_.Line)" -ForegroundColor Red }

        # Zero checks is a failure, not a pass. The first version of this script
        # accepted "no FAIL lines" and cheerfully reported all four floors green
        # while every run had exited before play mode ticked once.
        if ($pass -eq 0) {
            Write-Host "  no checks ran at all - treating as failure" -ForegroundColor Red
            $failed += $scene
        }
        elseif ($fail -gt 0 -or $proc.ExitCode -ne 0) { $failed += $scene }
    }
    else {
        Write-Host "  no log produced" -ForegroundColor Red
        $failed += $scene
    }
}

$env:SHOUTOU_SCENE = $null

Write-Host ""
if ($failed.Count -eq 0) {
    Write-Host "All floors passed." -ForegroundColor Green
    exit 0
}
Write-Host "Problems on: $($failed -join ', ')" -ForegroundColor Red
exit 1
