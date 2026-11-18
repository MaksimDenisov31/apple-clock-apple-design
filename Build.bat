@echo off
setlocal
cd /d "%~dp0"
set "FX=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319"
set "WPF=%FX%\WPF"
"%FX%\csc.exe" /nologo /target:winexe /optimize+ /platform:anycpu /win32manifest:app.manifest /win32icon:assets\app.ico /out:AppleClock.exe /resource:assets\app.ico,AppleClock.Icon /resource:assets\timer-chime.wav,AppleClock.TimerChime /r:System.dll /r:System.Core.dll /r:System.Xml.dll /r:System.Xaml.dll /r:"%WPF%\WindowsBase.dll" /r:"%WPF%\PresentationCore.dll" /r:"%WPF%\PresentationFramework.dll" src\*.cs
if errorlevel 1 exit /b 1
echo Built AppleClock.exe

