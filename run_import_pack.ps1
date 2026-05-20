# Imports the PBR Hospital Horror Pack Free into the project.

$ErrorActionPreference = 'Stop'
$unity   = 'C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe'
$project = 'C:\Users\hvnes\YomawariByoin'
$pkg     = 'C:\Users\hvnes\YomawariByoin\hospital_pack.unitypackage'
$logDir  = Join-Path $project 'unity_logs'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null

foreach ($f in @('Temp\UnityLockfile', 'Temp\ArtifactDB-lock')) {
    $p = Join-Path $project $f
    if (Test-Path $p) { Remove-Item $p -Force -ErrorAction SilentlyContinue }
}

$log = Join-Path $logDir 'import_pack.log'
if (Test-Path $log) { Remove-Item $log -Force }
$unityArgs = @(
    '-batchmode',
    '-projectPath', $project,
    '-executeMethod', 'PackageImporter.ImportHospitalPack',
    '-logFile', $log
)
Write-Host "=== Importing PBR Hospital Horror Pack ===" -ForegroundColor Cyan
$proc = Start-Process -FilePath $unity -ArgumentList $unityArgs -PassThru -Wait
Write-Host "Unity exit code: $($proc.ExitCode)" -ForegroundColor Cyan
if (Test-Path $log) {
    Write-Host "--- last 30 lines ---" -ForegroundColor DarkGray
    Get-Content $log -Tail 30 | ForEach-Object { Write-Host $_ }
}
exit $proc.ExitCode
