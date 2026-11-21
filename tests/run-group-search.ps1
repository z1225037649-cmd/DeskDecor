$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path $PSScriptRoot -Parent
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $compiler)) { throw 'Windows .NET Framework C# compiler not found' }

$tempRoot = Join-Path $env:TEMP ('DeskDecor-GroupSearch-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tempRoot | Out-Null
$logPath = Join-Path $tempRoot 'run.log'
$transcriptStarted = $false

try {
    Start-Transcript -Path $logPath -Force | Out-Null
    $transcriptStarted = $true
    $output = Join-Path $tempRoot 'GroupSearchRegression.exe'
    $sources = @(Get-ChildItem -LiteralPath $projectRoot -Filter '*.cs' -File |
        ForEach-Object { $_.FullName })
    $sources += Join-Path $PSScriptRoot 'GroupSearchRegression.cs'

    & $compiler /nologo /target:exe /platform:x64 /main:GroupSearchRegression `
        /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Xml.dll `
        /codepage:65001 ("/out:" + $output) $sources
    if ($LASTEXITCODE -ne 0) { throw 'Group-search regression compilation failed' }

    & $output
    if ($LASTEXITCODE -ne 0) { throw 'Group-search regression failed' }
}
finally {
    if ($transcriptStarted) { Stop-Transcript | Out-Null }
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
