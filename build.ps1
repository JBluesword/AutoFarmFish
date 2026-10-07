param([switch]$Install)

$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$source = Join-Path $projectRoot 'src\AutoFarmFish.cs'
$buildDll = Join-Path $projectRoot 'build\AutoFarmFish.dll'
$releaseDirectory = Join-Path $projectRoot 'release\AutoFarmFish-0.1.0'
$releaseDll = Join-Path $releaseDirectory 'AutoFarmFish.dll'
$releaseZip = Join-Path $projectRoot 'release\AutoFarmFish-0.1.0.zip'
$gameRoot = 'C:\Program Files (x86)\Steam\steamapps\common\Farm Together 2'
$core = Join-Path $gameRoot 'BepInEx\core'
$interop = Join-Path $gameRoot 'BepInEx\interop'

$csc = Get-ChildItem 'C:\Program Files\dotnet\sdk\*\Roslyn\bincore\csc.dll' -ErrorAction SilentlyContinue |
    Sort-Object { [version]$_.Directory.Parent.Parent.Name } -Descending |
    Select-Object -First 1 -ExpandProperty FullName
if (!$csc) { throw 'A .NET SDK with Roslyn csc.dll is required.' }

$refPack = Get-ChildItem 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref' -Directory |
    Where-Object { Test-Path (Join-Path $_.FullName 'ref\net6.0') } |
    Select-Object -First 1
if (!$refPack) { throw 'The .NET 6 reference pack is required.' }

$references = @(Get-ChildItem (Join-Path $refPack.FullName 'ref\net6.0') -Filter '*.dll' |
    ForEach-Object { '/r:' + $_.FullName })
$references += @(
    ('/r:' + (Join-Path $interop 'Assembly-CSharp.dll')),
    ('/r:' + (Join-Path $interop 'Il2Cppmscorlib.dll')),
    ('/r:' + (Join-Path $interop 'Il2CppSystem.dll')),
    ('/r:' + (Join-Path $interop 'UnityEngine.CoreModule.dll')),
    ('/r:' + (Join-Path $interop 'UnityEngine.InputLegacyModule.dll')),
    ('/r:' + (Join-Path $interop 'UnityEngine.IMGUIModule.dll')),
    ('/r:' + (Join-Path $interop 'UnityEngine.TextRenderingModule.dll')),
    ('/r:' + (Join-Path $core '0Harmony.dll')),
    ('/r:' + (Join-Path $core 'BepInEx.Core.dll')),
    ('/r:' + (Join-Path $core 'BepInEx.Unity.IL2CPP.dll')),
    ('/r:' + (Join-Path $core 'Il2CppInterop.Runtime.dll'))
)

New-Item -ItemType Directory -Path (Split-Path $buildDll), $releaseDirectory -Force | Out-Null
$compilerArgs = @($csc, '/nologo', '/target:library', '/langversion:latest', '/unsafe', '/optimize+', '/nullable:disable', "/out:$buildDll")
$compilerArgs += $references
$compilerArgs += $source
& 'C:\Program Files\dotnet\dotnet.exe' @compilerArgs
if ($LASTEXITCODE -ne 0) { throw "Compilation failed with exit code $LASTEXITCODE." }

Copy-Item -LiteralPath $buildDll -Destination $releaseDll -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination $releaseDirectory -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'CHANGELOG.md') -Destination $releaseDirectory -Force
Compress-Archive -LiteralPath @(
    $releaseDll,
    (Join-Path $releaseDirectory 'README.md'),
    (Join-Path $releaseDirectory 'CHANGELOG.md')
) -DestinationPath $releaseZip -CompressionLevel Optimal -Force

if ($Install) {
    $gameProcess = Get-Process -Name 'FarmTogether2' -ErrorAction SilentlyContinue
    if ($gameProcess) { throw 'Close Farm Together 2 before installing the DLL. The build and ZIP were still created.' }
    $plugins = Join-Path $gameRoot 'BepInEx\plugins'
    if (!(Test-Path $plugins)) { throw "BepInEx plugins folder not found: $plugins" }
    Copy-Item -LiteralPath $releaseDll -Destination (Join-Path $plugins 'AutoFarmFish.dll') -Force
    Write-Host "Installed: $(Join-Path $plugins 'AutoFarmFish.dll')"
}

Write-Host "Built:   $releaseDll"
Write-Host "Package: $releaseZip"
