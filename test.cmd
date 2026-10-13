@echo off
setlocal
cd /d "%~dp0"
set "TETRIS_CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%TETRIS_CSC%" set "TETRIS_CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%TETRIS_CSC%" (echo Can .NET Framework compiler. & exit /b 1)
if not exist "test-bin" mkdir "test-bin"
"%TETRIS_CSC%" /nologo /codepage:65001 /target:exe /out:test-bin\RulesTests.exe /r:System.dll /r:System.Core.dll /r:System.Runtime.Serialization.dll Core\*.cs Data\*.cs Tests\RulesTests.cs Tests\BossTests.cs
if errorlevel 1 exit /b 1
test-bin\RulesTests.exe
set "TETRIS_RESULT=%ERRORLEVEL%"
pause
exit /b %TETRIS_RESULT%
