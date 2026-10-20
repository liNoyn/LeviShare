# Draws LeviShare's application icon (a pixel-art box with an outgoing arrow) and writes
# src/LeviShare.App/Assets/levishare.ico. Original artwork, no game assets.
#   powershell -ExecutionPolicy Bypass -File tools/make-icon.ps1
Add-Type -AssemblyName System.Drawing
$out = Join-Path $PSScriptRoot '..\src\LeviShare.App\Assets'
New-Item -ItemType Directory -Force $out | Out-Null

# 10x10 art: a green crate (mods/packs) with a white "share" arrow leaving it.
$art = @(
  '......WWWW',
  '.......WWW',
  '......W.WW',
  '.....W...W',
  'DDDDW.....',
  'DGGLGGD...',
  'DGLGGGD...',
  'DLGGGGD...',
  'DGGGGGD...',
  'DDDDDDD...'
)
$colors = @{ D = '#1F6B47'; G = '#3FB27F'; L = '#7FD8A8'; W = '#FFFFFF' }

function Draw-Icon([int]$size) {
  $bmp = New-Object System.Drawing.Bitmap $size, $size
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = 'None'
  $g.Clear([System.Drawing.Color]::Transparent)
  $n = $art.Count
  $cell = $size / [double]$n
  for ($y = 0; $y -lt $n; $y++) {
    for ($x = 0; $x -lt $n; $x++) {
      $k = [string]$art[$y][$x]
      if ($k -eq '.') { continue }
      $c = [System.Drawing.ColorTranslator]::FromHtml($colors[$k])
      $x0 = [int][Math]::Round($x * $cell); $y0 = [int][Math]::Round($y * $cell)
      $x1 = [int][Math]::Round(($x + 1) * $cell); $y1 = [int][Math]::Round(($y + 1) * $cell)
      $g.FillRectangle((New-Object System.Drawing.SolidBrush $c), $x0, $y0, $x1 - $x0, $y1 - $y0)
    }
  }
  $g.Dispose()
  return $bmp
}

$sizes = 16, 24, 32, 48, 64, 128, 256
$pngs = foreach ($s in $sizes) {
  $bmp = Draw-Icon $s
  $ms = New-Object System.IO.MemoryStream
  $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
  if ($s -eq 256) { $bmp.Save((Join-Path $out 'levishare.png'), [System.Drawing.Imaging.ImageFormat]::Png) }
  $bmp.Dispose()
  , $ms.ToArray()
}

# ICO container with PNG-compressed entries.
$fs = [System.IO.File]::Create((Join-Path $out 'levishare.ico'))
$w = New-Object System.IO.BinaryWriter $fs
$w.Write([UInt16]0); $w.Write([UInt16]1); $w.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
  $s = $sizes[$i]
  $w.Write([byte]($(if ($s -ge 256) { 0 } else { $s }))); $w.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))
  $w.Write([byte]0); $w.Write([byte]0); $w.Write([UInt16]1); $w.Write([UInt16]32)
  $w.Write([UInt32]$pngs[$i].Length); $w.Write([UInt32]$offset)
  $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $w.Write($p) }
$w.Dispose()
"icon written to $out"
