# Rebuild the app PNG and multi-resolution Windows ICO from the custom leaf/share artwork.
# Keep the source artwork: regenerating must never replace it with the old pixel crate.
#   powershell -ExecutionPolicy Bypass -File tools/make-icon.ps1
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$out = Join-Path $PSScriptRoot '..\src\LeviShare.App\Assets'
$sourcePath = Join-Path $out 'levishare-icon-source.png'
if (!(Test-Path -LiteralPath $sourcePath)) { throw "Icon source not found: $sourcePath" }
$source = [System.Drawing.Image]::FromFile($sourcePath)
$sizes = 16, 24, 32, 48, 64, 128, 256
try {
  $pngs = foreach ($size in $sizes) {
    $bitmap = New-Object System.Drawing.Bitmap $size, $size
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $stream = New-Object System.IO.MemoryStream
    try {
      $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
      $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
      $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
      $graphics.Clear([System.Drawing.Color]::Transparent)
      $graphics.DrawImage($source, 0, 0, $size, $size)
      $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
      if ($size -eq 256) { $bitmap.Save((Join-Path $out 'levishare.png'), [System.Drawing.Imaging.ImageFormat]::Png) }
      , $stream.ToArray()
    } finally { $stream.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }
  }
  # ICO container with a PNG entry at each Windows shell size.
  $file = [System.IO.File]::Create((Join-Path $out 'levishare.ico'))
  $writer = New-Object System.IO.BinaryWriter $file
  try {
    $writer.Write([UInt16]0); $writer.Write([UInt16]1); $writer.Write([UInt16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($index = 0; $index -lt $sizes.Count; $index++) {
      $dimension = [byte]$(if ($sizes[$index] -eq 256) { 0 } else { $sizes[$index] })
      $writer.Write($dimension); $writer.Write($dimension)
      $writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([UInt16]1); $writer.Write([UInt16]32)
      $writer.Write([UInt32]$pngs[$index].Length); $writer.Write([UInt32]$offset)
      $offset += $pngs[$index].Length
    }
    foreach ($png in $pngs) { $writer.Write($png) }
  } finally { $writer.Dispose() }
} finally { $source.Dispose() }
"Icon written to $out"
