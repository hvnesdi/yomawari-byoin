# Visual diagnostics pass. NOTE: Keep this file ASCII-only.
$ErrorActionPreference = 'Stop'
$unity   = 'C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe'
$project = 'C:\Users\hvnes\YomawariByoin'
$logDir  = Join-Path $project 'unity_logs'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null

foreach ($f in @('Temp\UnityLockfile', 'Temp\ArtifactDB-lock')) {
    $p = Join-Path $project $f
    if (Test-Path $p) { Remove-Item $p -Force -ErrorAction SilentlyContinue }
}

$log = Join-Path $logDir 'diagnose.log'
if (Test-Path $log) { Remove-Item $log -Force }

$proc = Start-Process -FilePath $unity -PassThru -Wait -ArgumentList @(
    '-batchmode', '-projectPath', $project,
    '-executeMethod', 'VisualDiagnostics.RunBatch',
    '-logFile', $log, '-quit'
)
Write-Host "Unity exit: $($proc.ExitCode)"
if (Test-Path $log) {
    Select-String -Path $log -Pattern 'error CS|##########|\[白|\[ライト|\[マテリアル|^\s{4}' |
        ForEach-Object { Write-Host $_.Line }
}
exit $proc.ExitCode
