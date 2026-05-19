# Single-pass runner for re-shooting only (scenes already populated).

$ErrorActionPreference = 'Stop'
$unity   = 'C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe'
$project = 'C:\Users\hvnes\YomawariByoin'
$logDir  = Join-Path $project 'unity_logs'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null

foreach ($f in @('Temp\UnityLockfile', 'Temp\ArtifactDB-lock')) {
    $p = Join-Path $project $f
    if (Test-Path $p) { Remove-Item $p -Force -ErrorAction SilentlyContinue }
}

$log = Join-Path $logDir 'recapture.log'
if (Test-Path $log) { Remove-Item $log -Force }
$unityArgs = @(
    '-batchmode',
    '-projectPath', $project,
    '-executeMethod', 'HospitalPropsAndCharacters.CaptureFour',
    '-logFile', $log,
    '-quit'
)
Write-Host "=== Re-capturing 4 screenshots ===" -ForegroundColor Cyan
$proc = Start-Process -FilePath $unity -ArgumentList $unityArgs -PassThru -Wait
Write-Host "Unity exit code: $($proc.ExitCode)"
if (Test-Path $log) {
    Get-Content $log -Tail 15 | ForEach-Object { Write-Host $_ }
}
exit $proc.ExitCode
