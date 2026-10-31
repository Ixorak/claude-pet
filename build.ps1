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
    Write-Host 'Pet is running from out\app: asking it to quit...'
    & $exe --cmd quit | Out-Null
    $running | Wait-Process -Timeout 5 -ErrorAction SilentlyContinue
}

# Icône maison (fabriquée par le code, une seule fois).
$ico = Join-Path $root 'assets\house.ico'
if (-not (Test-Path $ico)) {
    Write-Host 'Generating the house icon...'
    dotnet run --project (Join-Path $root 'tools\AssetGen') -c Release -- ico $ico
    if ($LASTEXITCODE -ne 0) { throw 'Generating the house icon failed.' }
}

Write-Host 'Building Claude Pet (native, Release)...'
dotnet publish (Join-Path $root 'src\ClaudePet\ClaudePet.csproj') -c Release -o (Join-Path $root 'out\app') -nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'The build failed.' }
Write-Host ([string]::Format([cultureinfo]::InvariantCulture, '  {0} ({1:N1} MB)', $exe, (Get-Item $exe).Length / 1MB))

if ($NoSetup) { return }

$iscc = @(
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup 6 was not found. To install it: winget install JRSoftware.InnoSetup' }

Write-Host 'Building the installer...'
& $iscc /Qp (Join-Path $root 'installer\PetSetup.iss')
if ($LASTEXITCODE -ne 0) { throw 'Building the installer failed.' }
$setup = Join-Path $root 'dist\PetSetup.exe'
Write-Host ([string]::Format([cultureinfo]::InvariantCulture, 'Done: {0} ({1:N1} MB)', $setup, (Get-Item $setup).Length / 1MB))
