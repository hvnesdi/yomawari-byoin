# Rebuild character models, then capture the showcase shots. ASCII-only.
$ErrorActionPreference = 'Stop'
$unity   = 'C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe'
$project = 'C:\Users\hvnes\YomawariByoin'
$logDir  = Join-Path $project 'unity_logs'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null

function Run-Pass {
    param([Parameter(Mandatory)][string]$method, [Parameter(Mandatory)][string]$logName)
    foreach ($f in @('Temp\UnityLockfile', 'Temp\ArtifactDB-lock')) {
        $p = Join-Path $project $f
        if (Test-Path $p) { Remove-Item $p -Force -ErrorAction SilentlyContinue }
    }
    $log = Join-Path $logDir $logName
    if (Test-Path $log) { Remove-Item $log -Force }

    Write-Host "=== $method ===" -ForegroundColor Cyan
    $proc = Start-Process -FilePath $unity -PassThru -Wait -ArgumentList @(
        '-batchmode', '-projectPath', $project,
        '-executeMethod', $method, '-logFile', $log, '-quit'
    )
    $script:LastExit = $proc.ExitCode
    Write-Host "Unity exit: $($proc.ExitCode)"
    if (Test-Path $log) {
        Select-String -Path $log -Pattern 'error CS|Compilation failed|\[Characters\]|Screenshot saved|Exception' |
            ForEach-Object { Write-Host $_.Line }
    }
}

Run-Pass -method 'HospitalPropsAndCharacters.RebuildCharactersBatch' -logName 'characters.log'
if ($script:LastExit -ne 0) { exit $script:LastExit }

Run-Pass -method 'HospitalPropsAndCharacters.CaptureFour' -logName 'characters_capture.log'
exit $script:LastExit
