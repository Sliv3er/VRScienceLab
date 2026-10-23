# Compiles every Module 2 C# file against stub Unity/XRI/TMP/Input System/NUnit
# assemblies with the Roslyn compiler and then runs the instrument tests in a
# plain console process. This catches syntax errors, type errors and logic
# regressions without opening Unity. It does NOT replace opening the project in
# the Unity editor: the stubs only mirror the API surface the module uses.
#
# Usage (from the repository root):   powershell -File Tools/CompileCheck/run-compile-check.ps1

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$check = Join-Path $root "Tools/CompileCheck"
$out = Join-Path $check "out"
New-Item -ItemType Directory -Force $out | Out-Null

$csc = "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe"
if (-not (Test-Path $csc)) {
    $found = Get-ChildItem "C:\Program Files*\Microsoft Visual Studio" -Recurse -Filter csc.exe -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -eq $found) { throw "Roslyn csc.exe not found. Install the Visual Studio Build Tools (C# compiler)." }
    $csc = $found.FullName
}

$fw = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
$refs = @("-r:$fw\mscorlib.dll", "-r:$fw\System.dll", "-r:$fw\System.Core.dll")

$sources = @(
    (Join-Path $check "Stubs\*.cs"),
    (Join-Path $check "Harness\Program.cs"),
    (Join-Path $root "Assets\Module2_Equipment\Scripts\*.cs"),
    (Join-Path $root "Assets\Module2_Equipment\Editor\*.cs"),
    (Join-Path $root "Assets\Module2_Equipment\Tests\Runtime\*.cs")
)

function Invoke-Csc([string]$defines, [string]$exe) {
    Write-Host "== Compiling ($defines) -> $exe"
    & $csc -nologo -noconfig -nostdlib+ -langversion:9.0 -warn:4 -target:exe "-out:$exe" "-define:$defines" @refs @sources
    if ($LASTEXITCODE -ne 0) { throw "Compilation failed ($defines)." }
}

# 1) The configuration Unity uses in the editor with the Input System backend active.
Invoke-Csc "ENABLE_INPUT_SYSTEM;UNITY_INCLUDE_TESTS;UNITY_EDITOR" (Join-Path $out "Module2Check.exe")
# 2) Without the Input System define, to make sure the fallback branch also compiles.
Invoke-Csc "UNITY_INCLUDE_TESTS;UNITY_EDITOR" (Join-Path $out "Module2Check_NoInputSystem.exe")

Write-Host "== Running instrument tests against the stub runtime"
& (Join-Path $out "Module2Check.exe")
if ($LASTEXITCODE -ne 0) { throw "Tests failed." }
Write-Host "== Compile check finished successfully"
