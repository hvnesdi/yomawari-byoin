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
    Write-Host "=== $method ===" -ForegroundColor Cyan
    $proc = Start-Process -FilePath $unity -ArgumentList $unityArgs -PassThru -Wait
    $script:LastExit = $proc.ExitCode
    Write-Host "Unity exit: $($proc.ExitCode)"
    if (Test-Path $log) {
        Get-Content $log -Tail 40 | ForEach-Object { Write-Host $_ }
    }
}

Run-Pass -method 'HospitalPackArchFix.RunBatch' -logName 'arch_fix_apply.log'
if ($script:LastExit -ne 0) { exit $script:LastExit }
Run-Pass -method 'HospitalPackArchFixCapture.RunBatch' -logName 'arch_fix_capture.log'
exit $script:LastExit
