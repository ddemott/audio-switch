Add-Type -AssemblyName System.Drawing
. "$PSScriptRoot\generate-icon.ps1" *> $null  # side effect: regenerates .ico; ok

# Render a single large preview using the same function
$repoRoot = Split-Path -Parent $PSScriptRoot
$bmp = New-IconBitmap 512
$out = Join-Path $repoRoot 'src/AudioSwitch.App/Assets/audio-switch-preview.png'
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Host "Preview: $out"
