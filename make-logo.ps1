$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
function RoundedPath([float]$x,[float]$y,[float]$w,[float]$h,[float]$r) {
    $path=New-Object Drawing.Drawing2D.GraphicsPath
    $d=$r*2
    $path.AddArc($x,$y,$d,$d,180,90)
    $path.AddArc(($x+$w-$d),$y,$d,$d,270,90)
    $path.AddArc(($x+$w-$d),($y+$h-$d),$d,$d,0,90)
    $path.AddArc($x,($y+$h-$d),$d,$d,90,90)
    $path.CloseFigure()
    return ,$path
}
$images=@()
foreach($size in @(16,24,32,48,64,128,256)) {
    $bitmap=New-Object Drawing.Bitmap($size,$size)
    $g=[Drawing.Graphics]::FromImage($bitmap)
    $g.SmoothingMode='AntiAlias'
    $g.ScaleTransform(($size/256.0),($size/256.0))
    $bg=RoundedPath 4 4 248 248 52
    $brush=New-Object Drawing.Drawing2D.LinearGradientBrush([Drawing.Point]::new(20,0),[Drawing.Point]::new(240,256),[Drawing.Color]::FromArgb(242,88,104),[Drawing.Color]::FromArgb(165,29,53))
    $g.FillPath($brush,$bg)
    $pen=New-Object Drawing.Pen([Drawing.Color]::White,12)
    $pen.StartCap='Round';$pen.EndCap='Round';$pen.LineJoin='Round'
    # Four corners of a screenshot selection.
    foreach($points in @(@(85,51,51,51,51,85),@(171,51,205,51,205,85),@(205,171,205,205,171,205),@(85,205,51,205,51,171))) {
        $g.DrawLine($pen,$points[0],$points[1],$points[2],$points[3])
        $g.DrawLine($pen,$points[2],$points[3],$points[4],$points[5])
    }
    # Two overlapping sheets: the familiar copy symbol inside the capture frame.
    $rear=RoundedPath 88 83 63 76 9
    $g.DrawPath($pen,$rear)
    $front=RoundedPath 111 108 63 76 9
    $fill=New-Object Drawing.SolidBrush([Drawing.Color]::FromArgb(201,46,67))
    $g.FillPath($fill,$front);$g.DrawPath($pen,$front)
    $smallPen=New-Object Drawing.Pen([Drawing.Color]::White,6)
    $smallPen.StartCap='Round';$smallPen.EndCap='Round'
    $g.DrawLine($smallPen,129,133,155,133);$g.DrawLine($smallPen,129,151,150,151)
    $stream=New-Object IO.MemoryStream
    $bitmap.Save($stream,[Drawing.Imaging.ImageFormat]::Png)
    $images+=,[pscustomobject]@{Size=$size;Bytes=$stream.ToArray()}
    if($size -eq 256) { $bitmap.Save((Join-Path $PSScriptRoot 'TextSnip-logo.png'),[Drawing.Imaging.ImageFormat]::Png) }
    $stream.Dispose();$smallPen.Dispose();$fill.Dispose();$front.Dispose();$rear.Dispose();$pen.Dispose();$brush.Dispose();$bg.Dispose();$g.Dispose();$bitmap.Dispose()
}
$file=[IO.File]::Create((Join-Path $PSScriptRoot 'TextSnip.ico'))
$writer=New-Object IO.BinaryWriter($file)
$writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]$images.Count)
$offset=6+16*$images.Count
foreach($entry in $images) {
    $dimension=if($entry.Size -eq 256){0}else{$entry.Size}
    $writer.Write([byte]$dimension);$writer.Write([byte]$dimension);$writer.Write([byte]0);$writer.Write([byte]0)
    $writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$entry.Bytes.Length);$writer.Write([uint32]$offset)
    $offset+=$entry.Bytes.Length
}
foreach($entry in $images) { $writer.Write([byte[]]$entry.Bytes) }
$writer.Dispose()
