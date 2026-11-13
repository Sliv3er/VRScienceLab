#!/bin/bash
# Real-API compile check: Unity's Roslyn + real UnityEngine/UnityEditor DLLs + real UGUI/TMP sources.
# Usage (Git Bash, from the repository root):
#   UNITY_EDITOR_DATA="C:/Program Files/Unity/Hub/Editor/6000.3.16f1/Editor/Data" bash Tools/CompileCheck/RealApi/run-real-api-check.sh [all]
# "all" also rebuilds the UGUI / TextMeshPro assemblies from the sources bundled with the editor.
HERE="$(cd "$(dirname "$0")" && (pwd -W 2>/dev/null || pwd))"
E="${UNITY_EDITOR_DATA:?Set UNITY_EDITOR_DATA to <Unity install>/Editor/Data}"
P="$(cd "$HERE/../../.." && (pwd -W 2>/dev/null || pwd))"
OUT="$P/Tools/CompileCheck/out/realapi"; mkdir -p "$OUT"
CSC="$E/NetCoreRuntime/dotnet.exe $E/DotNetSdkRoslyn/csc.dll"
DEF="UNITY_EDITOR;UNITY_EDITOR_WIN;UNITY_EDITOR_64;UNITY_STANDALONE;UNITY_STANDALONE_WIN;UNITY_64;ENABLE_INPUT_SYSTEM;UNITY_INCLUDE_TESTS;UNITY_6000;UNITY_6000_3;UNITY_6000_3_16;PACKAGE_PHYSICS;PACKAGE_PHYSICS2D;PACKAGE_ANIMATION;PACKAGE_UITOOLKIT;PACKAGE_TILEMAP"
for v in 2017_1 2017_2 2017_3 2017_4 2018_1 2018_2 2018_3 2018_4 2019_1 2019_2 2019_3 2019_4 2020_1 2020_2 2020_3 2021_1 2021_2 2021_3 2022_1 2022_2 2022_3 2023_1 2023_2 2023_3 6000_0 6000_1 6000_2 6000_3; do DEF="$DEF;UNITY_${v}_OR_NEWER"; done
refs() { for f in "$E"/Managed/UnityEngine/*.dll "$E"/NetStandard/ref/2.1.0/netstandard.dll "$E"/NetStandard/compat/2.1.0/shims/netfx/*.dll "$E"/NetStandard/compat/2.1.0/shims/netstandard/*.dll; do echo "-r:\"$f\""; done; }
rsp() { # name target-kind extra-refs... ; sources on stdin
  local name=$1; shift
  { echo "-nologo -noconfig -nostdlib+ -langversion:9.0 -nowarn:0169,0649,0414,0618,0612,0108,0114,0067,0219,0168,1701,1702,0436 -target:library -unsafe+ -out:\"$OUT/$name.dll\" -define:$DEF"; refs; for r in "$@"; do echo "-r:\"$r\""; done; while read -r s; do echo "\"$s\""; done; } > "$OUT/$name.rsp"
  $CSC "@$OUT/$name.rsp" 2>&1 | grep -E "error|warning CS8" | head -${MAXERR:-40}
  [ -f "$OUT/$name.dll" ] && echo "OK $name" || echo "FAILED $name"
}
U="$E/Resources/PackageManager/BuiltInPackages/com.unity.ugui/Runtime"
if [ "$1" = "all" ] || [ ! -f "$OUT/Unity.TextMeshPro.dll" ]; then
  rm -f "$OUT"/UnityEngine.UI.dll "$OUT"/Unity.TextMeshPro.dll
  find "$U/UGUI" -name "*.cs" | rsp UnityEngine.UI
  find "$U/InternalBridge" -name "*.cs" | rsp Unity.InternalAPIEngineBridge.004
  find "$U/TMP" -name "*.cs" | rsp Unity.TextMeshPro "$OUT/UnityEngine.UI.dll" "$OUT/Unity.InternalAPIEngineBridge.004.dll"
fi
rm -f "$OUT"/Stubs.dll "$OUT"/Runtime.dll "$OUT"/Editor.dll "$OUT"/Tests.dll
find "$HERE/Stubs" -name "*.cs" | rsp Stubs "$OUT/UnityEngine.UI.dll"
PK="$OUT/UnityEngine.UI.dll $OUT/Unity.TextMeshPro.dll $OUT/Stubs.dll"
find "$P/Assets/Module2_Equipment/Scripts" -name "*.cs" | rsp Runtime $PK
ED=$(ls "$E"/Managed/UnityEditor*.dll)
find "$P/Assets/Module2_Equipment/Editor" -name "*.cs" | rsp Editor $PK "$OUT/Runtime.dll"
NU=$(find "$E/Resources/PackageManager/BuiltInPackages/com.unity.ext.nunit" -name "nunit.framework.dll" | head -1)
{ find "$P/Assets/Module2_Equipment/Tests" -name "*.cs"; cp "$HERE/Stubs/TestFrameworkStubs.cs.txt" "$OUT/TestFrameworkStubs.cs"; echo "$OUT/TestFrameworkStubs.cs"; } | rsp Tests $PK "$OUT/Runtime.dll" "$NU"
