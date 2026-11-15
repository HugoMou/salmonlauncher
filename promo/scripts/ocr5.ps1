# ocr5.ps1 — Windows PowerShell 5.1 + Windows.Media.Ocr
# 用法: ocr5.ps1 -Path a.png[,b.png] [-Rects] [-OffsetX n] [-OffsetY n]
param(
    [Parameter(Mandatory=$true)][string[]]$Path,
    [switch]$Rects,
    [int]$OffsetX = 0,
    [int]$OffsetY = 0
)
$ErrorActionPreference = 'Continue'

Add-Type -AssemblyName System.Runtime.WindowsRuntime
$asTaskGeneric = ([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {
    $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and
    $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1'
})[0]

function Await($op, [Type]$resultType) {
    $t = $asTaskGeneric.MakeGenericMethod($resultType).Invoke($null, @($op))
    if (-not $t.Wait(30000)) { throw 'timeout' }
    $t.Result
}

[Windows.Storage.StorageFile,Windows.Storage,ContentType=WindowsRuntime] | Out-Null
[Windows.Graphics.Imaging.BitmapDecoder,Windows.Graphics.Imaging,ContentType=WindowsRuntime] | Out-Null
[Windows.Media.Ocr.OcrEngine,Windows.Media,ContentType=WindowsRuntime] | Out-Null

$engine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromUserProfileLanguages()
if (-not $engine) {
    $langs = [Windows.Media.Ocr.OcrEngine]::AvailableRecognizerLanguages
    if ($langs.Count -gt 0) { $engine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromLanguage($langs[0]) }
}
if (-not $engine) { 'OCR engine unavailable'; exit 1 }

foreach ($p in $Path) {
    if (-not (Test-Path $p)) { "MISSING: $p"; continue }
    "===== $([System.IO.Path]::GetFileName($p)) ====="
    try {
        $file = Await ([Windows.Storage.StorageFile]::GetFileFromPathAsync((Resolve-Path $p).Path)) ([Windows.Storage.StorageFile])
        $stream = Await ($file.OpenAsync([Windows.Storage.FileAccessMode]::Read)) ([Windows.Storage.Streams.IRandomAccessStream])
        $decoder = Await ([Windows.Graphics.Imaging.BitmapDecoder]::CreateAsync($stream)) ([Windows.Graphics.Imaging.BitmapDecoder])
        $bitmap = Await ($decoder.GetSoftwareBitmapAsync()) ([Windows.Graphics.Imaging.SoftwareBitmap])
        $result = Await ($engine.RecognizeAsync($bitmap)) ([Windows.Media.Ocr.OcrResult])
        if ($Rects) {
            foreach ($line in $result.Lines) {
                $words = @($line.Words)
                if ($words.Count -eq 0) { continue }
                $x1 = ($words | ForEach-Object { $_.BoundingRect.X } | Measure-Object -Minimum).Minimum
                $y1 = ($words | ForEach-Object { $_.BoundingRect.Y } | Measure-Object -Minimum).Minimum
                $x2 = ($words | ForEach-Object { $_.BoundingRect.X + $_.BoundingRect.Width } | Measure-Object -Maximum).Maximum
                $y2 = ($words | ForEach-Object { $_.BoundingRect.Y + $_.BoundingRect.Height } | Measure-Object -Maximum).Maximum
                $cx = [int](($x1 + $x2) / 2 + $OffsetX)
                $cy = [int](($y1 + $y2) / 2 + $OffsetY)
                'LINE|{0}|{1}|{2}|{3}|{4}' -f $cx, $cy, $line.Text, [int]($x2-$x1), [int]($y2-$y1)
            }
        } else {
            $lines = @($result.Lines | ForEach-Object { $_.Text })
            if ($lines.Count -eq 0) { '(no text detected)' } else { $lines -join ' / ' }
        }
        $stream.Dispose()
    } catch {
        "OCR failed: $($_.Exception.Message)"
    }
}