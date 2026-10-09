@echo off
chcp 65001 >nul
cd /d "%~dp0"
where pwsh.exe >nul 2>&1
if errorlevel 1 (
    echo Требуется PowerShell 7: команда pwsh.exe не найдена.
    pause
    exit /b 1
)
pwsh.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Tools\KiasBenchmark\rerun.ps1" %*
set "kias_exit=%errorlevel%"
echo.
pause
exit /b %kias_exit%
