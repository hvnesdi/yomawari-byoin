# Primary entry point. Runs every setup pass in order, then validates.
# All passes are idempotent, so re-running is safe.
#
# NOTE: Keep this file ASCII-only.
# Windows PowerShell 5.1 reads .ps1 without a BOM as ANSI, so Japanese string
# literals get mangled and break the parser.
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
        '-executeMethod', $method,
        '-logFile', $log, '-quit'
    )
    $script:LastExit = $proc.ExitCode
    Write-Host "Unity exit: $($proc.ExitCode)"

    if (Test-Path $log) {
        if (Select-String -Path $log -Pattern 'No valid Unity Editor license' -Quiet) {
            Write-Host "LICENSE ERROR: activate Unity in Unity Hub, then re-run." -ForegroundColor Red
            $script:LastExit = 198
            return
        }
        Select-String -Path $log -Pattern 'error CS|Compilation failed|\[GameBootstrapBuilder\]|\[SceneWiringFixer\]|\[M2ContentFixer\]|PASS\]|FAIL\]|Exception|Exiting batchmode' |
            ForEach-Object { Write-Host $_.Line }
    }
}

Run-Pass -method 'GameBootstrapBuilder.RunBatch' -logName 'm1_bootstrap.log'
if ($script:LastExit -ne 0) { exit $script:LastExit }

Run-Pass -method 'SceneWiringFixer.RunBatch' -logName 'm1_scenes.log'
if ($script:LastExit -ne 0) { exit $script:LastExit }

Run-Pass -method 'M2ContentFixer.RunBatch' -logName 'm2_content.log'
if ($script:LastExit -ne 0) { exit $script:LastExit }

# M1Validator exits 1 when any check fails
Run-Pass -method 'M1Validator.RunBatch' -logName 'm1_validate.log'
exit $script:LastExit
