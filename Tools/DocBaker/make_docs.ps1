# Bakes aged-hanji case-document textures with vertical Gungsuh (Joseon palace script) text.
#
# Run from Unity:  menu [이문록 > 에셋: 사건 문서 텍스처 굽기]
# Run by hand:     powershell -ExecutionPolicy Bypass -File Tools\DocBaker\make_docs.ps1
#
# Edit docs.json to change document content - this file only draws.
# Source is ASCII-only on purpose; all Korean/Hanja lives in docs.json (UTF-8).
# Windows only: depends on System.Drawing and the bundled Gungsuh font.
#
# Document kinds (docs.json "kind", default "doc"):
#   doc    - petition/deed sheet: hanja title, vertical body, red seal.  Fields: title, seal, columns
#   ledger - account book spread: ruled grid, one entry per column, two
#            writing hands so a forged stretch reads as a different brush.
#            Fields: title, entries [{ text, hand 0|1, mark true|false }]
#   burnt  - charred fragment on transparent background (half-burned letter).
#            Fields: columns (text runs off where the paper is gone)
param(
    [string]$ProjectRoot
)

Add-Type -AssemblyName System.Drawing

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $ProjectRoot) { $ProjectRoot = (Resolve-Path (Join-Path $here '..\..')).Path }

$docs = Get-Content (Join-Path $here 'docs.json') -Raw -Encoding UTF8 | ConvertFrom-Json

# Where a document lands depends on whether it belongs to one case or to everybody.
# No "case" field  -> shared props (the three petition sheets on the 조사청 board).
# "case": "X"      -> that case's own art folder, so 옹고집 clues do not pile up in the shared bin.
$sharedDir = Join-Path $ProjectRoot 'Assets\_Project\Art\Props\Textures'
function Get-OutDir($doc) {
    $case = Get-Field $doc 'case' $null
    if (-not $case) { return $sharedDir }
    return (Join-Path $ProjectRoot ('Assets\_Project\{0}\Art\Textures' -f $case))
}

$installed = (New-Object System.Drawing.Text.InstalledFontCollection).Families | ForEach-Object { $_.Name }

# Two hands. They must be visibly different brushes, not two sizes of one brush -
# the whole point of the ledger clue is that a player can tell them apart.
# Two people wrote this ledger, and the whole clue is that you can tell.
#
#   hand 0 = Ong Deok-gu. Twenty years of entries in a landowner's trained brush.
#   hand 1 = Bok-dong. He was a household slave (see the manumission deed, J15) and
#            never had a scholar's schooling, so his brush is the looser of the two.
#            He only starts appearing in the last two lines - that is the forgery.
#
# Each list is tried in order, so the first installed name wins. Put a new font at
# the front of a list and it is picked up with no other change.
$fontName = 'Batang'
foreach ($cand in @('Ma Shan Zheng','LXGW WenKai KR','HCR Batang','Batang','Gungsuh')) {
    if ($installed -contains $cand) { $fontName = $cand; break }
}
$fontAlt = $fontName
foreach ($cand in @('Long Cang','Liu Jian Mao Cao','Zhi Mang Xing','Yuji Mai','Gungsuh','BatangChe')) {
    if ($installed -contains $cand -and $cand -ne $fontName) { $fontAlt = $cand; break }
}
if ($fontAlt -eq $fontName) { Write-Host "  ! no second face installed - the two hands differ by wobble only" }

# Brush faces drawn for Chinese drop hanja that Joseon paperwork needs - the
# Korean-coined ones above all. 畓 (paddy) has no Chinese counterpart at all, and
# traditional forms like 記 證 標 爲 錢 are often absent from simplified sets.
# A missing glyph renders as an empty box, so name it here instead of letting a
# tofu square ship as a clue.
function Report-MissingGlyphs($familyName, $texts) {
    try { Add-Type -AssemblyName PresentationCore -ErrorAction Stop } catch { return }
    try {
        $tf = New-Object System.Windows.Media.Typeface($familyName)
        $gt = $null
        if (-not $tf.TryGetGlyphTypeface([ref]$gt)) { return }
    } catch { return }
    $missing = New-Object System.Collections.Generic.List[char]
    foreach ($t in $texts) {
        foreach ($ch in $t.ToCharArray()) {
            if ($ch -eq ' ') { continue }
            if ($missing -contains $ch) { continue }
            if (-not $gt.CharacterToGlyphMap.ContainsKey([int]$ch)) { [void]$missing.Add($ch) }
        }
    }
    if ($missing.Count -gt 0) {
        Write-Host ("  ! '{0}' has no glyph for {1} character(s): {2}" -f $familyName, $missing.Count, (-join $missing))
        Write-Host "    (those will print as empty boxes - pick another face or change the wording)"
    } else {
        Write-Host ("  '{0}': all characters covered" -f $familyName)
    }
}

$allText = @()
foreach ($d in $docs) {
    if ($d.PSObject.Properties.Name -contains 'title'   -and $d.title)   { $allText += $d.title }
    if ($d.PSObject.Properties.Name -contains 'seal'    -and $d.seal)    { $allText += $d.seal }
    if ($d.PSObject.Properties.Name -contains 'columns' -and $d.columns) { $allText += $d.columns }
    if ($d.PSObject.Properties.Name -contains 'entries' -and $d.entries) {
        foreach ($e in $d.entries) { $allText += $e.text }
    }
}
Report-MissingGlyphs $fontName $allText
if ($fontAlt -ne $fontName) { Report-MissingGlyphs $fontAlt $allText }

Write-Host "font: $fontName  (second hand: $fontAlt)"
Write-Host "out : $sharedDir  (+ per-case folders)"

$px = [System.Drawing.GraphicsUnit]::Pixel

# ---------------------------------------------------------------- helpers

function Get-Field($obj, $name, $fallback) {
    if ($obj.PSObject.Properties.Name -contains $name -and $null -ne $obj.$name) { return $obj.$name }
    return $fallback
}

function New-Canvas($w, $h) {
    $bmp = New-Object System.Drawing.Bitmap($w, $h)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    return @{ bmp = $bmp; g = $g }
}

# Aged hanji: uneven pulp, visible fibers, foxing specks, darkened edges.
function Paint-Paper($g, $w, $h, $rng) {
    $g.Clear([System.Drawing.Color]::FromArgb(255, 231, 217, 186))

    for ($i = 0; $i -lt 70; $i++) {
        $r  = $rng.Next(320, 760)
        $c  = [System.Drawing.Color]::FromArgb($rng.Next(2, 6), 176, 156, 116)
        $br = New-Object System.Drawing.SolidBrush($c)
        $g.FillEllipse($br, $rng.Next(-300, $w), $rng.Next(-300, $h), $r, $r)
        $br.Dispose()
    }

    for ($i = 0; $i -lt 2600; $i++) {
        $x = $rng.Next(0, $w); $y = $rng.Next(0, $h); $len = $rng.Next(6, 34)
        $c = [System.Drawing.Color]::FromArgb($rng.Next(10, 30), 196, 178, 138)
        $pen = New-Object System.Drawing.Pen($c, 1)
        if ($rng.Next(0, 10) -lt 7) { $g.DrawLine($pen, $x, $y, ($x + $len), ($y + $rng.Next(-2, 3))) }
        else { $g.DrawLine($pen, $x, $y, ($x + $rng.Next(-2, 3)), ($y + $len)) }
        $pen.Dispose()
    }

    for ($i = 0; $i -lt 340; $i++) {
        $r = $rng.Next(2, 7)
        $c = [System.Drawing.Color]::FromArgb($rng.Next(14, 40), 150, 118, 74)
        $br = New-Object System.Drawing.SolidBrush($c)
        $g.FillEllipse($br, $rng.Next(0, $w), $rng.Next(0, $h), $r, $r)
        $br.Dispose()
    }

    for ($i = 0; $i -lt 46; $i++) {
        $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(5, 138, 112, 72), 3)
        $g.DrawRectangle($pen, ($i * 2), ($i * 2), ($w - $i * 4), ($h - $i * 4))
        $pen.Dispose()
    }
}

function Paint-Grain($g, $w, $h, $rng) {
    for ($i = 0; $i -lt 3000; $i++) {
        $c = [System.Drawing.Color]::FromArgb($rng.Next(6, 20), 120, 100, 70)
        $br = New-Object System.Drawing.SolidBrush($c)
        $g.FillRectangle($br, $rng.Next(0, $w), $rng.Next(0, $h), 2, 2)
        $br.Dispose()
    }
}

# One vertical run of characters, centered on $x, growing downward from $y.
# A space advances 45% of a character. Returns the y it ended at.
# $sloppy 0 = a trained clerk's hand: even, upright, evenly inked.
# $sloppy 1 = someone forging it in a hurry: every glyph leans a different way,
#             sizes wander, the column drifts off true, ink pools and runs dry.
# A different font alone does not read as a different hand - the wobble is what does.
function Paint-Column($g, $text, $font, $x, $y, $step, $ink, $inkSoft, $jitter, $rng, $sloppy = 0) {
    $drift = 0.0                      # how far the column has wandered off the rule
    $baseSize = $font.Size
    foreach ($ch in $text.ToCharArray()) {
        if ($ch -eq ' ') { $y += $step * 0.45; continue }
        $s  = [string]$ch

        # One drawing path for both hands. An earlier attempt gave the forged hand its
        # own path (per-glyph canvas rotation) and whole columns landed off the sheet.
        # The difference now comes from glyph size, ink and drift - nothing else.
        $gf = $font
        $made = $false
        if ($sloppy -eq 1) {
            $sz = $baseSize * (0.86 + $rng.NextDouble() * 0.30)
            $gf = New-Object System.Drawing.Font($font.FontFamily, $sz, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
            $made = $true
            $drift += ($rng.NextDouble() - 0.5) * ($jitter * 0.8)
            if ($drift -gt  $jitter * 1.6) { $drift =  $jitter * 1.6 }
            if ($drift -lt -$jitter * 1.6) { $drift = -$jitter * 1.6 }
        }

        $cs = $g.MeasureString($s, $gf)
        $brush = $ink
        $r = $rng.Next(0, 5)
        if ($r -eq 0) { $brush = $inkSoft }        # the brush had run dry
        $dx = $rng.Next(-$jitter, $jitter + 1) + $drift
        $dy = $rng.Next(-$jitter, $jitter + 1)
        $px0 = $x - $cs.Width / 2 + $dx
        $g.DrawString($s, $gf, $brush, $px0, ($y + $dy))
        if ($sloppy -eq 1 -and $r -eq 1) {         # ink pooled - stamped twice
            $g.DrawString($s, $gf, $brush, ($px0 + 1.2), ($y + $dy + 0.9))
        }
        if ($made) { $gf.Dispose() }

        if ($sloppy -eq 1) { $y += $step * (0.90 + $rng.NextDouble() * 0.22) }
        else { $y += $step }
    }
    return $y
}

# Longest column measured in character units, so nothing silently runs off the page.
function Measure-Units($texts) {
    $maxUnits = 0.0
    foreach ($t in $texts) {
        $u = 0.0
        foreach ($ch in $t.ToCharArray()) {
            if ($ch -eq ' ') { $u += 0.45 } else { $u += 1.0 }
        }
        if ($u -gt $maxUnits) { $maxUnits = $u }
    }
    if ($maxUnits -lt 1.0) { $maxUnits = 1.0 }
    return $maxUnits
}

function Paint-Seal($g, $seal, $sx, $sy, $size, $fontName, $rng) {
    if ([string]::IsNullOrEmpty($seal)) { return }
    $sealBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(205, 158, 42, 34))
    $g.FillRectangle($sealBrush, $sx, $sy, $size, $size)
    $sealFont   = New-Object System.Drawing.Font($fontName, 46, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $paperBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(230, 236, 224, 194))
    $sci = 0
    foreach ($ch in $seal.ToCharArray()) {
        $s = [string]$ch
        $cs = $g.MeasureString($s, $sealFont)
        $g.DrawString($s, $sealFont, $paperBrush, ($sx + $size / 2 - $cs.Width / 2), ($sy + 6 + $sci * 52))
        $sci++
    }
    # break up the block so it reads as stamped, not printed
    for ($i = 0; $i -lt 160; $i++) {
        $r = $rng.Next(2, 8)
        $c = [System.Drawing.Color]::FromArgb($rng.Next(30, 90), 231, 217, 186)
        $br = New-Object System.Drawing.SolidBrush($c)
        $g.FillEllipse($br, ($sx + $rng.Next(0, $size)), ($sy + $rng.Next(0, $size)), $r, $r)
        $br.Dispose()
    }
    $sealBrush.Dispose(); $sealFont.Dispose(); $paperBrush.Dispose()
}

# ---------------------------------------------------------------- kinds

function Draw-Doc($g, $doc, $w, $h, $fontName, $rng) {
    $ink     = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(238, 38, 30, 24))
    $inkSoft = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(210, 52, 42, 32))

    # title: hanja, horizontal, centered near the top
    $titleFont = New-Object System.Drawing.Font($fontName, 118, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
    $tSize = $g.MeasureString($doc.title, $titleFont)
    $g.DrawString($doc.title, $titleFont, $ink, (($w - $tSize.Width) / 2), 92)
    $titleBottom = 92 + $tSize.Height

    $rulePen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(150, 60, 48, 36), 3)
    $g.DrawLine($rulePen, 120, ($titleBottom + 26), ($w - 120), ($titleBottom + 26))
    $rulePen.Dispose()

    $startY     = $titleBottom + 92
    $bodyBottom = $h - 300           # leave the seal band clear
    $startX     = $w - 132

    $maxUnits = Measure-Units $doc.columns
    $charStep = [Math]::Min(74.0, ($bodyBottom - $startY) / $maxUnits)
    $fontSize = [Math]::Max(18.0, $charStep * 0.78)
    $colStep  = [Math]::Min(122.0, ($startX - 96) / [Math]::Max($doc.columns.Count - 1, 1))
    Write-Host ("  {0}: doc, {1} cols, charStep {2:N1}, font {3:N1}" -f $doc.file, $doc.columns.Count, $charStep, $fontSize)

    $bodyFont = New-Object System.Drawing.Font($fontName, $fontSize, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
    $ci = 0
    foreach ($col in $doc.columns) {
        $x = $startX - ($ci * $colStep)
        Paint-Column $g $col $bodyFont $x $startY $charStep $ink $inkSoft 2 $rng | Out-Null
        $ci++
    }

    Paint-Seal $g (Get-Field $doc 'seal' '') 132 ($h - 262) 118 $fontName $rng

    $titleFont.Dispose(); $bodyFont.Dispose(); $ink.Dispose(); $inkSoft.Dispose()
}

# Account book. Entries run right to left, one per ruled column.
# hand 1 is a different font, size and jitter - that difference IS the clue.
function Draw-Ledger($g, $doc, $w, $h, $fontName, $fontAlt, $rng) {
    $ink     = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(238, 38, 30, 24))
    $inkSoft = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(210, 52, 42, 32))
    # the forger writes with a wetter, blacker brush
    $ink2     = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(244, 24, 20, 18))
    $ink2Soft = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(220, 40, 34, 28))

    $titleFont = New-Object System.Drawing.Font($fontName, 84, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
    $tSize = $g.MeasureString($doc.title, $titleFont)
    $g.DrawString($doc.title, $titleFont, $ink, (($w - $tSize.Width) / 2), 54)
    $gridTop = 54 + $tSize.Height + 34

    $entries    = $doc.entries
    $count      = $entries.Count
    $gridBottom = $h - 96
    $rightX     = $w - 120
    $leftLimit  = 120
    $colStep    = [Math]::Min(168.0, ($rightX - $leftLimit) / [Math]::Max($count - 1, 1))

    $texts = @()
    foreach ($e in $entries) { $texts += $e.text }
    $maxUnits = Measure-Units $texts
    $charStep = [Math]::Min(66.0, ($gridBottom - $gridTop - 90) / $maxUnits)
    $fontSize = [Math]::Max(16.0, $charStep * 0.80)
    Write-Host ("  {0}: ledger, {1} entries, charStep {2:N1}, font {3:N1}" -f $doc.file, $count, $charStep, $fontSize)

    # ruling: printed grid, faded and slightly off-true like a woodblock impression
    $rulePen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(120, 70, 56, 42), 2)
    $g.DrawLine($rulePen, $leftLimit, $gridTop, $rightX + 40, $gridTop)
    $g.DrawLine($rulePen, $leftLimit, $gridBottom, $rightX + 40, $gridBottom)
    for ($i = 0; $i -lt $count + 1; $i++) {
        $lx = $rightX + 40 - ($i * $colStep)
        if ($lx -lt $leftLimit) { break }
        $g.DrawLine($rulePen, $lx, $gridTop, ($lx + $rng.Next(-2, 3)), $gridBottom)
    }
    $rulePen.Dispose()

    $fontA = New-Object System.Drawing.Font($fontName, $fontSize, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
    # Regular, not Italic. A synthesised italic on a CJK face makes MeasureString
    # report nonsense widths, which shoved these columns clean off the sheet -
    # and Joseon documents have no such thing as an italic anyway.
    $fontB = New-Object System.Drawing.Font($fontAlt, ($fontSize * 0.92), [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)

    $markFont  = New-Object System.Drawing.Font($fontName, ($fontSize * 1.05), [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $markBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(200, 132, 40, 32))

    $ci = 0
    foreach ($e in $entries) {
        $x = $rightX - ($ci * $colStep)
        $hand = Get-Field $e 'hand' 0
        if ($hand -eq 1) {
            $endY = Paint-Column $g $e.text $fontB $x ($gridTop + 34) ($charStep * 1.04) $ink2 $ink2Soft 7 $rng 1
        } else {
            $endY = Paint-Column $g $e.text $fontA $x ($gridTop + 34) $charStep $ink $inkSoft 2 $rng
        }

        # 押 - the personal mark. Present only where docs.json says so.
        if ((Get-Field $e 'mark' $false) -eq $true) {
            $ms = $g.MeasureString([char]0x62BC, $markFont)
            $g.DrawString([char]0x62BC, $markFont, $markBrush, ($x - $ms.Width / 2), ($endY + 10))
        }
        $ci++
    }

    $titleFont.Dispose(); $fontA.Dispose(); $fontB.Dispose(); $markFont.Dispose()
    $markBrush.Dispose(); $ink.Dispose(); $inkSoft.Dispose(); $ink2.Dispose(); $ink2Soft.Dispose()
}

# An irregular torn/charred outline. Wanders around the centre so no two edges match.
function New-FragmentPath($w, $h, $rng) {
    $pts = New-Object System.Collections.ArrayList
    $cx = $w / 2.0
    $cy = $h / 2.0
    $steps = 60
    # Two overlaid frequencies: a slow lobe (how the sheet was folded when it caught)
    # and a fast one (the ragged tooth of the burn line). Pure noise reads as a scallop.
    $ph1 = $rng.NextDouble() * 6.28
    $ph2 = $rng.NextDouble() * 6.28
    for ($i = 0; $i -lt $steps; $i++) {
        $a = ($i / [double]$steps) * 2.0 * [Math]::PI
        # left and bottom edges eaten further in - that side sat in the embers
        $bias = 1.0 - 0.20 * [Math]::Cos($a) - 0.13 * [Math]::Sin($a)
        $slow = 0.075 * [Math]::Sin($a * 3.0 + $ph1)
        $fast = 0.045 * [Math]::Sin($a * 11.0 + $ph2)
        $r = (0.415 + $slow + $fast + $rng.NextDouble() * 0.035) * $bias
        $x = $cx + [Math]::Cos($a) * $r * $w
        $y = $cy + [Math]::Sin($a) * $r * $h
        [void]$pts.Add((New-Object System.Drawing.PointF([float]$x, [float]$y)))
    }
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddClosedCurve([System.Drawing.PointF[]]$pts.ToArray(), 0.2)
    return $path
}

# ---------------------------------------------------------------- main

foreach ($doc in $docs) {
    # Fixed seed: re-baking an unchanged document produces an identical file.
    $rng  = New-Object System.Random(20260816)
    $kind = Get-Field $doc 'kind' 'doc'

    # Authored at A3 proportions (or landscape for a book spread), then resampled to a
    # power-of-two square so Unity can block-compress it. The quad stretches it back.
    if ($kind -eq 'ledger') { $W = 1448; $H = 1024 }
    elseif ($kind -eq 'burnt') { $W = 1024; $H = 1024 }
    else { $W = 1024; $H = 1448 }
    $OUT = 1024

    $canvas = New-Canvas $W $H
    $bmp = $canvas.bmp
    $g   = $canvas.g

    Paint-Paper $g $W $H $rng

    if ($kind -eq 'ledger') {
        Draw-Ledger $g $doc $W $H $fontName $fontAlt $rng
    }
    elseif ($kind -eq 'burnt') {
        # Body only - a scrap torn out of the middle of a letter has no title block.
        $ink     = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(238, 38, 30, 24))
        $inkSoft = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(210, 52, 42, 32))
        $maxUnits = Measure-Units $doc.columns
        $charStep = [Math]::Min(78.0, ($H - 300) / $maxUnits)
        $fontSize = [Math]::Max(18.0, $charStep * 0.78)
        $colStep  = [Math]::Min(126.0, ($W - 380) / [Math]::Max($doc.columns.Count - 1, 1))
        Write-Host ("  {0}: burnt, {1} cols, charStep {2:N1}, font {3:N1}" -f $doc.file, $doc.columns.Count, $charStep, $fontSize)
        $bodyFont = New-Object System.Drawing.Font($fontName, $fontSize, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
        $ci = 0
        foreach ($col in $doc.columns) {
            # first column sits where the paper is gone - the letter's opening burned away
            $x = ($W - 250) - ($ci * $colStep)
            Paint-Column $g $col $bodyFont $x 150 $charStep $ink $inkSoft 2 $rng | Out-Null
            $ci++
        }
        $bodyFont.Dispose(); $ink.Dispose(); $inkSoft.Dispose()
    }
    else {
        Draw-Doc $g $doc $W $H $fontName $rng
    }

    Paint-Grain $g $W $H $rng

    # burnt: keep only what is inside the fragment outline, and scorch the rim.
    if ($kind -eq 'burnt') {
        $path = New-FragmentPath $W $H $rng
        $frag = New-Object System.Drawing.Bitmap($W, $H)
        $fg   = [System.Drawing.Graphics]::FromImage($frag)
        $fg.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $fg.Clear([System.Drawing.Color]::Transparent)
        $fg.SetClip($path)
        $fg.DrawImage($bmp, 0, 0)

        # Scorch, from the edge inward: a thin black char line, then a fast-falling
        # brown scorch. Thin and steep - a wide even band reads as a drawn outline.
        for ($i = 0; $i -lt 16; $i++) {
            $t = $i / 16.0
            $a  = [int](225 * [Math]::Pow(1.0 - $t, 2.2))
            $r  = [int](28 + 78 * $t)
            $gg = [int](18 + 52 * $t)
            $b  = [int](12 + 32 * $t)
            $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb($a, $r, $gg, $b), (2 + $i * 1.7))
            $fg.DrawPath($pen, $path)
            $pen.Dispose()
        }

        # Uneven bite: soot blooms and pale ash flecks scattered along the burn line,
        # so the rim varies instead of running at one width the whole way around.
        $edge = $path.Clone()
        $edge.Flatten()
        $ep = $edge.PathPoints
        # Faint and hugging the line: half of each blob falls outside the clip and is
        # cut away, so what is left reads as an uneven bite rather than a dot.
        for ($i = 0; $i -lt 110; $i++) {
            $p  = $ep[$rng.Next(0, $ep.Length)]
            $cx = $w / 2.0; $cy = $h / 2.0
            $k  = $rng.NextDouble() * 0.05
            $bx = $p.X + ($cx - $p.X) * $k
            $by = $p.Y + ($cy - $p.Y) * $k
            $rw = $rng.Next(14, 58)
            $rh = $rng.Next(14, 58)
            $c = [System.Drawing.Color]::FromArgb($rng.Next(10, 34), 46, 30, 20)
            $br = New-Object System.Drawing.SolidBrush($c)
            $fg.FillEllipse($br, ($bx - $rw / 2), ($by - $rh / 2), $rw, $rh)
            $br.Dispose()
        }
        $edge.Dispose()
        $fg.ResetClip()
        $fg.Dispose(); $path.Dispose()
        $bmp.Dispose()
        $bmp = $frag
    }

    $docDir = Get-OutDir $doc
    if (-not (Test-Path $docDir)) { New-Item -ItemType Directory -Force $docDir | Out-Null }
    $outPath = Join-Path $docDir ($doc.file + '.png')
    $pot = New-Object System.Drawing.Bitmap($OUT, $OUT)
    $pg  = [System.Drawing.Graphics]::FromImage($pot)
    $pg.Clear([System.Drawing.Color]::Transparent)
    $pg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $pg.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $pg.DrawImage($bmp, (New-Object System.Drawing.Rectangle(0, 0, $OUT, $OUT)))
    $pot.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $pg.Dispose(); $pot.Dispose()
    Write-Host "  wrote $outPath"

    $g.Dispose(); $bmp.Dispose()
}
Write-Host 'done'
