# Two-pass runner: apply PBR Hospital Pack + capture 5 screenshots.

$ErrorActionPreference = 'Stop'
$unity   = 'C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe'
$project = 'C:\Users\hvnes\YomawariByoin'
$logDir  = Join-Path $project 'unity_logs'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null

function Clean-Locks {
    foreach ($f in @('Temp\UnityLockfile', 'Temp\ArtifactDB-lock')) {
        $p = Join-Path $project $f
        if (Test-Path $p) { Remove-Item $p -Force -ErrorAction SilentlyContinue }
    }
}

function Run-Pass {
    param([Parameter(Mandatory)][string]$method, [Parameter(Mandatory)][string]$logName)
    Clean-Locks
    $log = Join-Path $logDir $logName
    if (Test-Path $log) { Remove-Item $log -Force }
    $unityArgs = @(
        '-batchmode',
        '-projectPath', $project,
        '-executeMethod', $method,
        '-logFile', $log,
        '-quit'
    )
    Write-Host "=== Running: $method ===" -ForegroundColor Cyan
    $proc = Start-Process -FilePath $unity -ArgumentList $unityArgs -PassThru -Wait
    $script:LastExit = $proc.ExitCode
    Write-Host "Unity exit code: $($proc.ExitCode)" -ForegroundColor Cyan
    if (Test-Path $log) {
        Write-Host "--- last 25 lines ---" -ForegroundColor DarkGray
        Get-Content $log -Tail 25 | ForEach-Object { Write-Host $_ }
    }
}

Run-Pass -method 'HospitalPackApplier.RunBatch' -logName 'pack_apply.log'
if ($script:LastExit -ne 0) { Write-Host "Apply failed ($script:LastExit)" -ForegroundColor Red; exit $script:LastExit }

Run-Pass -method 'HospitalPackApplier.CaptureFive' -logName 'pack_capture.log'
if ($script:LastExit -ne 0) { Write-Host "Capture failed ($script:LastExit)" -ForegroundColor Red; exit $script:LastExit }

Write-Host "=== Both passes complete ===" -ForegroundColor Green
