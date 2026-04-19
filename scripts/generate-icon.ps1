# Generates src/AudioSwitch.App/Assets/audio-switch.ico
# Design: 3 vertical EQ bars (equalizer sliders) in deep sky blue on transparent
# background. Reads as "audio preferences / mixer" and is visually distinct from
# the Windows speaker-arcs volume icon.
#
# Run from repo root:
#   powershell -ExecutionPolicy Bypass -File scripts/generate-icon.ps1

Add-Type -AssemblyName System.Drawing

$repoRoot = Split-Path -Parent $PSScriptRoot
$outDir   = Join-Path $repoRoot 'src/AudioSwitch.App/Assets'
$outFile  = Join-Path $outDir  'audio-switch.ico'

if (-not (Test-Path $outDir)) {
    New-Item -ItemType Directory -Path $outDir | Out-Null
}

$sizes = @(16, 24, 32, 48, 64, 128, 256)

function New-IconBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode      = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $g.PixelOffsetMode    = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    $barCount      = 3
    $heightFactors = @(0.72, 0.42, 0.92)   # short-middle-tall EQ silhouette
    $accent        = [System.Drawing.Color]::FromArgb(255,   0, 191, 255)  # Deep Sky Blue
    $accentDark    = [System.Drawing.Color]::FromArgb(255,   0, 120, 200)

    # Padding and layout
    $pad = [Math]::Max(1, [int]($size * 0.08))
    $drawW = $size - 2 * $pad
    $drawH = $size - 2 * $pad
    $barW  = [Math]::Max(2, [int]($drawW * 0.22))
    $gap   = [Math]::Max(1, [int]($drawW * 0.11))
    $totalW = $barCount * $barW + ($barCount - 1) * $gap
    $startX = [int](($size - $totalW) / 2)
    $baseY  = $size - $pad

    # Vertical gradient fill per bar
    for ($i = 0; $i -lt $barCount; $i++) {
        $barH = [int]($drawH * $heightFactors[$i])
        $x = $startX + $i * ($barW + $gap)
        $y = $baseY - $barH

        $rect = New-Object System.Drawing.Rectangle $x, $y, $barW, $barH
        $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
            $rect, $accent, $accentDark,
            [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)

        if ($size -ge 32) {
            # Rounded rectangle path (top corners only)
            $r = [Math]::Min([int]($barW / 2), [int]($size * 0.12))
            $path = New-Object System.Drawing.Drawing2D.GraphicsPath
            $path.AddArc($x,                $y,          $r * 2, $r * 2, 180, 90)
            $path.AddArc($x + $barW - 2*$r, $y,          $r * 2, $r * 2, 270, 90)
            $path.AddLine($x + $barW, $y + $r, $x + $barW, $baseY)
            $path.AddLine($x + $barW, $baseY,  $x,          $baseY)
            $path.CloseFigure()
            $g.FillPath($brush, $path)
            $path.Dispose()
        } else {
            $g.FillRectangle($brush, $rect)
        }

        $brush.Dispose()
    }

    $g.Dispose()
    return $bmp
}

# Render each size to PNG bytes
$pngBytes = @{}
foreach ($s in $sizes) {
    $bmp = New-IconBitmap $s
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngBytes[$s] = $ms.ToArray()
    $ms.Dispose()
    $bmp.Dispose()
}

# Build multi-resolution ICO (PNG-embedded entries; Vista+ supports this)
$fs = New-Object System.IO.FileStream $outFile, ([System.IO.FileMode]::Create)
$bw = New-Object System.IO.BinaryWriter $fs

# ICONDIR header
$bw.Write([UInt16]0)              # Reserved
$bw.Write([UInt16]1)              # Type: icon
$bw.Write([UInt16]$sizes.Count)   # Count

# Reserve space for ICONDIRENTRY array; fill in after we know offsets
$headerSize = 6
$entrySize  = 16
$dataOffset = $headerSize + $entrySize * $sizes.Count

$currentOffset = $dataOffset
foreach ($s in $sizes) {
    $dim = if ($s -ge 256) { 0 } else { $s }  # 0 == 256 in ICO spec
    $bw.Write([byte]$dim)                 # Width
    $bw.Write([byte]$dim)                 # Height
    $bw.Write([byte]0)                    # ColorCount (0 = no palette)
    $bw.Write([byte]0)                    # Reserved
    $bw.Write([UInt16]1)                  # Planes
    $bw.Write([UInt16]32)                 # BitCount
    $bw.Write([UInt32]$pngBytes[$s].Length)
    $bw.Write([UInt32]$currentOffset)
    $currentOffset += $pngBytes[$s].Length
}

foreach ($s in $sizes) {
    $bw.Write($pngBytes[$s])
}

$bw.Close()
$fs.Close()

$info = Get-Item $outFile
Write-Host ("Wrote {0} ({1} bytes, {2} sizes embedded)" -f $info.FullName, $info.Length, $sizes.Count)
