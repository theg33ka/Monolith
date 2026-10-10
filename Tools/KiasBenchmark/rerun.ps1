param([switch]$CheckOnly, [switch]$Quick, [switch]$Legacy, [switch]$FullGame,
    [ValidateRange(1,40)][int]$Ships=40, [ValidateRange(2,120)][int]$Minutes=30, [int]$Seed=20261010)
$ErrorActionPreference='Stop'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
Set-Location -LiteralPath $repo
$bench=Join-Path $repo '.kias-benchmark'
$pointer=Join-Path $bench 'CURRENT_PAIR.txt'
$lock=$null
$current=$null
$exitCode=0
$taskTranscript=$false
function Set-LauncherStage([int]$Number,[string]$Name) {
    Write-Host "ЭТАП $Number/7: $Name | общая ETA: вычисляется по завершённым этапам"
    @{stage=$Number;stages=7;name=$Name;updatedUtc=[DateTime]::UtcNow.ToString('o');status='RUNNING'} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $current 'launcher-stage.tmp') -Encoding utf8
    Move-Item -LiteralPath (Join-Path $current 'launcher-stage.tmp') -Destination (Join-Path $current 'launcher-stage.json') -Force
}
try {
    if (($Quick -and $Legacy) -or ($FullGame -and ($Quick -or $Legacy))) { throw 'Выберите один режим: FullGame, Quick или Legacy.' }
    Get-Command python,dotnet -ErrorAction Stop | Out-Null
    & python -c 'import sys; assert sys.version_info >= (3,10)'
    if ($LASTEXITCODE -ne 0) { throw 'Нужен Python 3.10+.' }
    $taskReadinessExit=0
    if ($CheckOnly) {
        & python (Join-Path $PSScriptRoot 'readiness.py')
        $taskReadinessExit=$LASTEXITCODE
    }
    foreach ($script in Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.ps1') {
        $parseErrors=$null
        [void][Management.Automation.Language.Parser]::ParseFile($script.FullName,[ref]$null,[ref]$parseErrors)
        if ($parseErrors) { throw "Синтаксис $($script.Name): $parseErrors" }
    }
    $active=@(Get-CimInstance Win32_Process | Where-Object {
        $_.Name -eq 'Content.Server.exe' -and $_.ExecutablePath -and $_.ExecutablePath.StartsWith($bench+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)
    })
    if ($active.Count) { throw 'Тестовый сервер уже работает. Дождитесь завершения серии.' }
    $taskWorkspaceProcesses=@(Get-CimInstance Win32_Process | Where-Object {
        $_.Name -in @('Content.Server.exe','Content.Client.exe') -and $_.ExecutablePath -and
        $_.ExecutablePath.StartsWith((Join-Path $repo 'bin')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)
    })
    if ($taskWorkspaceProcesses.Count) {
        throw "BUILD_BLOCKED: закройте запущенный клиент/сервер Monolith перед проверкой; DLL заняты. PID: $($taskWorkspaceProcesses.ProcessId -join ', ')."
    }
    foreach ($port in @(12299,44880)) {
        $listener=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,$port)
        try { $listener.Start() } catch { throw "Порт $port занят." } finally { $listener.Stop() }
    }
    if ($CheckOnly) {
        if (-not (Test-Path -LiteralPath $pointer)) { throw 'REBUILD_REQUIRED: нет актуальной пары. Обычный запуск соберёт её автоматически.' }
        $manifestPath=(Get-Content -LiteralPath $pointer -Raw).Trim()
        $manifest=Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
        $fingerprint=& python (Join-Path $PSScriptRoot 'source_fingerprint.py') | ConvertFrom-Json
        if ($LASTEXITCODE -ne 0 -or $fingerprint.sha256 -ne $manifest.sourceFingerprint) { throw 'REBUILD_REQUIRED: исходники изменились после сборки.' }
        foreach ($build in $manifest.builds) {
            if (-not $build.compiled) { throw "REBUILD_REQUIRED: $($build.label) не собрана." }
            if ((Get-FileHash -LiteralPath (Join-Path $build.source 'bin/Content.Server/Content.Server.dll')).Hash.ToLowerInvariant() -ne $build.assemblySha256) { throw "REBUILD_REQUIRED: $($build.label) изменилась." }
        }
        if ($taskReadinessExit -ne 0) { throw 'NOT_READY: обязательные предварительные проверки v3 отсутствуют или устарели.' }
        Write-Host 'Строгая проверка v3: READY. Файлы и результаты не изменялись.'
    } else {
        New-Item -ItemType Directory -Path $bench -Force | Out-Null
        try { $lock=[IO.File]::Open((Join-Path $bench 'rerun.lock'),'OpenOrCreate','ReadWrite','None') }
        catch { throw 'Серия уже запущена в другом окне.' }
        $current=Join-Path $bench ('rerun/'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N').Substring(0,8))
        New-Item -ItemType Directory -Path $current | Out-Null
        $current | Set-Content -LiteralPath (Join-Path $bench 'CURRENT_RUN.txt')
        Start-Transcript -LiteralPath (Join-Path $current 'detailed.log') | Out-Null
        $taskTranscript=$true
        Set-LauncherStage 1 'Проверка обязательных доказательств'
        @{status='PREPARING';fullGameplayCoverage=$false;startedUtc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content (Join-Path $current 'launcher-status.json')
        Write-Host "Результаты: $current"
        if (-not $Legacy -and -not $Quick) {
            & python (Join-Path $PSScriptRoot 'readiness.py')
            if ($LASTEXITCODE -ne 0) { throw 'NOT_READY: полный запуск запрещён до прохождения всех обязательных проверок v3.' }
        }
        Write-Host 'Предыдущие результаты сохранены. Подготовка свежих Release A/B...'
        Set-LauncherStage 2 'Сборка свежих серверов и клиентов A/B'
        & (Join-Path $PSScriptRoot 'build_pair.ps1') -OutputDirectory (Join-Path $current 'pair')
        $manifestPath=Join-Path $current 'pair/BASELINE_MANIFEST.json'
        $manifestPath | Set-Content -LiteralPath $pointer
        Write-Host 'Функциональный gate: все KIAS-тесты, физические программы, комнаты и экспедиция...'
        Set-LauncherStage 3 'Регрессионные и физические проверки'
        $previousLab=$env:KIAS_LAB_OUTPUT
        try {
            $env:KIAS_LAB_OUTPUT=Join-Path $current 'functional'
            & dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj -c Release --filter 'FullyQualifiedName~KIAS' --logger 'trx;LogFileName=gate.trx' --results-directory (Join-Path $current 'tests') -m:4 -nr:false *> (Join-Path $current 'functional-gate.log')
            if ($LASTEXITCODE -ne 0) { throw "Gate не прошёл: $current/functional-gate.log" }
        } finally { $env:KIAS_LAB_OUTPUT=$previousLab }
        [xml]$trx=Get-Content -LiteralPath (Join-Path $current 'tests/gate.trx') -Raw
        $counts=$trx.TestRun.ResultSummary.Counters
        if ([int]$counts.total -eq 0 -or [int]$counts.total -ne [int]$counts.passed) { throw 'Gate содержит пропуски или ошибки.' }
        Write-Host "Gate: $($counts.passed)/$($counts.total) PASS."
        if ($Quick) {
            Set-LauncherStage 4 'Короткие игровые проверки'
            $Ships=[Math]::Min($Ships,3)
            Write-Host 'Quick: изолированные игровые проверки B, включая пожар, вентиляцию, ПКО, FTL, экипаж и столкновения. Полная готовность v3 проверяется отдельно.'
            & (Join-Path $PSScriptRoot 'native_suite.ps1') -Variants @('B') -Ships $Ships -OutputDirectory (Join-Path $current 'quick-B') -ManifestPath $manifestPath -Seed $Seed
            $taskNetworkScenario=Join-Path $current 'network-idle.jsonl'
            [IO.File]::WriteAllText($taskNetworkScenario,'')
            Write-Host 'Quick: настоящий сетевой клиент B и сериализация игровых состояний...'
            Set-LauncherStage 5 'Сетевая проверка'
            & (Join-Path $PSScriptRoot 'native_replay.ps1') -Variant B -Ships 1 -WarmupTicks 10800 -MeasuredTicks 600 -Scenario $taskNetworkScenario -OutputDirectory (Join-Path $current 'quick-network-B') -ManifestPath $manifestPath -Seed $Seed -NetworkClients 1 -FunctionalOnly -KeepNativeNpcAwake
            @{status='ISOLATED_NATIVE_SMOKE_COMPLETE';fullGameplayCoverage=$false;completedUtc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content (Join-Path $current 'launcher-status.json')
        }
        if (-not $Quick) {
        Set-LauncherStage 4 'Проверка свежей пары в игре'
        if ($Legacy) {
        Write-Host 'Проверка настоящего раунда A/B и сохранения 6000 админ-записей...'
        $preflight = Join-Path $current 'preflight'
        New-Item -ItemType Directory -Path $preflight | Out-Null
        $preflightScenario = Join-Path $preflight 'scenario.jsonl'
        & python (Join-Path $PSScriptRoot 'expanded_scenario.py') --ticks 1200 --output $preflightScenario
        if ($LASTEXITCODE -ne 0) { throw 'Не удалось создать короткий проверочный сценарий.' }
        foreach ($variant in @('A','B')) {
            $preflightRun = Join-Path $preflight $variant
            & (Join-Path $PSScriptRoot 'native_replay.ps1') -Variant $variant -Ships 40 -WarmupTicks 1800 -MeasuredTicks 1200 -Scenario $preflightScenario -OutputDirectory $preflightRun -ManifestPath $manifestPath -Seed $Seed -AdminLogProbe 6000
            & python (Join-Path $PSScriptRoot 'validate_adminlog_probe.py') $preflightRun
            if ($LASTEXITCODE -ne 0) { throw "Админ-логи $variant не прошли проверку сохранения." }
        }
        } else {
            Write-Host 'Игровые проверки свежей пары A/B перед полной серией...'
            & (Join-Path $PSScriptRoot 'native_suite.ps1') -Variants @('A','B') -Ships 1 -OutputDirectory (Join-Path $current 'native-preflight') -ManifestPath $manifestPath -Seed $Seed
        }
        $warmup=10800
        $measured=$Minutes*3600
        if ($Legacy) { $warmup=7200; $measured=72000 }
        Write-Host "A–B–B–A: $Ships кораблей, $($warmup/3600) мин прогрева + $($measured/3600) мин измерения на запуск."
        Set-LauncherStage 5 'Серия A–B–B–A и проверка отчётов'
        Write-Host 'Полное игровое покрытие учитывается отдельно от длительности серии.'
        & (Join-Path $PSScriptRoot 'load_series.ps1') -OutputDirectory (Join-Path $current 'series') -ReplayRoot (Join-Path $current 'runs') -ManifestPath $manifestPath -Ships $Ships -WarmupTicks $warmup -MeasuredTicks $measured -Seed $Seed -Quick:$Quick -Legacy:$Legacy -FullGame:(-not $Legacy)
        @{status='ABBA_COMPLETE';fullGameplayCoverage=$false;completedUtc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content (Join-Path $current 'launcher-status.json')
        }
    }
} catch {
    $exitCode=1
    Write-Host "Ошибка: $($_.Exception.Message)" -ForegroundColor Red
    if ($current) { @{status='FAILED_INCOMPLETE';error=$_.Exception.Message} | ConvertTo-Json | Set-Content (Join-Path $current 'launcher-status.json') }
} finally {
    if ($current) {
        if ($exitCode -eq 0) { Set-LauncherStage 6 'Подготовка архива результатов' }
        if ($taskTranscript) { Stop-Transcript | Out-Null; $taskTranscript=$false }
        & python (Join-Path $PSScriptRoot 'partial_archive.py') $current
        if ($LASTEXITCODE -ne 0) { $exitCode=1 }
        if ($exitCode -eq 0) { Write-Host 'ЭТАП 7/7: запуск завершён, архив готов.' }
    }
    if ($null -ne $lock) { $lock.Dispose() }
}
exit $exitCode
