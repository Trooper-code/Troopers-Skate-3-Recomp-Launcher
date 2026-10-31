@echo off
rem Builds "Trooper's Skate 3 Recomp Launcher.exe" into ..\bin using the C# compiler bundled with Windows (.NET Framework 4.x).
rem No Visual Studio or .NET SDK needed. Compiles every .cs file in this folder.
setlocal
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
set OUT=%~dp0..\bin
set ICON=
rem Optional: drop skate3.ico in this folder (or keep skate3.exe one folder up) to give the exe the game icon.
if not exist "%~dp0skate3.ico" if exist "%~dp0..\skate3.exe" powershell -NoProfile -Command "Add-Type -AssemblyName System.Drawing; $i=[System.Drawing.Icon]::ExtractAssociatedIcon('%~dp0..\skate3.exe'); $f=[IO.File]::Create('%~dp0skate3.ico'); $i.Save($f); $f.Close()"
if exist "%~dp0skate3.ico" set ICON=/win32icon:"%~dp0skate3.ico"
if not exist "%OUT%" mkdir "%OUT%"
"%CSC%" /nologo /target:winexe /optimize+ /out:"%OUT%\Trooper's Skate 3 Recomp Launcher.exe" %ICON% /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Core.dll /r:System.IO.Compression.dll "%~dp0*.cs"
if errorlevel 1 (
  echo.
  echo Build failed - see the errors above.
  if not defined CI pause
  exit /b 1
)
echo Built bin\Trooper's Skate 3 Recomp Launcher.exe
endlocal
