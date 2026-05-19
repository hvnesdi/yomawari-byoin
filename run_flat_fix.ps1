# Two-pass Unity runner for the flat-plaster + linoleum fix.
# Pass 1: HospitalFlatFix.RunBatch       (rewrite materials & 2F lighting)
# Pass 2: HospitalFlatFix.CaptureTwoShots (2F corridor + 1F patient room PNGs)

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
    param(
        [Parameter(Mandatory)][string]$method,
        [Parameter(Mandatory)][string]$logName
    )
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
    Write-Host "  Unity exit code: $($proc.ExitCode)" -ForegroundColor Cyan
    if (Test-Path $log) {
        Write-Host "--- last 20 lines of $logName ---" -ForegroundColor DarkGray
        Get-Content $log -Tail 20 | ForEach-Object { Write-Host $_ }
        Write-Host "--- end tail ---" -ForegroundColor DarkGray
    }
}

Run-Pass -method 'HospitalFlatFix.RunBatch' -logName 'flat_pass1.log'
if ($script:LastExit -ne 0) { Write-Host "Pass 1 failed ($script:LastExit)" -ForegroundColor Red; exit $script:LastExit }

Run-Pass -method 'HospitalFlatFix.CaptureTwoShots' -logName 'flat_pass2.log'
if ($script:LastExit -ne 0) { Write-Host "Pass 2 failed ($script:LastExit)" -ForegroundColor Red; exit $script:LastExit }

Write-Host "=== Both passes complete ===" -ForegroundColor Green
