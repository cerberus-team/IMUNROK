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
#   file   - case file (사목): what the county clerk wrote down and could not settle.
#            Korean and horizontal on purpose - it is read, not admired. The plea,
#            the two men, the household, the question the office failed to answer,
#            and a 未決 stamp struck across the foot. It carries NO method: what the
#            answer hangs on is for the player to find.
#            Fields: title, subtitle, filed, seal, stamp, cards [{mark,role,lines[]}],
#                    tie [], pleaLabel, plea [], rosterLabel, roster [], rosterNote,
#                    asksLabel, asks [], clerkNote
param(
    [string]$ProjectRoot
)

Add-Type -AssemblyName System.Drawing

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $ProjectRoot) { $ProjectRoot = (Resolve-Path (Join-Path $here '..\..')).Path }

$docs = Get-Content (Join-Path $here 'docs.json') -Raw -Encoding UTF8 | ConvertFrom-Json

# ---------------------------------------------------------------------------
# The same sheet, read out in Hangul.
#
# The player is handed a page of hanja and cannot read it. Printing a Korean
# line underneath turns the document into a subtitle, and writing Korean ON the
# paper wrecks it as a Joseon document. So we bake the sheet TWICE - once in
# hanja and once, in exactly the same layout, in Korean - and the game shows the
# Korean twin only through the magnifier. Look through the glass and the column
# you are over turns into Korean; take the glass away and the hanja is back.
#
# A twin is baked for any document whose entries carry "ko" (and/or "titleKo").
# Everything else about it - the ruling, the seal, the marks - is drawn by the
# same code from the same fields, so the two sheets line up character-cell for
# character-cell without anybody keeping two layouts in step by hand.
$koDocs = @()
foreach ($d in $docs) {
    $hasKo = $false
    if ($d.PSObject.Properties.Name -contains 'titleKo'    -and $d.titleKo)    { $hasKo = $true }
    if ($d.PSObject.Properties.Name -contains 'columnsKo'  -and $d.columnsKo)  { $hasKo = $true }
    if ($d.PSObject.Properties.Name -contains 'entries'    -and $d.entries) {
        foreach ($e in $d.entries) {
            if ($e.PSObject.Properties.Name -contains 'ko' -and $e.ko) { $hasKo = $true }
        }
    }
    if (-not $hasKo) { continue }

    $c = $d | ConvertTo-Json -Depth 12 | ConvertFrom-Json
    $c.file = $d.file + '_ko'
    if ($d.PSObject.Properties.Name -contains 'titleKo'   -and $d.titleKo)   { $c.title = $d.titleKo }
    if ($d.PSObject.Properties.Name -contains 'columnsKo' -and $d.columnsKo) { $c.columns = $d.columnsKo }
    if ($c.PSObject.Properties.Name -contains 'entries' -and $c.entries) {
        for ($i = 0; $i -lt @($c.entries).Count; $i++) {
            $src = @($d.entries)[$i]
            if ($src.PSObject.Properties.Name -contains 'ko' -and $src.ko) { @($c.entries)[$i].text = $src.ko }
        }
    }
    $koDocs += $c
}
if ($koDocs.Count -gt 0) {
    Write-Host ("  hangul twins: {0}" -f $koDocs.Count)
    $docs = @($docs) + $koDocs
}

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
    foreach ($f in @('subtitle','filed','stamp','pleaLabel','rosterLabel','rosterNote','asksLabel','clerkNote')) {
        if ($d.PSObject.Properties.Name -contains $f -and $d.$f) { $allText += $d.$f }
    }
    foreach ($f in @('roster','asks','tie','plea')) {
        if ($d.PSObject.Properties.Name -contains $f -and $d.$f) { $allText += $d.$f }
    }
    if ($d.PSObject.Properties.Name -contains 'cards' -and $d.cards) {
        foreach ($cd in $d.cards) { $allText += $cd.mark; $allText += $cd.role; $allText += $cd.lines }
    }
}

# The two brushes never draw Hangul.
#
# The hands sign the LEDGER ENTRIES, and those are hanja from end to end. The Hangul
# in this pile belongs to the case sheets, which are print and are drawn by $fontName.
# Testing a brush against all of it therefore asks it for letters it will never be
# handed - and every brush face here was cut for Chinese, so it carries no Hangul at
# all. It fails on the first syllable, HCR Batang covers everything, and the picker
# hands Deok-gu's brush to the same face that prints the office ledgers.
#
# Which quietly ate the clue. J06 is "the hand changes a month ago"; if the trained
# hand and the printed hand are one font, the sheet has nothing to show. So the
# brushes are measured against the hanja only.
$handText = @()
foreach ($t in $allText) {
    $sb = New-Object System.Text.StringBuilder
    foreach ($ch in $t.ToCharArray()) {
        $c = [int]$ch
        if ($c -ge 0xAC00 -and $c -le 0xD7A3) { continue }   # 가~힣
        if ($c -ge 0x3130 -and $c -le 0x318F) { continue }   # ㄱ~ㅣ
        [void]$sb.Append($ch)
    }
    if ($sb.Length -gt 0) { $handText += $sb.ToString() }
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

$fontHand = Pick-Face @('Bakudai','LXGW WenKai KR','Ma Shan Zheng','HCR Batang','Batang') $handText $null $false
if (-not $fontHand) { $fontHand = $fontName }

$fontAlt = Pick-Face @('AbbrFesFont','Long Cang','Liu Jian Mao Cao','Zhi Mang Xing','Yuji Mai','LXGW WenKai TC','BatangChe') $handText $fontHand $false
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

# ---------------------------------------------------------------------------
# The same two hands, in Hangul.
#
# The hanja brushes (Bakudai, AbbrFesFont) were cut for Chinese and carry no Hangul
# at all. On the Korean twin every glyph therefore fell back to ONE substitute face -
# so the forged stretch and the master's stretch came out in the same letters, and the
# whole clue of J06 ("the hand changes a month ago") was invisible exactly where the
# player can finally read the sheet.
#
# The wobble still differed (size, drift, watery ink, double-stamping), but wobble
# alone on identical letters reads as "badly printed", not as "a different person".
#
# So the Korean twin gets its own three faces, chosen against Hangul:
#   ko print    - the office's own book. Even, set, no wobble.
#   ko Deok-gu  - a brush face (Gungsuh is 궁서: a writing-brush hand).
#   ko Bok-dong - a DIFFERENT face from Deok-gu's, whatever else is on the machine.
#                 Paired with the wobble it already gets, two people now show.
$koText = @()
foreach ($t in $allText) {
    $sb = New-Object System.Text.StringBuilder
    foreach ($ch in $t.ToCharArray()) {
        $c = [int]$ch
        if (($c -ge 0xAC00 -and $c -le 0xD7A3) -or ($c -ge 0x3130 -and $c -le 0x318F) -or $c -lt 0x80) {
            [void]$sb.Append($ch)
        }
    }
    if ($sb.Length -gt 0) { $koText += $sb.ToString() }
}
# NOTE: font family names come back LOCALISED. On this Korean Windows the collection
# reports 궁서 / 바탕체 / 함초롬바탕 - never Gungsuh / BatangChe / HCR Batang. Listing
# only the English spellings found nothing, every pick fell through to the print face,
# and the twin came out in one hand again. Both spellings are listed; Pick-Face skips
# whatever is not installed, so the same list works on either machine.
# The office face is 궁서 too. 조선100년체 read as a typeface - even, geometric, a
# sign-painter's letter - and on a deed title (별급문기) it stood out as "a font" rather
# than as brushwork. 궁서 is the palace brush script the office would actually have used.
$script:fontNameKo = Pick-Face @('궁서','Gungsuh','조선100년체','Chosun Centennial','함초롬바탕','HCR Batang','바탕','Batang') $koText $null $false
if (-not $script:fontNameKo) { $script:fontNameKo = $fontName }

# Deok-gu: 궁서 is a writing-brush face - twenty years of a trained hand.
$script:fontHandKo = Pick-Face @('궁서','Gungsuh','조선100년체','Chosun Centennial','바탕','Batang') $koText $null $false
if (-not $script:fontHandKo) { $script:fontHandKo = $script:fontNameKo }

# Bok-dong: a plain block serif, not a brush. He never had a scholar's schooling, and
# it is the wobble (size, drift, watery ink) that finishes the difference.
$script:fontAltKo = Pick-Face @('바탕체','BatangChe','함초롬바탕','HCR Batang','조선100년체','굴림체','Gulim') $koText $script:fontHandKo $false
if (-not $script:fontAltKo) {
    $script:fontAltKo = $script:fontHandKo
    Write-Host "  ! only one Hangul face here - the two hands on the Korean twin differ by wobble alone."
}

Write-Host "print: $fontName   Deok-gu: $fontHand   Bok-dong: $fontAlt"
Write-Host ("ko   : {0}   Deok-gu: {1}   Bok-dong: {2}" -f $script:fontNameKo, $script:fontHandKo, $script:fontAltKo)
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
    # The hanja faces carry no Hangul. Without this the Korean twin of a deed fell back to
    # whatever sans face GDI+ found (a plain gothic), so 별급문기 printed like a signboard.
    if ($doc.file -like '*_ko') { $fontName = $script:fontNameKo }
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

# A case file (사목) - and it matters WHO wrote it.
#
# The clerk of the county office did. He took the plea, stood the two men side by
# side, failed to tell them apart, stamped the sheet 未決 and sent it up. That is
# the whole of what this paper knows, and the stamp is the reason the inspector is
# standing here at all.
#
# The draft before this one had grown into a solution: it listed the household
# slave by name, and it told the reader that the answer lay in the documents and in
# marks on the body. Both of those are things the player is supposed to FIND. A
# briefing that hands over the method is not a briefing, it is the ending read out
# in advance. So this sheet carries only the plea, the two claims, what the office
# could see with its own eyes, and the question it could not answer.
#
# Fields: title, subtitle, filed, seal, stamp,
#         cards [{mark, role, lines[]}], tie[],
#         pleaLabel, plea[],
#         rosterLabel, roster[], rosterNote,
#         asksLabel, asks[], clerkNote
function Draw-File($g, $doc, $w, $h, $fontName, $rng) {
    $ink     = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(240, 36, 28, 22))
    $inkSoft = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(206, 68, 56, 42))
    $inkFar  = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(158, 94, 78, 60))
    $red     = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(226, 150, 44, 34))
    $rule    = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(180, 54, 42, 30), 3)
    $hair    = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(108, 84, 68, 50), 2)

    $M = 84
    $inner = $w - $M * 2
    $kor = $script:fallbackName

    function Line($text, $font, $brush, $x, $y) {
        if (-not $text) { return }
        $g.DrawString($text, $font, $brush, $x, $y)
    }
    function Mid($text, $font, $brush, $cx, $y) {
        if (-not $text) { return }
        $s = $g.MeasureString($text, $font)
        $g.DrawString($text, $font, $brush, ($cx - $s.Width / 2), $y)
    }
    function Right($text, $font, $brush, $rx, $y) {
        if (-not $text) { return }
        $s = $g.MeasureString($text, $font)
        $g.DrawString($text, $font, $brush, ($rx - $s.Width), $y)
    }

    # ---- head ---------------------------------------------------------
    $titleFont = New-Face $fontName 84 ([System.Drawing.FontStyle]::Regular)
    Mid $doc.title $titleFont $ink ($w / 2) 42

    $subFont = New-Face $kor 32 ([System.Drawing.FontStyle]::Regular)
    Mid (Get-Field $doc 'subtitle' '') $subFont $inkSoft ($w / 2) 160

    $g.DrawLine($rule, $M, 218, ($w - $M), 218)
    $smallFont = New-Face $kor 25 ([System.Drawing.FontStyle]::Regular)
    Right (Get-Field $doc 'filed' '') $smallFont $inkFar ($w - $M) 226

    # ---- the two men ---------------------------------------------------
    $cards = Get-Field $doc 'cards' @()
    $cardW = 388; $cardH = 236
    $cardY = 274
    $gapX  = $inner - $cardW * 2
    $markFont = New-Face $fontName 58 ([System.Drawing.FontStyle]::Regular)
    $roleFont = New-Face $kor 26 ([System.Drawing.FontStyle]::Bold)
    $cardFont = New-Face $kor 28 ([System.Drawing.FontStyle]::Regular)

    for ($i = 0; $i -lt $cards.Count -and $i -lt 2; $i++) {
        $x = $M + $i * ($cardW + $gapX)
        $g.DrawRectangle($hair, $x, $cardY, $cardW, $cardH)
        $cx = $x + $cardW / 2
        Mid $cards[$i].mark $markFont $ink $cx ($cardY + 8)
        Mid (Get-Field $cards[$i] 'role' '') $roleFont $red $cx ($cardY + 88)
        $ly = $cardY + 130
        foreach ($ln in $cards[$i].lines) {
            Mid $ln $cardFont $inkSoft $cx $ly
            $ly += 42
        }
    }

    $tie = Get-Field $doc 'tie' @()
    $g.DrawLine($hair, ($M + $cardW), ($cardY + $cardH / 2), ($M + $cardW + $gapX), ($cardY + $cardH / 2))
    $tieY = $cardY + $cardH + 20
    $tieFont  = New-Face $kor 33 ([System.Drawing.FontStyle]::Regular)
    $tieFont2 = New-Face $kor 28 ([System.Drawing.FontStyle]::Regular)
    if ($tie.Count -gt 0) { Mid $tie[0] $tieFont $ink ($w / 2) $tieY }
    if ($tie.Count -gt 1) { Mid $tie[1] $tieFont2 $inkSoft ($w / 2) ($tieY + 42) }

    # ---- the plea, in the man's own words ---------------------------------
    # Set in from the margin and closed with quotation marks, because this is the one
    # block on the sheet the office did not write - it only wrote it down.
    $headFont = New-Face $kor 31 ([System.Drawing.FontStyle]::Bold)
    $py = 632
    Line (Get-Field $doc 'pleaLabel' '') $headFont $ink $M $py
    $g.DrawLine($hair, $M, ($py + 44), ($w - $M), ($py + 44))
    $pFont = New-Face $kor 30 ([System.Drawing.FontStyle]::Regular)
    $ly = $py + 62
    foreach ($q in (Get-Field $doc 'plea' @())) {
        Line $q $pFont $inkSoft ($M + 46) $ly
        $ly += 44
    }
    # a quiet vertical stroke down the left of the quotation
    $g.DrawLine($hair, ($M + 20), ($py + 68), ($M + 20), ($ly - 8))

    # ---- who is in the house -------------------------------------------
    $ry = 830
    Line (Get-Field $doc 'rosterLabel' '') $headFont $ink $M $ry
    $g.DrawLine($hair, $M, ($ry + 44), ($w - $M), ($ry + 44))
    $roster = Get-Field $doc 'roster' @()
    $rFont = New-Face $kor 32 ([System.Drawing.FontStyle]::Regular)
    if ($roster.Count -gt 0) {
        $step = $inner / $roster.Count
        for ($i = 0; $i -lt $roster.Count; $i++) {
            Mid $roster[$i] $rFont $inkSoft ($M + ($i + 0.5) * $step) ($ry + 60)
        }
    }
    $noteFont = New-Face $kor 26 ([System.Drawing.FontStyle]::Regular)
    Mid (Get-Field $doc 'rosterNote' '') $noteFont $inkFar ($w / 2) ($ry + 112)

    # ---- what the office could not answer ---------------------------------
    $ay = 1010
    Line (Get-Field $doc 'asksLabel' '') $headFont $ink $M $ay
    $g.DrawLine($rule, $M, ($ay + 44), ($w - $M), ($ay + 44))
    $aFont = New-Face $kor 33 ([System.Drawing.FontStyle]::Regular)
    $ly = $ay + 64
    foreach ($a in (Get-Field $doc 'asks' @())) {
        Line $a $aFont $ink ($M + 12) $ly
        $ly += 52
    }

    $clerkFont = New-Face $kor 26 ([System.Drawing.FontStyle]::Regular)
    Line (Get-Field $doc 'clerkNote' '') $clerkFont $inkFar ($M + 12) ($ly + 12)

    # ---- the stamp -------------------------------------------------------
    # 未決 - not settled. The county magistrate could not tell them apart, so the
    # paper went up. This one mark is why an inspector is standing in the village.
    # Struck askew and a shade off-register, the way a hand stamps a hundred of these.
    $stamp = Get-Field $doc 'stamp' ''
    if ($stamp) {
        $state = $g.Save()
        $g.TranslateTransform(724, 1176)
        $g.RotateTransform(-9.5)
        $size = 196
        $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(212, 168, 46, 36), 11)
        $g.DrawRectangle($pen, 0, 0, $size, $size)
        $inner2 = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(150, 168, 46, 36), 3)
        $g.DrawRectangle($inner2, 14, 14, ($size - 28), ($size - 28))
        $sf = New-Face $fontName 96 ([System.Drawing.FontStyle]::Bold)
        $sb2 = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(220, 168, 46, 36))
        $i2 = 0
        foreach ($ch in $stamp.ToCharArray()) {
            $s = [string]$ch
            $cs = $g.MeasureString($s, $sf)
            $g.DrawString($s, $sf, $sb2, ($size / 2 - $cs.Width / 2), (12 + $i2 * 90))
            $i2++
        }
        # ink does not take evenly on hanji - eat holes out of it
        for ($i = 0; $i -lt 240; $i++) {
            $r = $rng.Next(2, 10)
            $c = [System.Drawing.Color]::FromArgb($rng.Next(40, 120), 231, 217, 186)
            $br = New-Object System.Drawing.SolidBrush($c)
            $g.FillEllipse($br, $rng.Next(-6, $size), $rng.Next(-6, $size), $r, $r)
            $br.Dispose()
        }
        $g.Restore($state)
        $pen.Dispose(); $inner2.Dispose(); $sf.Dispose(); $sb2.Dispose()
    }

    Paint-Seal $g (Get-Field $doc 'seal' '') ($M + 8) 1300 112 $fontName $rng

    $titleFont.Dispose(); $subFont.Dispose(); $smallFont.Dispose()
    $markFont.Dispose(); $roleFont.Dispose(); $cardFont.Dispose()
    $tieFont.Dispose(); $tieFont2.Dispose(); $headFont.Dispose()
    $pFont.Dispose(); $rFont.Dispose(); $noteFont.Dispose(); $aFont.Dispose(); $clerkFont.Dispose()
    $ink.Dispose(); $inkSoft.Dispose(); $inkFar.Dispose(); $red.Dispose()
    $rule.Dispose(); $hair.Dispose()
}

# Account book. Entries run right to left, one per ruled column.
# hand 1 is a different font, size and jitter - that difference IS the clue.
function Draw-Ledger($g, $doc, $w, $h, $fontName, $fontHand, $fontAlt, $rng) {
    # The Korean twin is drawn by the same code from the same fields - only the three
    # faces change. Swapping here (and not at the call) keeps the ruling, the column
    # steps and the marks identical between the two sheets, which is what lets the
    # magnifier lay one over the other.
    if ($doc.file -like '*_ko') {
        $fontName = $script:fontNameKo
        $fontHand = $script:fontHandKo
        $fontAlt  = $script:fontAltKo
    }
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
    # Bok-dong's hand steps 1.10x wider (below). Fit the grid to HIS pace when he wrote
    # any of it, or his longest column runs out through the bottom rule - the Korean
    # twin of the household return did exactly that once 본관 was added to the head line.
    foreach ($e in $entries) { if ((Get-Field $e 'hand' 0) -eq 1) { $charStep = $charStep / 1.10; break } }
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
        # <b>The outline must be the same sheet on every pass.</b>
        #
        # New-FragmentPath draws from the running rng, and how many draws have happened
        # by now depends on how much text this page had. So the front, its Korean twin
        # and its BACK each got a different tear - flip the scrap over and the hole was
        # somewhere else, which reads as two pieces of paper rather than two sides of one.
        #
        # Seed from the base name (with _ko / _back taken off) so every face of one
        # scrap tears the same way.
        $baseName = $doc.file -replace '_(ko|back)$', ''
        $seed = 0
        foreach ($ch in $baseName.ToCharArray()) { $seed = ($seed * 31 + [int]$ch) % 2147483647 }
        $rng = New-Object System.Random($seed)
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
