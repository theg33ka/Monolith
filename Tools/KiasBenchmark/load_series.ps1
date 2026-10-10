param([Parameter(Mandatory)][string]$OutputDirectory,[Parameter(Mandatory)][string]$ReplayRoot,
    [Parameter(Mandatory)][string]$ManifestPath,[int]$Ships=40,[int]$WarmupTicks=10800,
    [int]$MeasuredTicks=108000,[int]$Seed=20261010,[switch]$Quick,[switch]$Legacy,[switch]$FullGame)
$ErrorActionPreference='Stop'
if ($FullGame -and ($Legacy -or $Quick)) { throw 'FullGame cannot be combined with Legacy or Quick.' }
$repoRoot=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$seriesOutput=[IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $seriesOutput) { throw 'Каталог серии уже существует.' }
New-Item -ItemType Directory -Path $seriesOutput | Out-Null
Set-Location -LiteralPath $repoRoot
$seriesState=@{status='LIMITED_WORKLOAD_ABBA_RUNNING';startedUtc=[DateTime]::UtcNow.ToString('o');order=@('A','B','B','A');ships=$Ships;warmupTicks=$WarmupTicks;measuredTicksPerRun=$MeasuredTicks;tickRate=60;seed=$Seed;fullGameplayCoverage=$false;completedRuns=@();collectorPid=$PID;quick=[bool]$Quick;legacy=[bool]$Legacy}
$seriesState.fullGame=[bool]$FullGame
if ($FullGame) { $seriesState.status='NATIVE_FULLGAME_ABBA_RUNNING' }
$statePath=Join-Path $seriesOutput 'status.json'
function Save-SeriesState {
    $seriesState | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath ($statePath+'.tmp') -Encoding utf8
    Move-Item -LiteralPath ($statePath+'.tmp') -Destination $statePath -Force
}
try {
    Save-SeriesState
    $scenarioPath=Join-Path $seriesOutput 'scenario.jsonl'
    if ($FullGame) { & python (Join-Path $PSScriptRoot 'fullgame_scenario.py') --ticks $MeasuredTicks --ships $Ships --seed $Seed --output $scenarioPath }
    elseif ($Legacy) { & python (Join-Path $PSScriptRoot 'expanded_scenario.py') --ticks $MeasuredTicks --output $scenarioPath }
    else { & python (Join-Path $PSScriptRoot 'wave_scenario.py') --ticks $MeasuredTicks --ships $Ships --seed $Seed --output $scenarioPath }
    if ($LASTEXITCODE -ne 0) { throw 'Не удалось подготовить сценарий.' }
    $seriesState.scenarioSha256=(Get-FileHash -LiteralPath $scenarioPath).Hash.ToLowerInvariant()
    Copy-Item -LiteralPath $ManifestPath -Destination (Join-Path $seriesOutput 'baseline-manifest.json')
    for ($index=0;$index -lt 4;$index++) {
        $variant=$seriesState.order[$index]
        $seriesState.currentRun=$index+1
        $seriesState.currentVariant=$variant
        Save-SeriesState
        Write-Host ("Старт {0}/4 — {1} {2}" -f ($index+1),$variant,(Get-Date -Format 'HH:mm:ss'))
        $clock=[Diagnostics.Stopwatch]::StartNew()
        $runOutput=Join-Path ([IO.Path]::GetFullPath($ReplayRoot)) ("{0}-{1}" -f ($index+1),$variant)
        $taskNetworkClients=if ($FullGame) {[Math]::Min(4,$Ships)} else {0}
        & (Join-Path $PSScriptRoot 'native_replay.ps1') -Variant $variant -Ships $Ships -WarmupTicks $WarmupTicks -MeasuredTicks $MeasuredTicks -Scenario $scenarioPath -OutputDirectory $runOutput -ManifestPath $ManifestPath -Seed $Seed -KeepNativeNpcAwake:$FullGame -NetworkClients $taskNetworkClients
        if ($FullGame) {
            & python (Join-Path $PSScriptRoot 'native_proof.py') $runOutput $scenarioPath *> (Join-Path $runOutput 'native-proof.json')
            if ($LASTEXITCODE -ne 0) { throw "Native evidence integrity failed for run $($index+1)." }
        }
        $seriesState.completedRuns+=@{variant=$variant;path=$runOutput}
        Write-Host ("Готово {0}/4 — {1} {2}, прошло {3:mm\:ss}" -f ($index+1),$variant,(Get-Date -Format 'HH:mm:ss'),$clock.Elapsed)
        Save-SeriesState
    }
    $taskReporter=if ($FullGame) {'fullgame_report.py'} else {'load_report.py'}
    & python (Join-Path $PSScriptRoot $taskReporter) $seriesOutput
    if ($LASTEXITCODE -ne 0) { throw 'Проверка данных ABBA не прошла.' }
    $seriesState.status=if ($FullGame) {'NATIVE_FULLGAME_ABBA_COMPLETE'} else {'LIMITED_WORKLOAD_ABBA_COMPLETE'}
    $seriesState.completedUtc=[DateTime]::UtcNow.ToString('o')
    Save-SeriesState
} catch {
    $seriesState.status='FAILED_INCOMPLETE'
    $seriesState.error=$_.Exception.Message
    $seriesState.stoppedUtc=[DateTime]::UtcNow.ToString('o')
    Save-SeriesState
    throw
}
