param([Parameter(Mandatory=$true)][string]$OutputDirectory, [string]$ReplayRoot)
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$seriesOutput = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $seriesOutput -Force | Out-Null
Set-Location -LiteralPath $repoRoot
$seriesState = @{
    status = 'LIMITED_WORKLOAD_ABBA_RUNNING'
    startedUtc = [DateTime]::UtcNow.ToString('o')
    order = @('A','B','B','A')
    ships = 40
    warmupTicks = 7200
    measuredTicksPerRun = 72000
    tickRate = 60
    fullGameplayCoverage = $false
    completedRuns = @()
    collectorPid = $PID
}
$statePath = Join-Path $seriesOutput 'status.json'
function Save-SeriesState { $seriesState | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $statePath -Encoding utf8 }
try {
    Save-SeriesState
    $scenarioPath = Join-Path $seriesOutput 'scenario.jsonl'
    & python (Join-Path $PSScriptRoot 'expanded_scenario.py') --ticks 72000 --output $scenarioPath | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not compile the limited workload.' }
    $seriesState.scenarioSha256 = (Get-FileHash -LiteralPath $scenarioPath -Algorithm SHA256).Hash.ToLowerInvariant()
    Copy-Item -LiteralPath (Join-Path $repoRoot '.kias-benchmark/BASELINE_MANIFEST.json') -Destination (Join-Path $seriesOutput 'baseline-manifest.json')
    for ($index = 0; $index -lt $seriesState.order.Count; $index++) {
        $variant = $seriesState.order[$index]
        $seriesState.currentRun = $index + 1
        $seriesState.currentVariant = $variant
        Save-SeriesState
        Write-Host ("Старт {0}/4 — {1} {2}" -f ($index + 1), $variant, (Get-Date -Format 'HH:mm'))
        $runClock = [Diagnostics.Stopwatch]::StartNew()
        if ($ReplayRoot) {
            $runOutput = Join-Path ([IO.Path]::GetFullPath($ReplayRoot)) ("{0}-{1}" -f ($index + 1), $variant)
            & (Join-Path $PSScriptRoot 'native_replay.ps1') -Variant $variant -Ships 40 -WarmupTicks 7200 -MeasuredTicks 72000 -Scenario $scenarioPath -OutputDirectory $runOutput
        } else {
            $before = @(Get-ChildItem -LiteralPath (Join-Path $repoRoot '.kias-benchmark/replay') -Directory | Select-Object -ExpandProperty FullName)
            & (Join-Path $PSScriptRoot 'native_replay.ps1') -Variant $variant -Ships 40 -WarmupTicks 7200 -MeasuredTicks 72000 -Scenario $scenarioPath
            $created = @(Get-ChildItem -LiteralPath (Join-Path $repoRoot '.kias-benchmark/replay') -Directory | Where-Object { $_.FullName -notin $before })
            if ($created.Count -ne 1) { throw 'Cannot uniquely identify the completed run.' }
            $runOutput = $created[0].FullName
        }
        $seriesState.completedRuns += @{ variant = $variant; path = $runOutput }
        Write-Host ("Готово {0}/4 — {1} {2}, прошло {3:mm\:ss}" -f ($index + 1), $variant, (Get-Date -Format 'HH:mm'), $runClock.Elapsed)
        Save-SeriesState
    }
    & python (Join-Path $PSScriptRoot 'load_report.py') $seriesOutput | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'ABBA evidence validation failed.' }
    $seriesState.status = 'LIMITED_WORKLOAD_ABBA_COMPLETE'
    $seriesState.completedUtc = [DateTime]::UtcNow.ToString('o')
    Save-SeriesState
} catch {
    $seriesState.status = 'FAILED_INCOMPLETE'
    $seriesState.error = $_.Exception.Message
    $seriesState.stoppedUtc = [DateTime]::UtcNow.ToString('o')
    Save-SeriesState
    throw
}
