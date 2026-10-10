param([switch]$CheckOnly, [switch]$Quick, [switch]$Legacy, [switch]$FullGame,
    [ValidateRange(1,40)][int]$Ships=40, [ValidateRange(2,120)][int]$Minutes=30,
    [int]$Seed=20261010, [switch]$WaitForStart)
$ErrorActionPreference='Stop'
$taskOptions=@{}
foreach ($taskName in $PSBoundParameters.Keys) {
    if ($taskName -ne 'WaitForStart') { $taskOptions[$taskName]=$PSBoundParameters[$taskName] }
}
if ($WaitForStart -and -not $CheckOnly) {
    Write-Host 'KIAS | ОЖИДАНИЕ КОМАНДЫ' -ForegroundColor Cyan
    Write-Host 'Сборка и тесты ещё не начались. Закройте игры и лишние приложения.'
    Write-Host 'START — начать выбранный запуск. EXIT — закрыть окно.'
    while ($true) {
        $taskCommand=Read-Host 'Команда'
        if ($null -eq $taskCommand) { exit 2 }
        if ($taskCommand.Trim() -ieq 'EXIT') { exit 0 }
        if ($taskCommand.Trim() -ieq 'START') { break }
        Write-Host 'Ожидается START или EXIT. Тесты не запущены.'
    }
}
& (Join-Path $PSScriptRoot 'rerun.ps1') @taskOptions
exit $LASTEXITCODE
