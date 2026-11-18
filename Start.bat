@echo off
cd /d "%~dp0"
if not exist AppleClock.exe call Build.bat
if exist AppleClock.exe start "" AppleClock.exe
