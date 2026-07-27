# Enters Play mode in batch mode and captures the PlayModeSelfCheck report.
# NOTE: Keep this file ASCII-only (PowerShell 5.1 reads BOM-less .ps1 as ANSI).
#
# No -quit here: PlayModeBatchRunner exits by itself once play mode has run.
$ErrorActionPreference = 'Stop'
$unity   = 'C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe'
$project = 'C:\Users\hvnes\YomawariByoin'
$logDir  = Join-Path $project 'unity_logs'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null

foreach ($f in @('Temp\UnityLockfile', 'Temp\ArtifactDB-lock')) {
    $p = Join-Path $project $f
    if (Test-Path $p) { Remove-Item $p -Force -ErrorAction SilentlyContinue }
}

$log = Join-Path $logDir 'playtest.log'
if (Test-Path $log) { Remove-Item $log -Force }

Write-Host "=== PlayModeBatchRunner.RunBatch ===" -ForegroundColor Cyan
$proc = Start-Process -FilePath $unity -PassThru -ArgumentList @(
    '-batchmode', '-projectPath', $project,
    '-executeMethod', 'PlayModeBatchRunner.RunBatch',
    '-logFile', $log
)

# Hard timeout so a stuck play mode cannot hang forever
if (-not $proc.WaitForExit(420000)) {
    Write-Host "TIMEOUT: killing Unity after 7 minutes" -ForegroundColor Red
    try { $proc.Kill() } catch {}
    Start-Sleep -Seconds 3
}
Write-Host "Unity exit: $($proc.ExitCode)"

if (Test-Path $log) {
    if (Select-String -Path $log -Pattern 'No valid Unity Editor license' -Quiet) {
        Write-Host "LICENSE ERROR: activate Unity in Unity Hub, then re-run." -ForegroundColor Red
        exit 198
    }
    Write-Host "--- selfcheck / runtime errors ---"
    Select-String -Path $log -Pattern 'PASS\]|FAIL\]|SelfCheck|PlayModeBatchRunner|SystemsBootstrap|JapaneseFont|GameManager\]|NullReferenceException|InvalidOperationException|error CS' |
        ForEach-Object { Write-Host $_.Line }
}
exit $proc.ExitCode
