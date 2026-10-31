# Recompile Claude Pet (exécutable natif, sans runtime à installer) et régénère l'installateur dist\PetSetup.exe.
# Usage : .\build.ps1            (ou double-clic sur build.bat)
#         .\build.ps1 -NoSetup   (seulement l'exécutable, dans out\app)
param([switch]$NoSetup)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
Set-Location $root

# La compilation native (AOT) a besoin de l'éditeur de liens de Visual Studio, que dotnet trouve avec vswhere.exe.
$vsInstaller = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer'
if (Test-Path $vsInstaller) { $env:PATH = "$vsInstaller;$env:PATH" }

# Un Pet lancé depuis out\app verrouille son exe : on lui demande de quitter.
$exe = Join-Path $root 'out\app\ClaudePet.exe'
$running = Get-Process ClaudePet -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe }
if ($running -and (Test-Path $exe)) {
    Write-Host 'Pet tourne depuis out\app : je lui demande de quitter...'
    & $exe --cmd quit | Out-Null
    $running | Wait-Process -Timeout 5 -ErrorAction SilentlyContinue
}

# Icône maison (fabriquée par le code, une seule fois).
$ico = Join-Path $root 'assets\house.ico'
if (-not (Test-Path $ico)) {
    Write-Host "Génération de l'icône maison..."
    dotnet run --project (Join-Path $root 'tools\AssetGen') -c Release -- ico $ico
    if ($LASTEXITCODE -ne 0) { throw "La génération de l'icône a échoué." }
}

Write-Host 'Compilation de Claude Pet (natif, Release)...'
dotnet publish (Join-Path $root 'src\ClaudePet\ClaudePet.csproj') -c Release -o (Join-Path $root 'out\app') -nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'La compilation a échoué.' }
Write-Host ("  {0} ({1:N1} Mo)" -f $exe, ((Get-Item $exe).Length / 1MB))

if ($NoSetup) { return }

$iscc = @(
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup 6 est introuvable. Pour l''installer : winget install JRSoftware.InnoSetup' }

Write-Host "Fabrication de l'installateur..."
& $iscc /Qp (Join-Path $root 'installer\PetSetup.iss')
if ($LASTEXITCODE -ne 0) { throw "La fabrication de l'installateur a échoué." }
$setup = Join-Path $root 'dist\PetSetup.exe'
Write-Host ("Terminé : {0} ({1:N1} Mo)" -f $setup, ((Get-Item $setup).Length / 1MB))
