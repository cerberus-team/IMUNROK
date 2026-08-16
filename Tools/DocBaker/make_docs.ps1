# Bakes aged-hanji case-document textures with vertical Gungsuh (Joseon palace script) text.
#
# Run from Unity:  menu [이문록 > 에셋: 사건 문서 텍스처 굽기]
# Run by hand:     powershell -ExecutionPolicy Bypass -File Tools\DocBaker\make_docs.ps1
#
# Edit docs.json to change document content - this file only draws.
# Source is ASCII-only on purpose; all Korean/Hanja lives in docs.json (UTF-8).
# Windows only: depends on System.Drawing and the bundled Gungsuh font.
param(
    [string]$ProjectRoot
)

Add-Type -AssemblyName System.Drawing

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $ProjectRoot) { $ProjectRoot = (Resolve-Path (Join-Path $here '..\..')).Path }

$docs   = Get-Content (Join-Path $here 'docs.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$outDir = Join-Path $ProjectRoot 'Assets\_Project\Art\Props\Textures'
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Force $outDir | Out-Null }

# Authored at 1024x1448 (A3 proportions), then resampled to a power-of-two square so
# Unity can block-compress it. The document quad is 1:1.414, so it stretches back on screen.
$W = 1024
$H = 1448
$OUT = 1024

$fontName  = 'Gungsuh'
$installed = (New-Object System.Drawing.Text.InstalledFontCollection).Families | ForEach-Object { $_.Name }
if ($installed -notcontains $fontName) { $fontName = 'Batang' }
Write-Host "font: $fontName"
Write-Host "out : $outDir"

$px = [System.Drawing.GraphicsUnit]::Pixel

foreach ($doc in $docs) {
    # Fixed seed: re-baking an unchanged document produces an identical file.
    $rng = New-Object System.Random(20260816)
    $bmp = New-Object System.Drawing.Bitmap($W, $H)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

    # --- paper base ---
    $g.Clear([System.Drawing.Color]::FromArgb(255, 231, 217, 186))

    # broad tonal mottling - kept very faint so it reads as uneven pulp, not blobs
    for ($i = 0; $i -lt 70; $i++) {
        $r  = $rng.Next(320, 760)
        $c  = [System.Drawing.Color]::FromArgb($rng.Next(2, 6), 176, 156, 116)
        $br = New-Object System.Drawing.SolidBrush($c)
        $g.FillEllipse($br, $rng.Next(-300, $W), $rng.Next(-300, $H), $r, $r)
        $br.Dispose()
    }

    # hanji fibers
    for ($i = 0; $i -lt 2600; $i++) {
        $x = $rng.Next(0, $W); $y = $rng.Next(0, $H); $len = $rng.Next(6, 34)
        $c = [System.Drawing.Color]::FromArgb($rng.Next(10, 30), 196, 178, 138)
        $pen = New-Object System.Drawing.Pen($c, 1)
        if ($rng.Next(0, 10) -lt 7) { $g.DrawLine($pen, $x, $y, ($x + $len), ($y + $rng.Next(-2, 3))) }
        else { $g.DrawLine($pen, $x, $y, ($x + $rng.Next(-2, 3)), ($y + $len)) }
        $pen.Dispose()
    }

    # foxing specks
    for ($i = 0; $i -lt 340; $i++) {
        $r = $rng.Next(2, 7)
        $c = [System.Drawing.Color]::FromArgb($rng.Next(14, 40), 150, 118, 74)
        $br = New-Object System.Drawing.SolidBrush($c)
        $g.FillEllipse($br, $rng.Next(0, $W), $rng.Next(0, $H), $r, $r)
        $br.Dispose()
    }

    # edge darkening (aging toward the borders)
    for ($i = 0; $i -lt 46; $i++) {
        $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(5, 138, 112, 72), 3)
        $g.DrawRectangle($pen, ($i * 2), ($i * 2), ($W - $i * 4), ($H - $i * 4))
        $pen.Dispose()
    }

    $ink     = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(238, 38, 30, 24))
    $inkSoft = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(210, 52, 42, 32))

    # --- title: hanja, horizontal, centered near the top ---
    $titleFont = New-Object System.Drawing.Font($fontName, 118, [System.Drawing.FontStyle]::Regular, $px)
    $tSize = $g.MeasureString($doc.title, $titleFont)
    $g.DrawString($doc.title, $titleFont, $ink, (($W - $tSize.Width) / 2), 92)
    $titleBottom = 92 + $tSize.Height

    $rulePen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(150, 60, 48, 36), 3)
    $g.DrawLine($rulePen, 120, ($titleBottom + 26), ($W - 120), ($titleBottom + 26))
    $rulePen.Dispose()

    # --- body: vertical columns, right to left ---
    $startY     = $titleBottom + 92
    $bodyBottom = $H - 300           # leave the seal band clear
    $startX     = $W - 132

    # Auto-fit so edits to docs.json can't silently run off the page.
    # A space advances 45% of a full character.
    $maxUnits = 0.0
    foreach ($col in $doc.columns) {
        $u = 0.0
        foreach ($ch in $col.ToCharArray()) { if ($ch -eq ' ') { $u += 0.45 } else { $u += 1.0 } }
        if ($u -gt $maxUnits) { $maxUnits = $u }
    }
    $charStep = [Math]::Min(74.0, ($bodyBottom - $startY) / [Math]::Max($maxUnits, 1.0))
    $fontSize = [Math]::Max(18.0, $charStep * 0.78)
    $colStep  = [Math]::Min(122.0, ($startX - 96) / [Math]::Max($doc.columns.Count - 1, 1))
    Write-Host ("  {0}: {1} cols, charStep {2:N1}, font {3:N1}" -f $doc.file, $doc.columns.Count, $charStep, $fontSize)

    $bodyFont = New-Object System.Drawing.Font($fontName, $fontSize, [System.Drawing.FontStyle]::Regular, $px)
    $ci = 0
    foreach ($col in $doc.columns) {
        $x = $startX - ($ci * $colStep)
        $y = $startY
        foreach ($ch in $col.ToCharArray()) {
            if ($ch -eq ' ') { $y += $charStep * 0.45; continue }
            $s  = [string]$ch
            $cs = $g.MeasureString($s, $bodyFont)
            # slight per-character jitter so it reads as brushwork, not a text field
            $brush = if ($rng.Next(0, 6) -eq 0) { $inkSoft } else { $ink }
            $g.DrawString($s, $bodyFont, $brush, ($x - $cs.Width / 2 + $rng.Next(-2, 3)), ($y + $rng.Next(-2, 3)))
            $y += $charStep
        }
        $ci++
    }

    # --- red seal, bottom left ---
    $sealSize = 118
    $sx = 132
    $sy = $H - 262
    $sealBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(205, 158, 42, 34))
    $g.FillRectangle($sealBrush, $sx, $sy, $sealSize, $sealSize)
    $sealFont   = New-Object System.Drawing.Font($fontName, 46, [System.Drawing.FontStyle]::Bold, $px)
    $paperBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(230, 236, 224, 194))
    $sci = 0
    foreach ($ch in $doc.seal.ToCharArray()) {
        $s = [string]$ch
        $cs = $g.MeasureString($s, $sealFont)
        $g.DrawString($s, $sealFont, $paperBrush, ($sx + $sealSize / 2 - $cs.Width / 2), ($sy + 6 + $sci * 52))
        $sci++
    }
    # break up the seal so it looks stamped, not printed
    for ($i = 0; $i -lt 160; $i++) {
        $r = $rng.Next(2, 8)
        $c = [System.Drawing.Color]::FromArgb($rng.Next(30, 90), 231, 217, 186)
        $br = New-Object System.Drawing.SolidBrush($c)
        $g.FillEllipse($br, ($sx + $rng.Next(0, $sealSize)), ($sy + $rng.Next(0, $sealSize)), $r, $r)
        $br.Dispose()
    }

    # final grain over everything
    for ($i = 0; $i -lt 3000; $i++) {
        $c = [System.Drawing.Color]::FromArgb($rng.Next(6, 20), 120, 100, 70)
        $br = New-Object System.Drawing.SolidBrush($c)
        $g.FillRectangle($br, $rng.Next(0, $W), $rng.Next(0, $H), 2, 2)
        $br.Dispose()
    }

    $outPath = Join-Path $outDir ($doc.file + '.png')
    $pot = New-Object System.Drawing.Bitmap($OUT, $OUT)
    $pg  = [System.Drawing.Graphics]::FromImage($pot)
    $pg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $pg.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $pg.DrawImage($bmp, (New-Object System.Drawing.Rectangle(0, 0, $OUT, $OUT)))
    $pot.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $pg.Dispose(); $pot.Dispose()
    Write-Host "  wrote $outPath"

    $titleFont.Dispose(); $bodyFont.Dispose(); $sealFont.Dispose()
    $ink.Dispose(); $inkSoft.Dispose(); $sealBrush.Dispose(); $paperBrush.Dispose()
    $g.Dispose(); $bmp.Dispose()
}
Write-Host 'done'
