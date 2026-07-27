# NOTE: Keep this file ASCII-only (see run_m1_setup.ps1 for why).
$ErrorActionPreference = 'Stop'
$unity   = 'C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe'
$project = 'C:\Users\hvnes\YomawariByoin'
$logDir  = Join-Path $project 'unity_logs'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null

foreach ($f in @('Temp\UnityLockfile', 'Temp\ArtifactDB-lock')) {
    $p = Join-Path $project $f
    if (Test-Path $p) { Remove-Item $p -Force -ErrorAction SilentlyContinue }
}

$log = Join-Path $logDir 'm2_content.log'
if (Test-Path $log) { Remove-Item $log -Force }

Write-Host "=== M2ContentFixer.RunBatch ===" -ForegroundColor Cyan
$proc = Start-Process -FilePath $unity -PassThru -Wait -ArgumentList @(
    '-batchmode', '-projectPath', $project,
    '-executeMethod', 'M2ContentFixer.RunBatch',
    '-logFile', $log, '-quit'
)
Write-Host "Unity exit: $($proc.ExitCode)"

if (Test-Path $log) {
    if (Select-String -Path $log -Pattern 'No valid Unity Editor license' -Quiet) {
        Write-Host "LICENSE ERROR: activate Unity in Unity Hub, then re-run." -ForegroundColor Red
        exit 198
    }
    Select-String -Path $log -Pattern 'error CS|Compilation failed|\[M2ContentFixer\]|Exception|Exiting batchmode' |
        ForEach-Object { Write-Host $_.Line }
}
exit $proc.ExitCode
