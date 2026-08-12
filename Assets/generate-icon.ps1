# Generates Assets/LfpHub.ico (multi-res) and Assets/LfpHub-256.png
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$assets = $PSScriptRoot
New-Item -ItemType Directory -Force -Path $assets | Out-Null

function Get-RoundRectPath {
    param(
        [float]$X, [float]$Y, [float]$W, [float]$H, [float]$Radius
    )
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = [Math]::Min([Math]::Min($Radius * 2, $W), $H)
    $path.AddArc($X, $Y, $d, $d, 180, 90)
    $path.AddArc(($X + $W - $d), $Y, $d, $d, 270, 90)
    $path.AddArc(($X + $W - $d), ($Y + $H - $d), $d, $d, 0, 90)
    $path.AddArc($X, ($Y + $H - $d), $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-LfpIconBitmap {
    param([int]$Size)

    $bmp = New-Object System.Drawing.Bitmap $Size, $Size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    $bgOuter = [System.Drawing.ColorTranslator]::FromHtml('#0F1412')
    $bgInner = [System.Drawing.ColorTranslator]::FromHtml('#171E1B')
    $accent  = [System.Drawing.ColorTranslator]::FromHtml('#3DDC97')
    $light   = [System.Drawing.ColorTranslator]::FromHtml('#E8F0EC')

    $pad = [Math]::Max(1.0, $Size * 0.04)
    $radius = [Math]::Max(2.0, $Size * 0.18)
    $tileX = $pad
    $tileY = $pad
    $tileW = $Size - (2 * $pad)
    $tileH = $Size - (2 * $pad)

    $tilePath = Get-RoundRectPath -X $tileX -Y $tileY -W $tileW -H $tileH -Radius $radius
    $tileRect = New-Object System.Drawing.RectangleF $tileX, $tileY, $tileW, $tileH
    $brushBg = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $tileRect, $bgOuter, $bgInner,
        [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
    $g.FillPath($brushBg, $tilePath)
    $brushBg.Dispose()

    if ($Size -ge 32) {
        $edgePen = New-Object System.Drawing.Pen (
            [System.Drawing.Color]::FromArgb(45, 232, 240, 236),
            [Math]::Max(1.0, $Size / 128.0))
        $g.DrawPath($edgePen, $tilePath)
        $edgePen.Dispose()
    }
    $tilePath.Dispose()

    $cx = $Size / 2.0
    $cy = $Size / 2.0

    if ($Size -le 16) {
        # Crisp 16px battery: outline + fill + terminal
        $bw = 7.0
        $bh = 9.0
        $bx = ($Size - $bw) / 2.0
        $by = 4.0
        $body = Get-RoundRectPath -X $bx -Y $by -W $bw -H $bh -Radius 1.5
        $pen = New-Object System.Drawing.Pen $accent, 1.25
        $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
        $g.DrawPath($pen, $body)
        $pen.Dispose()

        $fillBrush = New-Object System.Drawing.SolidBrush $accent
        $g.FillRectangle($fillBrush, ($bx + 1.5), ($by + 4.0), ($bw - 3.0), 3.5)
        # terminal
        $g.FillRectangle($fillBrush, 6.0, 2.5, 4.0, 2.0)
        $fillBrush.Dispose()
        $body.Dispose()
    }
    else {
        $bw = $Size * 0.40
        $bh = $Size * 0.52
        $bx = $cx - ($bw / 2.0)
        $by = $cy - ($bh / 2.0) + ($Size * 0.02)
        $br = [Math]::Max(2.0, $Size * 0.08)

        $bodyPath = Get-RoundRectPath -X $bx -Y $by -W $bw -H $bh -Radius $br
        $penW = [Math]::Max(1.75, $Size * 0.048)
        $pen = New-Object System.Drawing.Pen $accent, $penW
        $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
        $g.DrawPath($pen, $bodyPath)

        # Terminal nub (top)
        $tw = $bw * 0.38
        $th = $Size * 0.065
        $tx = $cx - ($tw / 2.0)
        $ty = $by - $th + ($penW * 0.2)
        $tr = [Math]::Max(1.0, $Size * 0.03)
        $termPath = Get-RoundRectPath -X $tx -Y $ty -W $tw -H ($th + $tr) -Radius $tr
        $termBrush = New-Object System.Drawing.SolidBrush $accent
        $g.FillPath($termBrush, $termPath)
        $termBrush.Dispose()
        $termPath.Dispose()

        # Charge fill (~60% from bottom), clipped to body
        $inset = $penW + ($Size * 0.025)
        $fillW = $bw - (2 * $inset)
        $fillH = ($bh - (2 * $inset)) * 0.60
        $fillX = $bx + $inset
        $fillY = $by + $bh - $inset - $fillH

        $region = New-Object System.Drawing.Region $bodyPath
        $g.SetClip($region, [System.Drawing.Drawing2D.CombineMode]::Replace)

        $fillRect = New-Object System.Drawing.RectangleF (
            [float]$fillX, [float]$fillY, [float]$fillW, [float]$fillH)
        $accentDark = [System.Drawing.Color]::FromArgb(255, 40, 180, 120)
        $fillBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
            $fillRect, $accentDark, $accent,
            [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)

        $fr = [Math]::Max(1.0, $br - 2)
        $fillPath = Get-RoundRectPath -X $fillX -Y $fillY -W $fillW -H $fillH -Radius $fr
        # Prefer flat top on charge level
        $fillPath.Dispose()
        $fillPath = New-Object System.Drawing.Drawing2D.GraphicsPath
        $fd = [Math]::Min($fr * 2, [Math]::Min($fillW, $fillH))
        # top edge flat
        $fillPath.AddLine([float]$fillX, [float]$fillY, [float]($fillX + $fillW), [float]$fillY)
        $fillPath.AddLine([float]($fillX + $fillW), [float]$fillY, [float]($fillX + $fillW), [float]($fillY + $fillH - $fd / 2))
        $fillPath.AddArc([float]($fillX + $fillW - $fd), [float]($fillY + $fillH - $fd), [float]$fd, [float]$fd, 0, 90)
        $fillPath.AddArc([float]$fillX, [float]($fillY + $fillH - $fd), [float]$fd, [float]$fd, 90, 90)
        $fillPath.CloseFigure()
        $g.FillPath($fillBrush, $fillPath)
        $g.ResetClip()
        $region.Dispose()
        $fillPath.Dispose()
        $fillBrush.Dispose()

        if ($Size -ge 48) {
            $hiPen = New-Object System.Drawing.Pen (
                [System.Drawing.Color]::FromArgb(100, 232, 240, 236), 1.0)
            $g.DrawLine($hiPen,
                [float]($fillX + 1), [float]($fillY + 1),
                [float]($fillX + $fillW - 1), [float]($fillY + 1))
            $hiPen.Dispose()
        }

        # "LFP" under battery on large sizes only
        if ($Size -ge 128) {
            $fontSize = [float]($Size * 0.095)
            $font = New-Object System.Drawing.Font(
                'Segoe UI', $fontSize,
                [System.Drawing.FontStyle]::Bold,
                [System.Drawing.GraphicsUnit]::Pixel)
            $sf = New-Object System.Drawing.StringFormat
            $sf.Alignment = [System.Drawing.StringAlignment]::Center
            $sf.LineAlignment = [System.Drawing.StringAlignment]::Near
            $textBrush = New-Object System.Drawing.SolidBrush (
                [System.Drawing.Color]::FromArgb(210, $light.R, $light.G, $light.B))
            $textY = [float]($by + $bh + $Size * 0.025)
            $textH = [float]($Size * 0.12)
            if (($textY + $textH) -lt ($Size - $pad)) {
                $textRect = New-Object System.Drawing.RectangleF(
                    0.0, $textY, [float]$Size, $textH)
                $g.DrawString('LFP', $font, $textBrush, $textRect, $sf)
            }
            $textBrush.Dispose()
            $font.Dispose()
            $sf.Dispose()
        }

        $pen.Dispose()
        $bodyPath.Dispose()
    }

    $g.Dispose()
    return $bmp
}

function Write-MultiIcon {
    param(
        [string]$IcoPath,
        [int[]]$Sizes
    )

    $images = New-Object System.Collections.Generic.List[System.Drawing.Bitmap]
    foreach ($s in $Sizes) {
        [void]$images.Add((New-LfpIconBitmap -Size $s))
    }

    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter $ms

    # ICONDIR
    $bw.Write([uint16]0)
    $bw.Write([uint16]1)
    $bw.Write([uint16]$images.Count)

    $headerSize = 6 + (16 * $images.Count)
    $offset = $headerSize
    $pngBlobs = New-Object System.Collections.Generic.List[byte[]]

    foreach ($img in $images) {
        $pngMs = New-Object System.IO.MemoryStream
        $img.Save($pngMs, [System.Drawing.Imaging.ImageFormat]::Png)
        $blob = $pngMs.ToArray()
        $pngMs.Dispose()
        [void]$pngBlobs.Add($blob)

        $wByte = if ($img.Width -ge 256) { [byte]0 } else { [byte]$img.Width }
        $hByte = if ($img.Height -ge 256) { [byte]0 } else { [byte]$img.Height }
        $bw.Write($wByte)
        $bw.Write($hByte)
        $bw.Write([byte]0)
        $bw.Write([byte]0)
        $bw.Write([uint16]1)
        $bw.Write([uint16]32)
        $bw.Write([uint32]$blob.Length)
        $bw.Write([uint32]$offset)
        $offset += $blob.Length
    }

    foreach ($blob in $pngBlobs) {
        $bw.Write($blob)
    }

    $bw.Flush()
    [System.IO.File]::WriteAllBytes($IcoPath, $ms.ToArray())
    $bw.Dispose()
    $ms.Dispose()

    foreach ($img in $images) { $img.Dispose() }
}

$sizes = @(16, 32, 48, 64, 128, 256)
$icoPath = Join-Path $assets 'LfpHub.ico'
$pngPath = Join-Path $assets 'LfpHub-256.png'

Write-MultiIcon -IcoPath $icoPath -Sizes $sizes

$bmp256 = New-LfpIconBitmap -Size 256
$bmp256.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp256.Dispose()

$bytes = [System.IO.File]::ReadAllBytes($icoPath)
$count = [BitConverter]::ToUInt16($bytes, 4)
Write-Host ("ICO: {0} ({1} bytes, {2} images)" -f $icoPath, $bytes.Length, $count)
Write-Host ("PNG: {0} ({1} bytes)" -f $pngPath, (Get-Item $pngPath).Length)

for ($i = 0; $i -lt $count; $i++) {
    $base = 6 + ($i * 16)
    $w = $bytes[$base]; if ($w -eq 0) { $w = 256 }
    $h = $bytes[$base + 1]; if ($h -eq 0) { $h = 256 }
    $len = [BitConverter]::ToUInt32($bytes, $base + 8)
    $off = [BitConverter]::ToUInt32($bytes, $base + 12)
    Write-Host ("  [{0}] {1}x{2}  png={3}B  off={4}" -f $i, $w, $h, $len, $off)
}

Write-Host 'OK'
