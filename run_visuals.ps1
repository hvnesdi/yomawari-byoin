# Rebuilds the entire look of the game from source, in dependency order.
#
# run_all.ps1 covers only the passes that make the game RUNNABLE (M1/M2).
# Everything that makes it LOOK like anything lives here. Without this script
# the visual state existed only as saved scene files, so a fresh checkout or a
# reverted scene had no way to get it back. That is the gap this closes.
#
# Every pass is idempotent - re-running is safe and produces the same result.
#
# About 8 minutes when Unity's GI cache is warm. A cold cache (fresh checkout,
# or after deleting Library/) makes the bake 20-30 minutes on its own.
#
# NOTE: Keep this file ASCII-only.
# Windows PowerShell 5.1 reads .ps1 without a BOM as ANSI, so Japanese string
# literals get mangled and break the parser.
#
# ORDER MATTERS. It has bitten us twice, so the reasoning is written down:
#   - Material passes must run after the passes that CREATE those materials.
#     M7 (surfaces) and M8 (colours) touch every material in the project, so
#     they run after M6/M11 have placed their props.
#   - M8 runs last of the material passes because it has the final say on
#     _BaseColor. Running M10 after it would reset ceiling colours.
#   - M9 (bake) must be dead last. It captures light bouncing off the geometry
#     and materials as they finally are; anything moved afterwards is baked
#     into a lie - shadows of objects that are no longer there.
$ErrorActionPreference = 'Stop'

# The pass logs are UTF-8 and mostly Japanese. Without this the console renders
# them as mojibake, which makes the progress output useless to read.
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$unity   = 'C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe'
$project = 'C:\Users\hvnes\YomawariByoin'
$logDir  = Join-Path $project 'unity_logs'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null

function Run-Pass {
    param(
        [Parameter(Mandatory)][string]$method,
        [Parameter(Mandatory)][string]$logName,
        [Parameter(Mandatory)][string]$what,
        [int]$timeoutMinutes = 15
    )

    # Unity leaves these behind when a previous batch run was killed.
    foreach ($f in @('Temp\UnityLockfile', 'Temp\ArtifactDB-lock')) {
        $p = Join-Path $project $f
        if (Test-Path $p) { Remove-Item $p -Force -ErrorAction SilentlyContinue }
    }

    $log = Join-Path $logDir $logName
    if (Test-Path $log) { Remove-Item $log -Force }

    Write-Host ""
    Write-Host "=== $method  ($what) ===" -ForegroundColor Cyan
    $started = Get-Date

    $proc = Start-Process -FilePath $unity -PassThru -ArgumentList @(
        '-batchmode', '-projectPath', $project,
        '-executeMethod', $method,
        '-logFile', $log, '-quit'
    )
    if (-not $proc.WaitForExit($timeoutMinutes * 60 * 1000)) {
        Write-Host "TIMEOUT after $timeoutMinutes min - killing Unity" -ForegroundColor Red
        try { $proc.Kill() } catch {}
        $script:LastExit = 199
        return
    }
    $script:LastExit = $proc.ExitCode
    $mins = [math]::Round(((Get-Date) - $started).TotalMinutes, 1)
    Write-Host "Unity exit: $($proc.ExitCode)  ($mins min)"

    if (Test-Path $log) {
        if (Select-String -Path $log -Pattern 'No valid Unity Editor license' -Quiet) {
            Write-Host "LICENSE ERROR: activate Unity in Unity Hub, then re-run." -ForegroundColor Red
            $script:LastExit = 198
            return
        }
        # Read as UTF-8 explicitly. PowerShell 5.1 assumes the system ANSI
        # codepage for files without a BOM, and Unity writes its log without
        # one, so the Japanese pass output comes out as mojibake otherwise.
        $text = Get-Content -Path $log -Encoding UTF8

        # Compile errors are the common failure and they are silent in the exit
        # code when they happen during a domain reload, so surface them loudly.
        $text | Select-String -Pattern 'error CS|Compilation failed|Exception' |
            Select-Object -First 12 | ForEach-Object { Write-Host $_.Line -ForegroundColor Red }
        $text | Select-String -Pattern '\[M[0-9]+\]|\[Props\]|\[Grime\]|\[Detail\]|\[Bake\]' |
            ForEach-Object { Write-Host "  $($_.Line)" }
    }
}

function Step {
    param([string]$method, [string]$log, [string]$what, [int]$timeout = 15)
    Run-Pass -method $method -logName $log -what $what -timeoutMinutes $timeout
    if ($script:LastExit -ne 0) {
        Write-Host "FAILED at $method - stopping." -ForegroundColor Red
        exit $script:LastExit
    }
}

Write-Host "Rebuilding the look. Expect 30-40 minutes." -ForegroundColor Yellow

# --- Atmosphere and rendering -------------------------------------------------
# Ambient light, fog and per-floor darkness. Also repairs materials that emit
# light when they should not, and albedo values above 1.0 (the original cause
# of the white rectangles on the tiled walls).
Step 'M3AtmospherePass.RunBatch'      'v_m3_atmosphere.log' 'fog, ambient, albedo repair'

# URP asset (HDR, MSAA, SSAO, shadow distance) plus the post-processing volume.
Step 'M5LookPass.RunBatch'            'v_m5_look.log'       'URP settings and grade'

# --- Set dressing -------------------------------------------------------------
# Kills a proportion of the fluorescent tubes so the corridor has a rhythm of
# light and dark. Deeper floors lose more.
Step 'M5SetDressingPass.RunBatch'     'v_m5_dressing.log'   'dead fluorescent tubes'

# Water stains, mould, scratches and blood, placed at heights that suit each.
Step 'M5GrimePass.RunBatch'           'v_m5_grime.log'      'grime decals'

# --- Props (must precede the material passes) ---------------------------------
# Pipes, vents, signs, radiators, skirting - the things that stop a corridor
# reading as an empty box.
Step 'M6CorridorDetailPass.RunBatch'  'v_m6_detail.log'     'corridor fittings'

# CC0 photoscanned props from Poly Haven.
Step 'M11PolyHavenPropsPass.RunBatch' 'v_m11_props.log'     'scanned props'

# --- Materials (order within this block is deliberate) ------------------------
# Normal and roughness maps for anything that has none.
Step 'M7SurfacePass.RunBatch'         'v_m7_surface.log'    'surface detail maps'

# Photoscanned ceiling. Only the ceiling: see the comment in M10 for why the
# walls, floor and metal were all reverted.
Step 'M10RealMaterialsPass.RunBatch'  'v_m10_real.log'      'scanned ceiling'

# Final say on base colours.
Step 'M8PalettePass.RunBatch'         'v_m8_palette.log'    'material colours'

# --- Bake (last, and slow) ----------------------------------------------------
# Four scenes at roughly 5-6 minutes each. Lights are Mixed, not Baked, so the
# flicker still works at runtime while the bounce light is precomputed.
Step 'M9BakedLightingPass.BakeAll'    'v_m9_bake.log'       'lightmap bake' 45

# --- Look at the result -------------------------------------------------------
Step 'HospitalPropsAndCharacters.CaptureFour' 'v_capture.log' 'screenshots'

Write-Host ""
Write-Host "Done. Screenshots are in Screenshots\." -ForegroundColor Green
Write-Host "Now verify the game still runs:" -ForegroundColor Yellow
Write-Host "  powershell -ExecutionPolicy Bypass -File $project\run_playtest.ps1"
Write-Host "(run it separately - chaining it here leaves its log file locked)"
exit 0
