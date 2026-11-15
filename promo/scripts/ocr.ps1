# ocr.ps1 — 用 Windows.Media.Ocr 识别图片中的文字，用于自动校验录屏画面
param([Parameter(Mandatory=$true)][string[]]$Path)
$ErrorActionPreference = 'Continue'

Add-Type -AssemblyName System.Runtime.WindowsRuntime
$asTask = ([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {
    $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and
    $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1'
})[0]

function Await($op, [Type]$resultType) {
    $t = $asTask.MakeGenericMethod($resultType).Invoke($null, @($op))
    $t.Wait(20000) | Out-Null
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
"engine language: $($engine.RecognizerLanguage.LanguageTag)"

foreach ($p in $Path) {
    if (-not (Test-Path $p)) { "MISSING: $p"; continue }
    "===== $([System.IO.Path]::GetFileName($p)) ====="
    try {
        $file = Await ([Windows.Storage.StorageFile]::GetFileFromPathAsync((Resolve-Path $p).Path)) ([Windows.Storage.StorageFile])
        $stream = Await ($file.OpenAsync([Windows.Storage.FileAccessMode]::Read)) ([Windows.Storage.Streams.IRandomAccessStream])
        $decoder = Await ([Windows.Graphics.Imaging.BitmapDecoder]::CreateAsync($stream)) ([Windows.Graphics.Imaging.BitmapDecoder])
        $bitmap = Await ($decoder.GetSoftwareBitmapAsync()) ([Windows.Graphics.Imaging.SoftwareBitmap])
        $result = Await ($engine.RecognizeAsync($bitmap)) ([Windows.Media.Ocr.OcrResult])
        $lines = $result.Lines | ForEach-Object { $_.Text }
        if ($lines.Count -eq 0) { '(no text detected)' } else { $lines -join ' / ' }
        $stream.Dispose()
    } catch {
        "OCR failed: $($_.Exception.Message)"
    }
}