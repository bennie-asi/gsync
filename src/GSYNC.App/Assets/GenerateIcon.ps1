# Regenerate GSYNC.ico from the same vector geometry as GSYNC.svg on Windows.
Add-Type -AssemblyName System.Drawing
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$images = @()
foreach ($size in $sizes) {
    $bitmap = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.ScaleTransform($size / 256.0, $size / 256.0)
    $tile = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $tile.AddArc(8, 8, 112, 112, 180, 90)
    $tile.AddArc(136, 8, 112, 112, 270, 90)
    $tile.AddArc(136, 136, 112, 112, 0, 90)
    $tile.AddArc(8, 136, 112, 112, 90, 90)
    $tile.CloseFigure()
    $fill = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#142128'))
    $edge = [System.Drawing.Pen]::new([System.Drawing.ColorTranslator]::FromHtml('#23514f'), 4)
    $graphics.FillPath($fill, $tile)
    $graphics.DrawPath($edge, $tile)
    $stroke = [System.Drawing.Pen]::new([System.Drawing.ColorTranslator]::FromHtml('#00c9a7'), 25)
    $stroke.StartCap = $stroke.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $stroke.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $graphics.DrawArc($stroke, 46, 46, 164, 164, 40, 280)
    $graphics.DrawLines($stroke, [System.Drawing.PointF[]]@([System.Drawing.PointF]::new(134,128), [System.Drawing.PointF]::new(202,128), [System.Drawing.PointF]::new(202,168)))
    $stroke.Width = 13
    $graphics.DrawLines($stroke, [System.Drawing.PointF[]]@([System.Drawing.PointF]::new(177,62), [System.Drawing.PointF]::new(198,78), [System.Drawing.PointF]::new(201,51)))
    $stream = [System.IO.MemoryStream]::new()
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $images += ,$stream.ToArray()
    $stream.Dispose(); $stroke.Dispose(); $edge.Dispose(); $fill.Dispose(); $tile.Dispose(); $graphics.Dispose(); $bitmap.Dispose()
}
$path = Join-Path $PSScriptRoot 'GSYNC.ico'
$writer = [System.IO.BinaryWriter]::new([System.IO.File]::Create($path))
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dimension = if ($sizes[$i] -eq 256) { 0 } else { $sizes[$i] }
    $writer.Write([byte]$dimension); $writer.Write([byte]$dimension); $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$images[$i].Length); $writer.Write([uint32]$offset)
    $offset += $images[$i].Length
}
foreach ($image in $images) { $writer.Write([byte[]]$image) }
$writer.Dispose()
