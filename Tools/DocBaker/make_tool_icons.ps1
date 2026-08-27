# Draws the toolbelt icons (lantern / magnifier / journal / map) as flat ink-and-paper
# silhouettes on a transparent background, so they read at HUD size in a dark scene.
#
# Run from Unity:  menu [이문록 > 에셋: 도구 아이콘 굽기]
# Run by hand:     powershell -ExecutionPolicy Bypass -File Tools\DocBaker\make_tool_icons.ps1
#
# Source is ASCII-only on purpose (same rule as make_docs.ps1). Output goes to
# Assets/_Project/Art/UI/Icons - these are UI, so unlike the document textures they
# are small and are kept in git.
param(
    [string]$ProjectRoot
)

Add-Type -AssemblyName System.Drawing

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $ProjectRoot) { $ProjectRoot = (Resolve-Path (Join-Path $here '..\..')).Path }

$outDir = Join-Path $ProjectRoot 'Assets\_Project\Art\UI\Icons'
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Force $outDir | Out-Null }
Write-Host "out : $outDir"

# Light on dark: the toolbelt slot behind these is near-black (ToolbeltPanel._slotColor),
# so the strokes carry the light tone and the fills stay dark. INK/PAPER keep their names
# for what they draw - the outline and the sheet - not for their brightness.
$S = 256
$INK   = [System.Drawing.Color]::FromArgb(255, 244, 234, 210)   # strokes / solid parts
$PAPER = [System.Drawing.Color]::FromArgb(255, 58, 50, 41)      # sheet interior
$FLAME = [System.Drawing.Color]::FromArgb(255, 240, 178, 74)

function New-Icon {
    $bmp = New-Object System.Drawing.Bitmap($S, $S)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)
    return @{ bmp = $bmp; g = $g }
}

function Save-Icon($bmp, $name) {
    $p = Join-Path $outDir ("T_Icon_" + $name + ".png")
    $bmp.Save($p, [System.Drawing.Imaging.ImageFormat]::Png)
    Write-Host "  wrote $p"
}

# --- journal: a stitch-bound booklet, seen slightly open ---
$c = New-Icon; $g = $c.g
$inkB   = New-Object System.Drawing.SolidBrush($INK)
$paperB = New-Object System.Drawing.SolidBrush($PAPER)
$inkP   = New-Object System.Drawing.Pen($INK, 9)
$g.FillRectangle($inkB, 52, 34, 152, 188)                 # cover
$g.FillRectangle($paperB, 74, 48, 116, 160)               # page block
for ($i = 0; $i -lt 4; $i++) {                            # binding stitches down the spine
    $g.FillRectangle($paperB, 58, (60 + $i * 42), 10, 22)
}
$g.DrawLine($inkP, 96, 88, 168, 88)                       # a couple of written lines
$g.DrawLine($inkP, 96, 124, 168, 124)
$g.DrawLine($inkP, 96, 160, 140, 160)
Save-Icon $c.bmp 'journal'
$g.Dispose(); $c.bmp.Dispose()

# --- map: an unrolled scroll with a route across it ---
$c = New-Icon; $g = $c.g
$g.FillRectangle($paperB, 48, 76, 160, 104)               # sheet
$outline = New-Object System.Drawing.Pen($INK, 7)
$g.DrawRectangle($outline, 48, 76, 160, 104)
$g.FillRectangle($inkB, 26, 62, 30, 132)                  # rolled ends
$g.FillRectangle($inkB, 200, 62, 30, 132)
$route = New-Object System.Drawing.Pen($INK, 8)
$route.DashStyle = [System.Drawing.Drawing2D.DashStyle]::Dash
$g.DrawCurve($route, [System.Drawing.Point[]]@(
    (New-Object System.Drawing.Point(70, 156)),
    (New-Object System.Drawing.Point(104, 112)),
    (New-Object System.Drawing.Point(146, 146)),
    (New-Object System.Drawing.Point(186, 100))))
$g.FillEllipse($inkB, 176, 90, 22, 22)                    # destination mark
Save-Icon $c.bmp 'map'
$route.Dispose(); $outline.Dispose()
$g.Dispose(); $c.bmp.Dispose()

# --- lantern: a hanging cheorong with a lit wick ---
$c = New-Icon; $g = $c.g
$hook = New-Object System.Drawing.Pen($INK, 9)
$g.DrawArc($hook, 110, 12, 36, 36, 200, 250)              # carrying hook
$g.DrawLine($hook, 128, 44, 128, 62)
$g.FillRectangle($inkB, 78, 58, 100, 18)                  # top cap
$body = New-Object System.Drawing.Drawing2D.GraphicsPath  # tapered paper body
$body.AddPolygon([System.Drawing.Point[]]@(
    (New-Object System.Drawing.Point(88, 76)),
    (New-Object System.Drawing.Point(168, 76)),
    (New-Object System.Drawing.Point(182, 186)),
    (New-Object System.Drawing.Point(74, 186))))
$g.FillPath($paperB, $body)
$bodyPen = New-Object System.Drawing.Pen($INK, 8)
$g.DrawPath($bodyPen, $body)
$g.DrawLine($bodyPen, 96, 110, 160, 110)                  # bamboo ribs
$g.DrawLine($bodyPen, 92, 150, 164, 150)
$flameB = New-Object System.Drawing.SolidBrush($FLAME)
$g.FillEllipse($flameB, 114, 116, 28, 40)                 # flame
$g.FillRectangle($inkB, 68, 186, 120, 18)                 # base ring
Save-Icon $c.bmp 'lantern'
$body.Dispose(); $bodyPen.Dispose(); $hook.Dispose(); $flameB.Dispose()
$g.Dispose(); $c.bmp.Dispose()

# --- magnifier: ring and handle, angled like it is being held ---
$c = New-Icon; $g = $c.g
$handle = New-Object System.Drawing.Pen($INK, 30)
$handle.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$handle.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round
$g.DrawLine($handle, 152, 152, 214, 214)
$g.FillEllipse($paperB, 40, 40, 130, 130)                 # lens
$ring = New-Object System.Drawing.Pen($INK, 20)
$g.DrawEllipse($ring, 40, 40, 130, 130)
$glint = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(150, 255, 255, 255), 10)
$glint.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$glint.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round
$g.DrawArc($glint, 62, 62, 86, 86, 170, 70)               # highlight so it reads as glass
Save-Icon $c.bmp 'magnify'
$handle.Dispose(); $ring.Dispose(); $glint.Dispose()
$g.Dispose(); $c.bmp.Dispose()

$inkB.Dispose(); $paperB.Dispose(); $inkP.Dispose()
Write-Host 'done'
