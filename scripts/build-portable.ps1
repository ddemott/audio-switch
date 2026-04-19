# Build a portable distribution of AudioSwitch.
# Usage: pwsh ./scripts/build-portable.ps1
#
# Produces build/portable/ (ready-to-run folder) and build/AudioSwitch-Portable.zip.
# The portable.flag marker makes ProfileStore.DefaultFilePath use profiles.json
# beside the exe instead of %APPDATA%, and disables Start-with-Windows in the tray.

$ErrorActionPreference = 'Stop'

$repoRoot   = Split-Path -Parent $PSScriptRoot
$buildDir   = Join-Path $repoRoot 'build'
$publishDir = Join-Path $buildDir 'portable'
$project    = Join-Path $repoRoot 'src/AudioSwitch.App/AudioSwitch.App.csproj'
$zipPath    = Join-Path $buildDir 'AudioSwitch-Portable.zip'

if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }
New-Item -ItemType Directory -Path $publishDir | Out-Null

dotnet publish $project `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -o $publishDir

$marker = Join-Path $publishDir 'portable.flag'
Set-Content -Path $marker -Value '' -NoNewline

if (Test-Path $zipPath) { Remove-Item $zipPath }
Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $zipPath

Write-Host ""
Write-Host "Portable folder: $publishDir"
Write-Host "Portable zip:    $zipPath"
