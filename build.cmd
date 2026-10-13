@echo off
setlocal
cd /d "%~dp0"
rem C# 5: compiler di kem .NET Framework tren Windows, khong can NuGet.
set "TETRIS_CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%TETRIS_CSC%" set "TETRIS_CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%TETRIS_CSC%" (
  echo Khong tim thay .NET Framework compiler. Hay cai .NET Framework 4.8 hoac mo solution bang Visual Studio.
  pause
  exit /b 1
)
if not exist "dist\Assets" mkdir "dist\Assets"
"%TETRIS_CSC%" /nologo /codepage:65001 /target:winexe /optimize+ /out:dist\Tetris118.exe /win32manifest:app.manifest /win32icon:Assets\tetris118.ico /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Runtime.Serialization.dll Program.cs Core\*.cs Data\*.cs Audio\*.cs UI\*.cs
if errorlevel 1 (
  echo Build that bai. Xem loi o tren.
  pause
  exit /b 1
)
xcopy /Y /Q "Assets\*.wav" "dist\Assets\" >nul
copy /Y "Tetris118.exe.config" "dist\Tetris118.exe.config" >nul
echo.
echo Build thanh cong: dist\Tetris118.exe
echo Chay test.cmd de kiem tra luat choi.
if /i not "%~1"=="--no-pause" pause
exit /b 0
