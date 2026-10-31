@echo off
rem Recompile Claude Pet et regenere l'installateur dist\PetSetup.exe en une commande.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" %*
if errorlevel 1 (
  echo.
  echo The build failed.
  pause
  exit /b 1
)
