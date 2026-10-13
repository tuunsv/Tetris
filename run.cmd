@echo off
setlocal
cd /d "%~dp0"
call build.cmd --no-pause
if errorlevel 1 exit /b 1
start "Tetris 118" "%~dp0dist\Tetris118.exe"
