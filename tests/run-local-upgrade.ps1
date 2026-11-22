$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('DeskDecor-local-fixtures-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot | Out-Null
try {
    $fixtures = @()
    foreach ($version in @('0.0.0.0','1.1.0.0','2.0.0.0')) {
        $directory = Join-Path $testRoot $version
        New-Item -ItemType Directory -Path $directory | Out-Null
        $source = Join-Path $directory 'Fixture.cs'
        [IO.File]::WriteAllText($source, ('using System.Reflection; [assembly: AssemblyVersion("' + $version + '")] internal static class Fixture { private static void Main() {} }'))
        $fixture = Join-Path $directory 'DeskDecor.exe'
        & $compiler /nologo /target:winexe /platform:x64 ('/out:' + $fixture) $source
        if ($LASTEXITCODE -ne 0) { throw 'Local upgrade fixture compilation failed' }
        $fixtures += $fixture
    }
    $output = Join-Path $testRoot 'LocalUpgradeRegression.exe'
    & $compiler /nologo /target:exe /r:System.dll /r:System.Xml.dll /codepage:65001 ('/out:' + $output) `
        (Join-Path $projectRoot 'DesktopItem.cs') (Join-Path $projectRoot 'LocalUpgrade.cs') (Join-Path $PSScriptRoot 'LocalUpgradeRegression.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Local upgrade regression compilation failed' }
    & $output @fixtures
    if ($LASTEXITCODE -ne 0) { throw 'Local upgrade regression failed' }
}
finally {
    $resolved = [IO.Path]::GetFullPath($testRoot)
    if ([IO.Path]::GetDirectoryName($resolved).TrimEnd('\') -ine [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') -or
        [IO.Path]::GetFileName($resolved) -notmatch '^DeskDecor-local-fixtures-[0-9a-f]{32}$') { throw 'Unexpected fixture cleanup path' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
