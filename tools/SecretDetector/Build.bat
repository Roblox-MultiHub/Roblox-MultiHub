@echo off
setlocal
cd /d "%~dp0"
title Building SecretDetector

set "CSC=%windir%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%windir%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo Could not find the .NET Framework 4 compiler on this PC.
  pause
  exit /b 1
)

echo Compiling SecretDetector...
"%CSC%" /nologo /target:winexe /optimize+ /debug- /warn:1 /nowarn:0168,0219,0414,0649,0169,0067 /out:SecretDetector.exe /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /recurse:*.cs
if errorlevel 1 (
  echo.
  echo ============================ BUILD FAILED ============================
  echo Copy all the text above and send it back, so it can be fixed.
  pause
  exit /b 1
)
echo.
echo Build OK  -  SecretDetector.exe is ready.
exit /b 0
