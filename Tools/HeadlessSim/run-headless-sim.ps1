# HeadlessSim: compiles the simulated engine (Engine/), the harness (Harness/) and every Module 2
# script, editor script and test into one console program with the Roslyn compiler, then runs it.
# No Unity installation is needed. Exit code 0 = everything passed, 1 = a run failure
# (exception, logged error, failed check or failed test), 2 = compile error.
#
# Usage (from the repository root):
#   powershell -ExecutionPolicy Bypass -File Tools/HeadlessSim/run-headless-sim.ps1
#   powershell -ExecutionPolicy Bypass -File Tools/HeadlessSim/run-headless-sim.ps1 -AssetsRoot C:\path\to\Assets
# Options:
#   -SkipTests        do not run the PlayMode tests
#   -SkipBuild        do not build / play the lab scene (tests that load it are then skipped)
#   -Tests <text>     only fixtures whose full name contains <text>
#   -StarterAssets    pretend the XRI "Starter Assets" rig and the XR simulator prefab are imported
#   -NavMeshMap       print an ASCII map of the baked NavMesh; -Mark <object name> letters its children on the map
#   -NoSelfTest       skip the simulator's own self-tests
#   -VerboseLog       also print plain Debug.Log messages
#   -NoRun            compile only
param(
    [string]$AssetsRoot = "",
    [switch]$SkipTests,
    [switch]$SkipBuild,
    [string]$Tests = "",
    [switch]$StarterAssets,
    [switch]$NavMeshMap,
    [string]$Mark = "",
    [switch]$NoSelfTest,
    [switch]$VerboseLog,
    [switch]$NoRun
)

$ErrorActionPreference = "Stop"
$sim = $PSScriptRoot
$repo = Split-Path -Parent (Split-Path -Parent $sim)
if ([string]::IsNullOrWhiteSpace($AssetsRoot)) { $AssetsRoot = Join-Path $repo "Assets" }
$AssetsRoot = (Resolve-Path $AssetsRoot).Path
$module = Join-Path $AssetsRoot "Module2_Equipment"
if (-not (Test-Path $module)) { Write-Host "Module folder not found: $module"; exit 2 }

$out = Join-Path $sim "out"
New-Item -ItemType Directory -Force $out | Out-Null

$csc = "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe"
if (-not (Test-Path $csc)) {
    $found = Get-ChildItem "C:\Program Files*\Microsoft Visual Studio" -Recurse -Filter csc.exe -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match "Roslyn" } | Select-Object -First 1
    if ($null -eq $found) { Write-Host "Roslyn csc.exe not found. Install the Visual Studio Build Tools (C# compiler)."; exit 2 }
    $csc = $found.FullName
}

$fw = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
$sources = @()
$sources += Get-ChildItem (Join-Path $sim "Engine") -Recurse -Filter *.cs | ForEach-Object { $_.FullName }
$sources += Get-ChildItem (Join-Path $sim "Harness") -Recurse -Filter *.cs | ForEach-Object { $_.FullName }
foreach ($folder in @("Scripts", "Editor", "Tests")) {
    $path = Join-Path $module $folder
    if (Test-Path $path) { $sources += Get-ChildItem $path -Recurse -Filter *.cs | ForEach-Object { $_.FullName } }
}

$exe = Join-Path $out "HeadlessSim.exe"
$rsp = Join-Path $out "HeadlessSim.rsp"
$lines = @(
    "-nologo", "-nostdlib+", "-langversion:9.0", "-target:exe", "-platform:x64", "-debug:pdbonly", "-optimize+",
    "-define:UNITY_EDITOR;ENABLE_INPUT_SYSTEM;UNITY_INCLUDE_TESTS;UNITY_6000_0_OR_NEWER",
    # Unused-field style warnings are expected for [SerializeField] fields (Unity assigns them); everything else is shown.
    "-nowarn:0649,0169,0414,0067,0108,0114,0618,0612,0219,0168,0162,0109,0660,0661,0659,1591",
    "-r:`"$fw\mscorlib.dll`"", "-r:`"$fw\System.dll`"", "-r:`"$fw\System.Core.dll`"",
    "-out:`"$exe`""
)
foreach ($source in $sources) { $lines += "`"$source`"" }
Set-Content -Path $rsp -Value $lines -Encoding UTF8

Write-Host "== HeadlessSim: compiling $($sources.Count) files (project code from $module)"
if (Test-Path $exe) { Remove-Item $exe -Force }
$compileOutput = & $csc -noconfig "@$rsp" 2>&1
$compileErrors = @($compileOutput | Where-Object { $_ -match "error " })
$compileWarnings = @($compileOutput | Where-Object { $_ -match "warning " })
if ($compileErrors.Count -gt 0 -or -not (Test-Path $exe)) {
    Write-Host "== COMPILE FAILED: $($compileErrors.Count) error(s)"
    $compileErrors | Select-Object -First 60 | ForEach-Object { Write-Host $_ }
    exit 2
}
Write-Host "== compiled with 0 errors, $($compileWarnings.Count) warning(s)"
$compileWarnings | Select-Object -First 20 | ForEach-Object { Write-Host "   $_" }
if ($NoRun) { exit 0 }

$arguments = @()
if ($SkipTests) { $arguments += "--skip-tests" }
if ($SkipBuild) { $arguments += "--skip-build" }
if ($StarterAssets) { $arguments += "--starter-assets" }
if ($VerboseLog) { $arguments += "--verbose" }
if ($NoSelfTest) { $arguments += "--no-self-test" }
if ($NavMeshMap) { $arguments += "--navmesh-map" }
if (-not [string]::IsNullOrWhiteSpace($Mark)) { $arguments += @("--mark", $Mark) }
if (-not [string]::IsNullOrWhiteSpace($Tests)) { $arguments += @("--tests", $Tests) }

& $exe @arguments
exit $LASTEXITCODE
