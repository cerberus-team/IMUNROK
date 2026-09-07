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
#   file   - case file (事目): the ruled sheet handed to the inspector on arrival.
#            Portrait box per suspect, household roster, what has to be settled,
#            and a red slip pasted at the foot.
#            Fields: title, subtitle, seal, portraitNote, suspects [{mark,caption}],
#                    rosterLabel, roster [], asksLabel, asks [], tagHead, tagNote
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

# Fonts ship with the repo, they are not installed on anyone's machine. Load them
# straight out of the project so a teammate can bake without installing anything.
# While each file is open we also record which characters it actually has - these
# calligraphy faces are drawn for Chinese and drop a lot of Joseon hanja.
$fontDir = Join-Path $ProjectRoot 'Assets\_Project\_Common\Art\Fonts'
$script:privateFonts = New-Object System.Drawing.Text.PrivateFontCollection
$script:familyByName = @{}
$script:coverByName  = @{}
if (Test-Path $fontDir) {
    $wpf = $true
    try { Add-Type -AssemblyName PresentationCore -ErrorAction Stop } catch { $wpf = $false }
    $seen = @{}
    foreach ($file in (Get-ChildItem (Join-Path $fontDir '*') -Include *.ttf,*.otf -File)) {
        try { $script:privateFonts.AddFontFile($file.FullName) } catch { continue }
        $added = $null
        foreach ($fam in $script:privateFonts.Families) {
            if (-not $seen.ContainsKey($fam.Name)) { $seen[$fam.Name] = $true; $added = $fam }
        }
        if ($null -eq $added) { continue }
        $script:familyByName[$added.Name] = $added
        if ($wpf) {
            try {
                $gt = New-Object System.Windows.Media.GlyphTypeface (New-Object System.Uri($file.FullName))
                $set = New-Object System.Collections.Generic.HashSet[int]
                foreach ($k in $gt.CharacterToGlyphMap.Keys) { [void]$set.Add($k) }
                $script:coverByName[$added.Name] = $set
            } catch { }
        }
        Write-Host ("  loaded font from project: {0}  ({1})" -f $added.Name, $file.Name)
    }
    $installed = @($installed) + @($script:familyByName.Keys)
}

# A face by family name: the repo's own fonts first, then whatever Windows has.
function New-Face($name, $size, $style) {
    if ($script:familyByName.ContainsKey($name)) {
        return New-Object System.Drawing.Font($script:familyByName[$name], $size, $style, [System.Drawing.GraphicsUnit]::Pixel)
    }
    return New-Object System.Drawing.Font($name, $size, $style, [System.Drawing.GraphicsUnit]::Pixel)
}

# If the chosen face has no glyph for this character, hand back a stand-in face that
# does. An empty box does not read as bad handwriting - it reads as a broken asset.
# Hangul only shows up in one document - the burnt letter (J10), which a slave
# wrote, so it could not be hanja. The brush faces here carry no Hangul at all,
# so that sheet falls through to this face. Chosun Centennial is the house Korean
# face; it goes first so the letter reads in the same voice as the rest of the game.
$script:fallbackName = 'Batang'
foreach ($cand in @('조선100년체','Chosun Centennial','ChosunCentennial','HCR Batang','Batang','Gungsuh','Malgun Gothic')) {
    if ($installed -contains $cand) { $script:fallbackName = $cand; break }
}
function Resolve-Face($font, $ch) {
    $fam = $font.FontFamily.Name
    if (-not $script:coverByName.ContainsKey($fam)) { return $null }
    if ($script:coverByName[$fam].Contains([int]$ch)) { return $null }
    return (New-Face $script:fallbackName $font.Size $font.Style)
}

# Everything the documents ask a font to draw. The face is chosen against this
# list, so it has to be gathered before the choosing and not after.
$allText = @()
foreach ($d in $docs) {
    if ($d.PSObject.Properties.Name -contains 'title'   -and $d.title)   { $allText += $d.title }
    if ($d.PSObject.Properties.Name -contains 'seal'    -and $d.seal)    { $allText += $d.seal }
    if ($d.PSObject.Properties.Name -contains 'columns' -and $d.columns) { $allText += $d.columns }
    if ($d.PSObject.Properties.Name -contains 'entries' -and $d.entries) {
        foreach ($e in $d.entries) { $allText += $e.text }
    }
    # the case file keeps its words in fields of its own - gather those too, or the
    # face is chosen without ever seeing them and the sheet bakes full of tofu boxes.
    foreach ($f in @('subtitle','portraitNote','rosterLabel','asksLabel','tagHead','tagNote')) {
        if ($d.PSObject.Properties.Name -contains $f -and $d.$f) { $allText += $d.$f }
    }
    foreach ($f in @('roster','asks')) {
        if ($d.PSObject.Properties.Name -contains $f -and $d.$f) { $allText += $d.$f }
    }
    if ($d.PSObject.Properties.Name -contains 'suspects' -and $d.suspects) {
        foreach ($su in $d.suspects) { $allText += $su.mark; $allText += $su.caption }
    }
}

# Which of those characters a face cannot draw. Repo fonts were read straight from
# the file while loading; for installed ones ask WPF once and keep the answer.
function Get-Missing($familyName, $texts) {
    $cover = $null
    if ($script:coverByName.ContainsKey($familyName)) { $cover = $script:coverByName[$familyName] }
    if ($null -eq $cover) {
        try { Add-Type -AssemblyName PresentationCore -ErrorAction Stop } catch { return '' }
        try {
            $tf = New-Object System.Windows.Media.Typeface($familyName)
            $gt = $null
            if (-not $tf.TryGetGlyphTypeface([ref]$gt)) { return '' }
            $cover = New-Object System.Collections.Generic.HashSet[int]
            foreach ($k in $gt.CharacterToGlyphMap.Keys) { [void]$cover.Add($k) }
            $script:coverByName[$familyName] = $cover
        } catch { return '' }
    }
    $missing = New-Object System.Collections.Generic.List[char]
    foreach ($t in $texts) {
        foreach ($ch in $t.ToCharArray()) {
            if ($ch -eq ' ') { continue }
            if ($missing -contains $ch) { continue }
            if (-not $cover.Contains([int]$ch)) { [void]$missing.Add($ch) }
        }
    }
    return (-join $missing)
}

# Two people wrote this ledger, and the whole clue is that you can tell.
#
#   hand 0 = Ong Deok-gu. Twenty years of entries in a landowner's trained brush.
#   hand 1 = Bok-dong. He was a household slave (see the manumission deed, J11) and
#            never had a scholar's schooling, so his brush is the looser of the two.
#            He only starts appearing in the last two lines - that is the forgery.
#
# A hand is picked by what it can DRAW, not by which name comes first. The brush
# faces here were cut for Chinese: they carry no Hangul at all, and they drop the
# Korean-coined hanja on top of that. Crowning one of them the main hand meant
# every character it lacked fell through to the plain fallback face - half a sheet
# in one hand and half in another, which reads as a broken asset long before it
# reads as calligraphy. So: a face that draws all of it wins; failing that, the one
# that drops the fewest, and the shortfall is printed. The order below is still the
# tie-break, so putting a new font at the front still works.
function Pick-Face($cands, $texts, $exclude, $mustCoverAll) {
    $best = $null
    $bestMiss = [int]::MaxValue
    foreach ($cand in $cands) {
        if (-not ($installed -contains $cand)) { continue }
        if ($exclude -and $cand -eq $exclude) { continue }
        $miss = (Get-Missing $cand $texts).Length
        if ($miss -eq 0) { return $cand }
        if (-not $mustCoverAll -and $miss -lt $bestMiss) { $best = $cand; $bestMiss = $miss }
    }
    return $best
}

# Three faces, and each one is a person.
#
#   $fontName   tkFangSong   - the plain hanja face. Titles, seals, and anything the
#                              office itself wrote (the household register, the three
#                              case sheets). Nobody's handwriting: it is print.
#   $fontHand   Bakudai      - Ong Deok-gu's brush. Twenty years of trained, even
#                              entries in his own ledgers.
#   $fontAlt    AbbrFesFont  - Bok-dong's brush. A household slave who never had a
#                              scholar's schooling, so it wanders. He shows up in the
#                              last two lines - that is the forgery.
#
# The old lists stay behind each name as a fallback: fonts are art and live in the
# team share, so a fresh clone may not have them yet. If the named face is missing
# the bake still runs, and the log below says which face actually drew.
$fontName = Pick-Face @('tkFangSong','LXGW WenKai KR','LXGW WenKai TC','Ma Shan Zheng','HCR Batang','Batang','Gungsuh') $allText $null $false
if (-not $fontName) { $fontName = 'Batang' }

$fontHand = Pick-Face @('Bakudai','LXGW WenKai KR','Ma Shan Zheng','HCR Batang','Batang') $allText $null $false
if (-not $fontHand) { $fontHand = $fontName }

$fontAlt = Pick-Face @('AbbrFesFont','Long Cang','Liu Jian Mao Cao','Zhi Mang Xing','Yuji Mai','LXGW WenKai TC','BatangChe') $allText $fontHand $false
if (-not $fontAlt) {
    $fontAlt = $fontName
    Write-Host "  ! only one face draws every character - the two hands will differ by wobble alone."
    Write-Host "    Fonts are art, so they are not in git. Copy another full-coverage face from"
    Write-Host "    the team share into Assets\_Project\_Common\Art\Fonts and bake again."
}

# Brush faces drawn for Chinese drop hanja that Joseon paperwork needs - the
# Korean-coined ones above all. 畓 (paddy) has no Chinese counterpart at all, and
# traditional forms like 記 證 標 爲 錢 are often absent from simplified sets.
# A missing glyph renders as an empty box, so name it here instead of letting a
# tofu square ship as a clue.
function Report-MissingGlyphs($familyName, $texts) {
    $missing = Get-Missing $familyName $texts
    if ($missing.Length -gt 0) {
        Write-Host ("  ! '{0}' lacks {1} character(s), falling back to '{2}' for: {3}" -f $familyName, $missing.Length, $script:fallbackName, $missing)
    } else {
        Write-Host ("  '{0}': all characters covered" -f $familyName)
    }
}

Report-MissingGlyphs $fontName $allText
if ($fontHand -ne $fontName) { Report-MissingGlyphs $fontHand $allText }
if ($fontAlt -ne $fontName -and $fontAlt -ne $fontHand) { Report-MissingGlyphs $fontAlt $allText }

Write-Host "print: $fontName   Deok-gu: $fontHand   Bok-dong: $fontAlt"
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
            # The forged hand is bigger, leans, and wanders more. Subtle is worthless here:
            # the player is meant to notice from arm's length that two people wrote this.
            $sz = $baseSize * (0.88 + $rng.NextDouble() * 0.60)
            $gf = New-Object System.Drawing.Font($font.FontFamily, $sz, [System.Drawing.FontStyle]::Italic, [System.Drawing.GraphicsUnit]::Pixel)
            $made = $true
            $drift += ($rng.NextDouble() - 0.5) * ($jitter * 0.8)
            if ($drift -gt  $jitter * 1.6) { $drift =  $jitter * 1.6 }
            if ($drift -lt -$jitter * 1.6) { $drift = -$jitter * 1.6 }
        }

        # This face may simply not have the character. Swap in one that does rather
        # than stamping an empty box onto a clue.
        $fb = Resolve-Face $gf $ch
        $use = $gf
        if ($null -ne $fb) { $use = $fb }

        $cs = $g.MeasureString($s, $use)
        $brush = $ink
        $r = $rng.Next(0, 5)
        if ($r -eq 0) { $brush = $inkSoft }        # the brush had run dry
        $dx = $rng.Next(-$jitter, $jitter + 1) + $drift
        $dy = $rng.Next(-$jitter, $jitter + 1)
        $px0 = $x - $cs.Width / 2 + $dx
        $g.DrawString($s, $use, $brush, $px0, ($y + $dy))
        if ($sloppy -eq 1 -and $r -le 1) {         # ink pooled - stamped twice
            $g.DrawString($s, $use, $brush, ($px0 + 1.2), ($y + $dy + 0.9))
        }
        if ($null -ne $fb) { $fb.Dispose() }
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
    $sealFont   = New-Face $fontName 46 ([System.Drawing.FontStyle]::Bold)
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
    $titleFont = New-Face $fontName 118 ([System.Drawing.FontStyle]::Regular)
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

    $bodyFont = New-Face $fontName $fontSize ([System.Drawing.FontStyle]::Regular)
    $ci = 0
    foreach ($col in $doc.columns) {
        $x = $startX - ($ci * $colStep)
        Paint-Column $g $col $bodyFont $x $startY $charStep $ink $inkSoft 2 $rng | Out-Null
        $ci++
    }

    # A sheet slipped between the layers of the backing paper. Nothing shows on the
    # face; hold the sheet against a lamp and its shadow gathers into letters.
    #
    # Bake it as a second file of the same document (same seed, same columns) with
    # this field added - the game cross-fades that file in over the plain one while
    # the lantern is up. Painted faint and warm because it is being seen *through*
    # the paper, not on it: the fibres eat most of the contrast and the flame
    # pushes what is left toward amber.
    $hidden = Get-Field $doc 'hidden' $null
    if ($hidden) {
        $faint  = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(120, 92, 56, 28))
        $faint2 = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(88, 108, 68, 36))
        $hFont  = New-Face $fontName ($fontSize * 0.86) ([System.Drawing.FontStyle]::Regular)
        # Left margin - the visible columns march right to left from $startX, so the
        # far left is the one band they never reach on a short document.
        $hx = 108 + ($colStep * 0.45)
        $hi = 0
        foreach ($col in $hidden) {
            Paint-Column $g $col $hFont ($hx + $hi * $colStep) ($startY + 46) ($charStep * 0.86) $faint $faint2 4 $rng | Out-Null
            $hi++
        }
        $hFont.Dispose(); $faint.Dispose(); $faint2.Dispose()
        Write-Host ("  {0}: + hidden, {1} cols" -f $doc.file, $hidden.Count)
    }

    Paint-Seal $g (Get-Field $doc 'seal' '') 132 ($h - 262) 118 $fontName $rng

    $titleFont.Dispose(); $bodyFont.Dispose(); $ink.Dispose(); $inkSoft.Dispose()
}

# A sheet that was <b>under</b> another when someone wrote on it. No ink ever touched
# this paper - only the pressure of the brush above, which pushed the fibres down.
#
# So it is drawn with no ink at all. Each stroke is a shallow dent: a slightly darker
# edge where the light does not reach, and a slightly lighter one where it catches.
# On its own the sheet reads as blank; the marks only gather into letters when the
# light rakes across them - which is exactly what the magnifying glass does.
function Draw-Pressed($g, $doc, $w, $h, $fontName, $rng) {
    # Neither is ink. Both are the paper itself, lit from the upper left.
    # Deeper than a real impression would be. It costs nothing: the naked eye never sees
    # this sheet at all (it lives on a layer only the lens camera renders), so the only
    # question is whether it reads through the glass - and it must.
    $shade = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(152, 84, 72, 56))
    $glint = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(165, 255, 252, 242))

    $startY = 150
    $startX = $w - 150
    $maxUnits = Measure-Units $doc.columns
    $charStep = [Math]::Min(78.0, ($h - 260 - $startY) / $maxUnits)
    $fontSize = [Math]::Max(18.0, $charStep * 0.80)
    $colStep  = [Math]::Min(128.0, ($startX - 110) / [Math]::Max($doc.columns.Count - 1, 1))
    Write-Host ("  {0}: pressed, {1} cols, charStep {2:N1}, font {3:N1}" -f $doc.file, $doc.columns.Count, $charStep, $fontSize)

    $bodyFont = New-Face $fontName $fontSize ([System.Drawing.FontStyle]::Regular)
    $ci = 0
    foreach ($col in $doc.columns) {
        $x = $startX - ($ci * $colStep)
        # The dent is drawn twice, a hair apart. Light side first, shadow over it -
        # a stroke pushed into paper has its lit edge above and its dark edge below.
        Paint-Column $g $col $bodyFont ($x - 1.6) ($startY - 1.6) $charStep $glint $glint 3 $rng | Out-Null
        Paint-Column $g $col $bodyFont ($x + 1.4) ($startY + 1.4) $charStep $shade $shade 3 $rng | Out-Null
        $ci++
    }

    $bodyFont.Dispose(); $shade.Dispose(); $glint.Dispose()
}

# A case file (事目). The petition says why the crown cares; this is the sheet an
# inspector is handed when he reaches the place, and it says what he has to settle.
#
# It is a FORM, not a letter, and that is the whole difference: ruled cells, a
# portrait box for each of the two men, the household roster, and the things to
# establish. Everything inside is still vertical and right to left - the boxes are
# what make it read as a file rather than a plea.
#
# The portrait boxes are drawn EMPTY on purpose. This case is two men with one
# face; a file that could show the face would already have answered it.
#
# Fields: title, subtitle, seal, portraitNote, suspects [{mark, caption}],
#         rosterLabel, roster [], asksLabel, asks [], tagHead, tagNote
function Draw-File($g, $doc, $w, $h, $fontName, $rng) {
    $ink     = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(238, 38, 30, 24))
    $inkSoft = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(205, 62, 50, 38))
    $faint   = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(96, 96, 78, 58))
    $rule    = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(178, 56, 44, 32), 3)
    $hair    = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(112, 78, 62, 46), 2)

    $M = 92
    $inner = $w - $M * 2

    # ---- head ---------------------------------------------------------
    $titleFont = New-Face $fontName 112 ([System.Drawing.FontStyle]::Regular)
    $ts = $g.MeasureString($doc.title, $titleFont)
    $g.DrawString($doc.title, $titleFont, $ink, (($w - $ts.Width) / 2), 52)

    $subFont = New-Face $fontName 36 ([System.Drawing.FontStyle]::Regular)
    $sub = Get-Field $doc 'subtitle' ''
    if ($sub) {
        $ss = $g.MeasureString($sub, $subFont)
        $g.DrawString($sub, $subFont, $inkSoft, (($w - $ss.Width) / 2), 202)
    }
    $g.DrawLine($rule, $M, 262, ($w - $M), 262)
    $g.DrawLine($hair, $M, 272, ($w - $M), 272)

    # ---- the two men --------------------------------------------------
    # Right to left: the one named first in docs.json sits on the right, because
    # that is where a Joseon reader starts.
    $bandY = 300; $bandH = 366
    $g.DrawRectangle($rule, $M, $bandY, $inner, $bandH)

    $sus = Get-Field $doc 'suspects' @()
    $n = [Math]::Max($sus.Count, 1)
    $cellW = $inner / $n
    $capTexts = @()
    foreach ($su in $sus) { $capTexts += $su.caption }
    $capStep = 50.0
    if ($capTexts.Count -gt 0) {
        $capStep = [Math]::Min(50.0, ($bandH - 96) / (Measure-Units $capTexts))
    }
    $capFont  = New-Face $fontName ([Math]::Max(20.0, $capStep * 0.80)) ([System.Drawing.FontStyle]::Regular)
    $markFont = New-Face $fontName 62 ([System.Drawing.FontStyle]::Regular)
    $noteFont = New-Face $fontName 30 ([System.Drawing.FontStyle]::Regular)
    $pnote = Get-Field $doc 'portraitNote' ''

    for ($i = 0; $i -lt $sus.Count; $i++) {
        $cellRight = $w - $M - ($i * $cellW)
        $cellLeft  = $cellRight - $cellW
        if ($i -gt 0) { $g.DrawLine($hair, $cellRight, ($bandY + 14), $cellRight, ($bandY + $bandH - 14)) }

        # caption reads down the right edge of the cell
        Paint-Column $g $sus[$i].caption $capFont ($cellRight - 44) ($bandY + 30) $capStep $ink $inkSoft 2 $rng | Out-Null

        # empty portrait box
        $bx = $cellLeft + 30; $by = $bandY + 26
        $bw = $cellW - 30 - 92; $bh = $bandH - 106
        $g.DrawRectangle($hair, $bx, $by, $bw, $bh)
        if ($pnote) {
            Paint-Column $g $pnote $noteFont ($bx + $bw / 2) ($by + $bh / 2 - 34) 36 $faint $faint 2 $rng | Out-Null
        }

        # the mark under the box
        $ms = $g.MeasureString($sus[$i].mark, $markFont)
        $g.DrawString($sus[$i].mark, $markFont, $ink, ($bx + $bw / 2 - $ms.Width / 2), ($by + $bh + 6))
    }

    # ---- household roster ---------------------------------------------
    $rY = 692; $rH = 122
    $g.DrawRectangle($hair, $M, $rY, $inner, $rH)
    $labFont = New-Face $fontName 44 ([System.Drawing.FontStyle]::Regular)
    $rLabel = Get-Field $doc 'rosterLabel' ''
    $labW = 0
    if ($rLabel) {
        $ls = $g.MeasureString($rLabel, $labFont)
        $labW = $ls.Width + 40
        $g.DrawString($rLabel, $labFont, $ink, ($w - $M - $ls.Width - 22), ($rY + $rH / 2 - $ls.Height / 2))
        $g.DrawLine($hair, ($w - $M - $labW), ($rY + 12), ($w - $M - $labW), ($rY + $rH - 12))
    }
    $roster = Get-Field $doc 'roster' @()
    $rFont = New-Face $fontName 46 ([System.Drawing.FontStyle]::Regular)
    if ($roster.Count -gt 0) {
        $span = $inner - $labW - 24
        $step = $span / $roster.Count
        for ($i = 0; $i -lt $roster.Count; $i++) {
            $cx = $w - $M - $labW - 12 - ($i + 0.5) * $step
            $es = $g.MeasureString($roster[$i], $rFont)
            $g.DrawString($roster[$i], $rFont, $inkSoft, ($cx - $es.Width / 2), ($rY + $rH / 2 - $es.Height / 2))
        }
    }

    # ---- what has to be settled ----------------------------------------
    $aLabel = Get-Field $doc 'asksLabel' ''
    if ($aLabel) {
        $as = $g.MeasureString($aLabel, $labFont)
        $g.DrawString($aLabel, $labFont, $ink, ($w - $M - $as.Width), 836)
    }
    $aY = 896; $aH = 402
    $g.DrawRectangle($rule, $M, $aY, $inner, $aH)

    $asks = Get-Field $doc 'asks' @()
    if ($asks.Count -gt 0) {
        $maxUnits = Measure-Units $asks
        $charStep = [Math]::Min(52.0, ($aH - 56) / $maxUnits)
        $aFont = New-Face $fontName ([Math]::Max(22.0, $charStep * 0.80)) ([System.Drawing.FontStyle]::Regular)
        $colStep = [Math]::Min(238.0, ($inner - 124) / [Math]::Max($asks.Count - 1, 1))
        $startX = $w / 2 + ($asks.Count - 1) * $colStep / 2
        Write-Host ("  {0}: file, {1} asks, charStep {2:N1}" -f $doc.file, $asks.Count, $charStep)
        for ($i = 0; $i -lt $asks.Count; $i++) {
            $x = $startX - ($i * $colStep)
            if ($i -gt 0) { $g.DrawLine($hair, ($x + $colStep / 2), ($aY + 16), ($x + $colStep / 2), ($aY + $aH - 16)) }
            Paint-Column $g $asks[$i] $aFont $x ($aY + 28) $charStep $ink $inkSoft 2 $rng | Out-Null
        }
        $aFont.Dispose()
    }

    # ---- the red slip pasted at the foot --------------------------------
    # A 첨지: a strip of red paper stuck onto a file to say what the office already
    # suspects. It is not part of the document - it sits on top of it, a little
    # askew, because a hand put it there.
    $tagHead = Get-Field $doc 'tagHead' ''
    if ($tagHead) {
        $state = $g.Save()
        $g.TranslateTransform(548, 1322)
        $g.RotateTransform(-2.6)
        $tw = 390; $th = 96
        $red = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(214, 162, 44, 36))
        $g.FillRectangle($red, 0, 0, $tw, $th)
        $paper = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(236, 238, 226, 196))
        $thFont = New-Face $fontName 52 ([System.Drawing.FontStyle]::Bold)
        $hs = $g.MeasureString($tagHead, $thFont)
        $g.DrawString($tagHead, $thFont, $paper, 22, ($th / 2 - $hs.Height / 2))
        $tagNote = Get-Field $doc 'tagNote' ''
        if ($tagNote) {
            $tnFont = New-Face $fontName 28 ([System.Drawing.FontStyle]::Regular)
            $ns = $g.MeasureString($tagNote, $tnFont)
            $g.DrawString($tagNote, $tnFont, $paper, (22 + $hs.Width + 10), ($th / 2 - $ns.Height / 2))
            $tnFont.Dispose()
        }
        # the paste is uneven - break the block so it reads as stuck on, not printed
        for ($i = 0; $i -lt 90; $i++) {
            $r = $rng.Next(2, 9)
            $c = [System.Drawing.Color]::FromArgb($rng.Next(18, 54), 231, 217, 186)
            $br = New-Object System.Drawing.SolidBrush($c)
            $g.FillEllipse($br, $rng.Next(0, $tw), $rng.Next(0, $th), $r, $r)
            $br.Dispose()
        }
        $g.Restore($state)
        $red.Dispose(); $paper.Dispose(); $thFont.Dispose()
    }

    # 118 is the size Paint-Seal is cut for: it stacks glyphs at a fixed 52px step,
    # so anything smaller drops the second character out the bottom of the red block.
    Paint-Seal $g (Get-Field $doc 'seal' '') ($M + 16) 1292 118 $fontName $rng

    $titleFont.Dispose(); $subFont.Dispose(); $capFont.Dispose(); $markFont.Dispose()
    $noteFont.Dispose(); $labFont.Dispose(); $rFont.Dispose()
    $ink.Dispose(); $inkSoft.Dispose(); $faint.Dispose(); $rule.Dispose(); $hair.Dispose()
}

# Account book. Entries run right to left, one per ruled column.
# hand 1 is a different font, size and jitter - that difference IS the clue.
function Draw-Ledger($g, $doc, $w, $h, $fontName, $fontHand, $fontAlt, $rng) {
    $ink     = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(238, 38, 30, 24))
    $inkSoft = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(210, 52, 42, 32))
    # Bok-dong writes with a thinner, waterier brush than the master's - it is not
    # his ink and not his hand, and both should show.
    $ink2     = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(196, 62, 52, 42))
    $ink2Soft = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(150, 78, 66, 52))

    $titleFont = New-Face $fontName 84 ([System.Drawing.FontStyle]::Regular)
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

    # Hand 0 is Deok-gu's own brush, not the office's print face. The title above
    # stays print - a ledger's header is ruled and stamped, the entries are written.
    $fontA = New-Face $fontHand $fontSize ([System.Drawing.FontStyle]::Regular)
    # Regular, not Italic. A synthesised italic on a CJK face makes MeasureString
    # report nonsense widths, which shoved these columns clean off the sheet -
    # and Joseon documents have no such thing as an italic anyway.
    $fontB = New-Face $fontAlt ($fontSize * 1.08) ([System.Drawing.FontStyle]::Italic)
    # hand 2 = nobody's handwriting. The household register is the office's own book -
    # ruled, stamped and printed - so it is set in the print face like the titles.
    $fontC = New-Face $fontName $fontSize ([System.Drawing.FontStyle]::Regular)

    $markFont  = New-Face $fontName ($fontSize * 1.05) ([System.Drawing.FontStyle]::Bold)
    $markBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(200, 132, 40, 32))

    $ci = 0
    foreach ($e in $entries) {
        $x = $rightX - ($ci * $colStep)
        $hand = Get-Field $e 'hand' 0
        if ($hand -eq 1) {
            $endY = Paint-Column $g $e.text $fontB $x ($gridTop + 34) ($charStep * 1.10) $ink2 $ink2Soft 11 $rng 1
        } elseif ($hand -eq 2) {
            # Print: no wobble at all. Jitter 0 is what makes it read as set, not written.
            $endY = Paint-Column $g $e.text $fontC $x ($gridTop + 34) $charStep $ink $ink 0 $rng
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

    # ------------------------------------------------------------------
    # A line that was scraped off and written over.
    #
    # 'hidden' on a ledger is not a whole second page - it is one column that
    # used to be there. Backlit, the fibres under the scraped patch are thinner
    # and the old ink that soaked in shows as a warm ghost beside the new entry.
    # So it is painted faint, warm and slightly small, in the slot named by
    # 'hiddenAt', nudged aside so both can be read at once.
    #
    # Bake this as a twin file (..._lit) of the same document - same seed, same
    # entries - with these two fields added. The game cross-fades that file in
    # over the plain one while the lantern is behind the paper.
    $hidden = Get-Field $doc 'hidden' $null
    $hAt    = [int](Get-Field $doc 'hiddenAt' -1)
    if ($hidden -and $hAt -ge 0) {
        $faint  = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(132, 96, 58, 28))
        $faint2 = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(96, 112, 70, 36))
        $hFont  = New-Face $fontName ($fontSize * 0.90) ([System.Drawing.FontStyle]::Regular)
        $hx     = $rightX - ($hAt * $colStep) - ($colStep * 0.30)
        $hi     = 0
        foreach ($col in $hidden) {
            Paint-Column $g $col $hFont ($hx - $hi * $colStep) ($gridTop + 40) ($charStep * 0.90) $faint $faint2 3 $rng | Out-Null
            $hi++
        }
        $hFont.Dispose(); $faint.Dispose(); $faint2.Dispose()
        Write-Host ("  {0}: + scraped line at entry {1}" -f $doc.file, $hAt)
    }

    $titleFont.Dispose(); $fontA.Dispose(); $fontB.Dispose(); $fontC.Dispose(); $markFont.Dispose()
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
    elseif ($kind -eq 'pressed') { $W = 1024; $H = 1024 }
    else { $W = 1024; $H = 1448 }        # doc, file - A3 portrait

    # Keep the shape it was drawn at.
    #
    # This used to squeeze every sheet into a 1024 square, and DocumentView sizes the
    # quad from the TEXTURE - so an A3 petition came out 30% shorter than it was drawn
    # and a ledger spread came out 30% narrower. The view already means to preserve the
    # proportion ("a landscape document stretched to a square makes the writing
    # unreadable" is written right there); it was the bake that took it away.
    #
    # Not a power of two any more, but both sides stay multiples of four, which is all
    # DXT needs. 1024x1448 and 1448x1024 both qualify.
    $OUTW = $W
    $OUTH = $H

    $canvas = New-Canvas $W $H
    $bmp = $canvas.bmp
    $g   = $canvas.g

    Paint-Paper $g $W $H $rng

    if ($kind -eq 'ledger') {
        Draw-Ledger $g $doc $W $H $fontName $fontHand $fontAlt $rng
    }
    elseif ($kind -eq 'pressed') {
        Draw-Pressed $g $doc $W $H $fontName $rng
    }
    elseif ($kind -eq 'file') {
        Draw-File $g $doc $W $H $fontName $rng
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
        $bodyFont = New-Face $fontName $fontSize ([System.Drawing.FontStyle]::Regular)
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
    $pot = New-Object System.Drawing.Bitmap($OUTW, $OUTH)
    $pg  = [System.Drawing.Graphics]::FromImage($pot)
    $pg.Clear([System.Drawing.Color]::Transparent)
    $pg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $pg.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $pg.DrawImage($bmp, (New-Object System.Drawing.Rectangle(0, 0, $OUTW, $OUTH)))
    $pot.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $pg.Dispose(); $pot.Dispose()
    Write-Host "  wrote $outPath"

    $g.Dispose(); $bmp.Dispose()
}
Write-Host 'done'
