param(
    [ValidateSet('A','B')][string]$Variant,
    [int]$Ships = 40,
    [int]$WarmupTicks = 1800,
    [int]$MeasuredTicks = 600,
    [string]$Scenario = '.kias-benchmark/dryrun-scenario.jsonl',
    [ValidateRange(0.05,1)][float]$DiagnosticTimeScale = 1,
    [string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$source = Join-Path $repo ".kias-benchmark/builds/$Variant"
$manifest = Get-Content -LiteralPath (Join-Path $repo '.kias-benchmark/BASELINE_MANIFEST.json') -Raw | ConvertFrom-Json
$build = $manifest.builds | Where-Object label -eq $Variant
if (-not $build.compiled) { throw "Variant $Variant is not recorded as successfully built." }
$assembly = Join-Path $source 'bin/Content.Server/Content.Server.dll'
if ((Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash.ToLowerInvariant() -ne $build.assemblySha256) { throw 'Assembly differs from the baseline manifest.' }
$mapHash = (Get-FileHash -LiteralPath (Join-Path $source 'Resources/Maps/_Forge/Shuttles/Archive/Mercenary/labBriar.yml') -Algorithm SHA256).Hash.ToLowerInvariant()
$expectedMapHash = if ($Variant -eq 'A') { $manifest.staticParity.aMapSha256 } else { $manifest.staticParity.bMapSha256 }
if ($mapHash -ne $expectedMapHash) { throw 'Replay map differs from the verified static parity manifest.' }
$nativeLabHash = (Get-FileHash -LiteralPath (Join-Path $source 'Tools/KiasBenchmark/Server/NativeLab.cs') -Algorithm SHA256).Hash.ToLowerInvariant()
if ($nativeLabHash -ne $build.nativeLabSha256) { throw 'Native drivers differ from the compiled source manifest.' }
if ($Variant -eq 'B') {
    $telemetryHash = (Get-FileHash -LiteralPath (Join-Path $source 'Tools/KiasBenchmark/Server/KiasTelemetry.cs') -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($telemetryHash -ne $build.telemetrySha256) { throw 'KIAS telemetry differs from the compiled source manifest.' }
}
$output = if ($OutputDirectory) { [IO.Path]::GetFullPath($OutputDirectory) } else { Join-Path $repo ('.kias-benchmark/replay/' + $Variant + '-' + (Get-Date -Format 'yyyyMMdd-HHmmss')) }
New-Item -ItemType Directory -Path $output -Force | Out-Null
$runManifest = @{
    variant = $Variant
    assemblySha256 = $build.assemblySha256
    mapSha256 = $mapHash
    nativeLabSha256 = $nativeLabHash
    telemetrySha256 = if ($Variant -eq 'B') { $telemetryHash } else { $null }
    metricsEnabled = $true
    diagnosticTimeScale = $DiagnosticTimeScale
    performanceEligible = ($DiagnosticTimeScale -eq 1)
}
$runManifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'run-manifest.json') -Encoding UTF8
$oldEnv = @{}
$settings = @{
    NATIVE_LAB_OUTPUT = $output
    NATIVE_LAB_SCENARIO = (Resolve-Path $(if ([IO.Path]::IsPathRooted($Scenario)) { $Scenario } else { Join-Path $repo $Scenario })).Path
    NATIVE_LAB_SHIPS = "$Ships"
    NATIVE_LAB_WARMUP = "$WarmupTicks"
    NATIVE_LAB_TICKS = "$MeasuredTicks"
}
$server = $null
try {
    foreach ($name in $settings.Keys) {
        $oldEnv[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
        [Environment]::SetEnvironmentVariable($name, $settings[$name], 'Process')
    }
    $arguments = @('--data-dir', (Join-Path $output 'data'), '--cvar', 'net.port=12299', '--cvar', 'net.bindto=127.0.0.1', '--cvar', 'net.tickrate=60', '--cvar', 'metrics.enabled=true', '--cvar', 'metrics.host=127.0.0.1', '--cvar', 'metrics.port=44880', '--cvar', 'game.auto_pause_empty=false', '--cvar', 'game.lobbyenabled=true', '--cvar', 'game.lobbyduration=999999', '--cvar', 'auth.mode=0', '+native_lab_start')
    if ($DiagnosticTimeScale -ne 1) { $arguments = @('--cvar', "game.time_scale=$($DiagnosticTimeScale.ToString([Globalization.CultureInfo]::InvariantCulture))") + $arguments }
    $server = Start-Process -FilePath (Join-Path $source 'bin/Content.Server/Content.Server.exe') -WorkingDirectory $source -ArgumentList $arguments -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $output 'server.log') -RedirectStandardError (Join-Path $output 'server-error.log')
    $deadline = (Get-Date).AddSeconds(300 + ($WarmupTicks + $MeasuredTicks) / 30)
    $completed = Join-Path $output 'completed.json'
    $failed = Join-Path $output 'failed.json'
    $lastScrapeTick = -1
    while (-not (Test-Path -LiteralPath $completed) -and -not (Test-Path -LiteralPath $failed) -and -not $server.HasExited -and (Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 2
        $server.Refresh()
        $progressPath = Join-Path $output 'progress.json'
        if (Test-Path -LiteralPath $progressPath) {
            $progress = Get-Content -LiteralPath $progressPath -Raw | ConvertFrom-Json
            if ($progress.relativeTick -ne $lastScrapeTick) {
                $lastScrapeTick = $progress.relativeTick
                Invoke-WebRequest -Uri 'http://127.0.0.1:44880/metrics' -UseBasicParsing -TimeoutSec 10 -OutFile (Join-Path $output "metrics-$lastScrapeTick.prom") | Out-Null
                if ($Variant -eq 'B' -and $progress.telemetry.activeGrids -ne $Ships) { throw "KIAS fleet lost availability at tick $lastScrapeTick; active grids: $($progress.telemetry.activeGrids). Result is incomplete." }
                if ($Variant -eq 'B' -and ($progress.telemetry.activeMachines -ne (26 * $Ships) -or $progress.telemetry.faultedCards -ne 0)) { throw "KIAS machines stopped or faulted at tick $lastScrapeTick; see $progressPath." }
                if ($Variant -eq 'B' -and $progress.telemetry.runningCards -ne (26 * $Ships) -and -not $progress.telemetry.availabilityPending) { throw "KIAS cards unavailable without a pending topology rebuild at tick $lastScrapeTick; see $progressPath." }
            }
        }
    }
    if (Test-Path -LiteralPath $failed) { throw "Native replay failed: $(Get-Content -LiteralPath $failed -Raw)" }
    if (-not (Test-Path -LiteralPath $completed)) { throw "Native replay did not complete. See $output/server.log" }
    Invoke-WebRequest -Uri 'http://127.0.0.1:44880/metrics' -UseBasicParsing -TimeoutSec 10 -OutFile (Join-Path $output 'metrics-final.prom') | Out-Null
    $errors = Select-String -LiteralPath (Join-Path $output 'server.log') -Pattern '\[ERRO\]|\[FATL\]'
    if ($errors) { throw "Native replay logged server errors. Results are invalid: $output/server.log" }
    Write-Host "Limited native replay completed: $output"
} finally {
    if ($null -ne $server -and -not $server.HasExited) { Stop-Process -Id $server.Id }
    foreach ($name in $oldEnv.Keys) { [Environment]::SetEnvironmentVariable($name, $oldEnv[$name], 'Process') }
}
