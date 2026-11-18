Add-Type -AssemblyName System.Drawing
$root = Split-Path -Parent $PSScriptRoot
[IO.Directory]::CreateDirectory((Join-Path $root 'assets')) | Out-Null
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$images = @()
foreach ($size in $sizes) {
    $bitmap = New-Object Drawing.Bitmap($size, $size)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.ScaleTransform($size / 256.0, $size / 256.0)
    $path = New-Object Drawing.Drawing2D.GraphicsPath
    $path.AddArc(8,8,64,64,180,90); $path.AddArc(184,8,64,64,270,90)
    $path.AddArc(184,184,64,64,0,90); $path.AddArc(8,184,64,64,90,90); $path.CloseFigure()
    $face = New-Object Drawing.SolidBrush([Drawing.Color]::FromArgb(30,30,33))
    $graphics.FillPath($face,$path)
    $graphics.FillEllipse([Drawing.Brushes]::White,28,28,200,200)
    $pen = New-Object Drawing.Pen([Drawing.Color]::FromArgb(30,30,33),6)
    for ($tick=0; $tick -lt 12; $tick++) {
        $angle = $tick * [Math]::PI / 6
        $graphics.DrawLine($pen,[single](128 + 78 * [Math]::Sin($angle)),[single](128 - 78 * [Math]::Cos($angle)),[single](128 + 88 * [Math]::Sin($angle)),[single](128 - 88 * [Math]::Cos($angle)))
    }
    $pen.Width=10; $pen.StartCap='Round'; $pen.EndCap='Round'
    $graphics.DrawLine($pen,128,128,88,102); $graphics.DrawLine($pen,128,128,160,68)
    $second = New-Object Drawing.Pen([Drawing.Color]::FromArgb(255,149,0),4)
    $graphics.DrawLine($second,128,145,128,49)
    $graphics.FillEllipse([Drawing.Brushes]::DarkOrange,121,121,14,14)
    $stream = New-Object IO.MemoryStream
    $bitmap.Save($stream,[Drawing.Imaging.ImageFormat]::Png)
    $images += ,$stream.ToArray()
    $stream.Dispose(); $pen.Dispose(); $face.Dispose(); $second.Dispose(); $path.Dispose(); $graphics.Dispose(); $bitmap.Dispose()
}
$file = [IO.File]::Create((Join-Path $root 'assets\app.ico'))
$writer = New-Object IO.BinaryWriter($file)
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i=0; $i -lt $sizes.Count; $i++) {
    $dimension = if ($sizes[$i] -eq 256) { 0 } else { $sizes[$i] }
    $writer.Write([byte]$dimension); $writer.Write([byte]$dimension); $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$images[$i].Length); $writer.Write([uint32]$offset)
    $offset += $images[$i].Length
}
foreach ($image in $images) { $writer.Write([byte[]]$image) }
$writer.Dispose(); $file.Dispose()

