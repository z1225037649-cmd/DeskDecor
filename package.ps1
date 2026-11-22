$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$distRoot = Join-Path $projectRoot 'dist'
New-Item -ItemType Directory -Path $distRoot -Force | Out-Null
if ((Get-Item -LiteralPath $distRoot).Attributes -band [IO.FileAttributes]::ReparsePoint) {
    throw 'The package output directory must not be a reparse point'
}
$stageRoot = Join-Path $distRoot ('.package-' + [Guid]::NewGuid().ToString('N'))
$sourceRoot = Join-Path $stageRoot 'DeskDecor-source'
New-Item -ItemType Directory -Path $sourceRoot -Force | Out-Null
try {
    # Start clean so removed features or local preferences cannot enter a share.
    foreach ($pattern in @('*.cs', '*.ps1', 'app.manifest', 'DeskDecor.ico', 'README.md', 'README.en.md', 'LICENSE.md', 'THIRD-PARTY-NOTICES.md')) {
        Get-ChildItem -LiteralPath $projectRoot -Filter $pattern -File | Copy-Item -Destination $sourceRoot -Force
    }
    $testsRoot = Join-Path $sourceRoot 'tests'
    $vendorRoot = Join-Path $sourceRoot 'vendor'
    $engineRoot = Join-Path $vendorRoot 'TranslucentTB-2026.2-x64'
    New-Item -ItemType Directory -Path $testsRoot, $engineRoot -Force | Out-Null
    Get-ChildItem -LiteralPath (Join-Path $projectRoot 'tests') -File |
        Where-Object { $_.Extension -in '.cs', '.ps1' } | Copy-Item -Destination $testsRoot -Force
    Copy-Item -LiteralPath (Join-Path $projectRoot 'vendor\TranslucentTB-2026.2-x64\ExplorerTAP.dll') -Destination $engineRoot -Force
    Copy-Item -LiteralPath (Join-Path $projectRoot 'vendor\TranslucentTB-2026.2-source.zip') -Destination $vendorRoot -Force
    $sourceZip = Join-Path $stageRoot 'DeskDecor-source.zip'
    Compress-Archive -Path (Join-Path $sourceRoot '*') -DestinationPath $sourceZip -Force
    $portableFiles = @('DeskDecor.exe', 'README.md', 'README.en.md', 'LICENSE.md', 'THIRD-PARTY-NOTICES.md') |
        ForEach-Object { Join-Path $projectRoot $_ }
    Compress-Archive -LiteralPath ($portableFiles + $sourceZip) -DestinationPath (Join-Path $stageRoot 'DeskDecor-portable-x64.zip') -Force
    foreach ($name in @('DeskDecor-source.zip', 'DeskDecor-portable-x64.zip')) {
        Copy-Item -LiteralPath (Join-Path $stageRoot $name) -Destination (Join-Path $distRoot $name) -Force
    }
    Write-Host ('Portable: ' + (Join-Path $distRoot 'DeskDecor-portable-x64.zip'))
    Write-Host ('Source: ' + (Join-Path $distRoot 'DeskDecor-source.zip'))
}
finally {
    $resolved = [IO.Path]::GetFullPath($stageRoot)
    if ([IO.Path]::GetDirectoryName($resolved) -ine [IO.Path]::GetFullPath($distRoot) -or
        [IO.Path]::GetFileName($resolved) -notmatch '^\.package-[0-9a-f]{32}$') { throw 'Unexpected package cleanup path' }
    if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
