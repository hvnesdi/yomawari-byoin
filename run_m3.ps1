# M3: diagnose, then apply the atmosphere pass. NOTE: Keep this file ASCII-only.
$ErrorActionPreference = 'Stop'
$unity   = 'C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe'
$project = 'C:\Users\hvnes\YomawariByoin'
$logDir  = Join-Path $project 'unity_logs'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null

function Run-Pass {
    param([Parameter(Mandatory)][string]$method, [Parameter(Mandatory)][string]$logName,
          [Parameter(Mandatory)][string]$pattern)

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
        Select-String -Path $log -Pattern $pattern | ForEach-Object { Write-Host $_.Line }
    }
}

Run-Pass -method 'VisualDiagnostics.RunBatch' -logName 'diagnose.log' `
         -pattern 'error CS|##########|\[起動直後|\[白|\[ライト|\[マテリアル|^\s{4}\d'
if ($script:LastExit -ne 0) { exit $script:LastExit }

Run-Pass -method 'M3AtmospherePass.RunBatch' -logName 'm3_atmosphere.log' `
         -pattern 'error CS|\[M3AtmospherePass\]|Exception'
exit $script:LastExit
