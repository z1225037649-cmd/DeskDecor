$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$distRoot = Join-Path $projectRoot 'dist'
$sourceRoot = Join-Path $distRoot 'DeskDecor-source'
New-Item -ItemType Directory -Path $sourceRoot -Force | Out-Null
foreach ($pattern in @('*.cs', '*.ps1', 'app.manifest', 'DeskDecor.ico', 'README.md', 'README.en.md', 'LICENSE.md', 'THIRD-PARTY-NOTICES.md')) {
    Get-ChildItem -LiteralPath $projectRoot -Filter $pattern -File | Copy-Item -Destination $sourceRoot -Force
}
$testsRoot = Join-Path $sourceRoot 'tests'
$vendorRoot = Join-Path $sourceRoot 'vendor'
$engineRoot = Join-Path $vendorRoot 'TranslucentTB-2026.2-x64'
New-Item -ItemType Directory -Path $testsRoot, $engineRoot -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $projectRoot 'tests') -File | Where-Object { $_.Extension -in '.cs', '.ps1' } | Copy-Item -Destination $testsRoot -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'vendor\TranslucentTB-2026.2-x64\ExplorerTAP.dll') -Destination $engineRoot -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'vendor\TranslucentTB-2026.2-source.zip') -Destination $vendorRoot -Force
$sourceZip = Join-Path $distRoot 'DeskDecor-source.zip'
Compress-Archive -Path (Join-Path $sourceRoot '*') -DestinationPath $sourceZip -Force
$portableFiles = @('DeskDecor.exe', 'README.md', 'README.en.md', 'LICENSE.md', 'THIRD-PARTY-NOTICES.md') | ForEach-Object { Join-Path $projectRoot $_ }
Compress-Archive -LiteralPath ($portableFiles + $sourceZip) -DestinationPath (Join-Path $distRoot 'DeskDecor-portable-x64.zip') -Force
Write-Host ('Portable: ' + (Join-Path $distRoot 'DeskDecor-portable-x64.zip'))
Write-Host ('Source: ' + $sourceZip)
