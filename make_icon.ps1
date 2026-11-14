param([Parameter(Mandatory = $true)][string]$OutputPath)

Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition 'using System; using System.Runtime.InteropServices; public static class DeskDecorIconNative { [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr hIcon); }'

$bitmap = New-Object System.Drawing.Bitmap(64, 64, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.Color]::Transparent)

$backgroundPath = New-Object System.Drawing.Drawing2D.GraphicsPath
$backgroundPath.AddArc(2, 2, 24, 24, 180, 90)
$backgroundPath.AddArc(38, 2, 24, 24, 270, 90)
$backgroundPath.AddArc(38, 38, 24, 24, 0, 90)
$backgroundPath.AddArc(2, 38, 24, 24, 90, 90)
$backgroundPath.CloseFigure()
$backgroundBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 31, 43, 51))
$graphics.FillPath($backgroundBrush, $backgroundPath)

$tileBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 75, 112, 122))
$graphics.FillRectangle($tileBrush, 13, 12, 15, 14)
$graphics.FillRectangle($tileBrush, 35, 12, 15, 14)
$graphics.FillRectangle($tileBrush, 13, 31, 15, 14)
$graphics.FillRectangle($tileBrush, 35, 31, 15, 14)

$folderPath = New-Object System.Drawing.Drawing2D.GraphicsPath
$points = [System.Drawing.PointF[]]@(
    (New-Object System.Drawing.PointF(11, 39)),
    (New-Object System.Drawing.PointF(27, 39)),
    (New-Object System.Drawing.PointF(31, 43)),
    (New-Object System.Drawing.PointF(52, 43)),
    (New-Object System.Drawing.PointF(52, 54)),
    (New-Object System.Drawing.PointF(11, 54))
)
$folderPath.AddPolygon($points)
$folderBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 237, 179, 84))
$folderTabBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 255, 211, 123))
$graphics.FillPath($folderBrush, $folderPath)
$graphics.FillRectangle($folderTabBrush, 12, 36, 18, 5)

$handle = $bitmap.GetHicon()
$icon = [System.Drawing.Icon]::FromHandle($handle)
$stream = [System.IO.File]::Create($OutputPath)
try { $icon.Save($stream) }
finally {
    $stream.Dispose()
    $icon.Dispose()
    [void][DeskDecorIconNative]::DestroyIcon($handle)
    $graphics.Dispose()
    $bitmap.Dispose()
    $backgroundPath.Dispose()
    $backgroundBrush.Dispose()
    $tileBrush.Dispose()
    $folderPath.Dispose()
    $folderBrush.Dispose()
    $folderTabBrush.Dispose()
}
