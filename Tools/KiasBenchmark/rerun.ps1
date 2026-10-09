param([switch]$CheckOnly)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
Set-Location -LiteralPath $repo
$benchRoot = Join-Path $repo '.kias-benchmark'
New-Item -ItemType Directory -Path $benchRoot -Force | Out-Null
$lock = $null
$exitCode = 0
try {
    try {
        $lock = [IO.File]::Open((Join-Path $benchRoot 'rerun.lock'), 'OpenOrCreate', 'ReadWrite', 'None')
    } catch { throw 'Повторная серия уже запущена в другом окне.' }
    Get-Command python -ErrorAction Stop | Out-Null
    & python -c 'import sys; assert sys.version_info >= (3, 10), "Python 3.10+ required"'
    if ($LASTEXITCODE -ne 0) { throw 'Нужен Python 3.10 или новее.' }
    $active = @(Get-CimInstance Win32_Process | Where-Object {
        $_.ProcessId -ne $PID -and $_.CommandLine -and
        ($_.CommandLine -match '(?i)\s-File\s+"?[^"\r\n]*[\\/]load_series\.ps1(?:["\s]|$)' -or
         ($_.Name -eq 'Content.Server.exe' -and $_.ExecutablePath -and
          $_.ExecutablePath.StartsWith((Join-Path $benchRoot 'builds'), [StringComparison]::OrdinalIgnoreCase)))
    })
    if ($active.Count) { throw 'Нагрузочная серия или её сервер уже работают. Дождитесь завершения.' }
    foreach ($port in @(12299, 44880)) {
        $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, $port)
        try { $listener.Start() } catch { throw "Порт $port занят. Закройте использующий его тестовый сервер." }
        finally { $listener.Stop() }
    }
    $manifest = Get-Content (Join-Path $benchRoot 'BASELINE_MANIFEST.json') -Raw | ConvertFrom-Json
    foreach ($variant in @('A', 'B')) {
        $build = @($manifest.builds | Where-Object label -eq $variant)
        if ($build.Count -ne 1 -or -not $build[0].compiled) { throw "Нет проверенной сборки $variant." }
        $source = Join-Path $benchRoot "builds/$variant"
        $expectedMap = if ($variant -eq 'A') { $manifest.staticParity.aMapSha256 } else { $manifest.staticParity.bMapSha256 }
        $checks = @{
            'bin/Content.Server/Content.Server.dll' = $build[0].assemblySha256
            'Tools/KiasBenchmark/Server/NativeLab.cs' = $build[0].nativeLabSha256
            'Resources/Maps/_Forge/Shuttles/Archive/Mercenary/labBriar.yml' = $expectedMap
        }
        if ($variant -eq 'B') { $checks['Tools/KiasBenchmark/Server/KiasTelemetry.cs'] = $build[0].telemetrySha256 }
        foreach ($relative in $checks.Keys) {
            if ((Get-FileHash -LiteralPath (Join-Path $source $relative)).Hash.ToLowerInvariant() -ne $checks[$relative]) {
                throw "Сборка $variant изменилась: $relative. Требуется подготовить сборки заново."
            }
        }
    }
    if ($CheckOnly) { Write-Host 'Проверка запуска: OK. Результаты не удалялись, серия не запускалась.' }
    else {
        $rerunRoot = [IO.Path]::GetFullPath((Join-Path $benchRoot 'rerun'))
        $current = [IO.Path]::GetFullPath((Join-Path $rerunRoot 'current'))
        if (-not $current.StartsWith($rerunRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Недопустимый каталог очистки.'
        }
        if (Test-Path -LiteralPath $rerunRoot) {
            if ((Get-Item -LiteralPath $rerunRoot).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Каталог rerun не должен быть ссылкой.' }
        }
        if (Test-Path -LiteralPath $current) {
            if ((Get-Item -LiteralPath $current).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Каталог current не должен быть ссылкой.' }
            Remove-Item -LiteralPath $current -Recurse -Force
        }
        New-Item -ItemType Directory -Path $current -Force | Out-Null
        Write-Host 'A–B–B–A: 40 кораблей, 2 минуты прогрева + 20 минут на прогон.'
        Write-Host 'Старые результаты этого батника очищены. Ожидайте около 90 минут.'
        $series = Join-Path $current 'series'
        try {
            & (Join-Path $PSScriptRoot 'load_series.ps1') -OutputDirectory $series -ReplayRoot (Join-Path $current 'runs')
        } catch {
            $exitCode = 1
            Write-Host "Серия неполная: $($_.Exception.Message)" -ForegroundColor Red
        }
        if (Test-Path -LiteralPath (Join-Path $series 'status.json')) {
            $archive = Join-Path $current 'KIAS_web-agent.zip'
            Write-Host 'Подготовка архива для анализа...'
            & python (Join-Path $PSScriptRoot 'package_analysis.py') $series $archive
            if ($LASTEXITCODE -ne 0) { throw 'Не удалось собрать архив.' }
            Write-Host "Архив: $archive"
            if ($exitCode -eq 0) { Write-Host 'Все 4 прогона завершены и проверены.' -ForegroundColor Green }
            else { Write-Host 'В архиве неполная серия; см. status.json и логи.' -ForegroundColor Yellow }
        }
    }
} catch {
    $exitCode = 1
    Write-Host "Ошибка: $($_.Exception.Message)" -ForegroundColor Red
} finally {
    if ($null -ne $lock) { $lock.Dispose() }
}
exit $exitCode
