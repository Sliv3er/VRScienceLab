# Compile checks (no Unity editor window required)

Two checks live here. Neither is part of the Unity project and nothing in this folder must
ever be copied into `Assets`.

## 1. Real-API check (everything)

`RealApi/run-real-api-check.sh` compiles **all** Module 2 code (runtime scripts, the editor
scene builder and the PlayMode tests) with the Roslyn compiler shipped inside the Unity
editor, against the real `UnityEngine` / `UnityEditor` assemblies and the real UGUI +
TextMeshPro sources bundled with the editor. Only the packages that Unity downloads at first
open (XR Interaction Toolkit, Input System, XR Core Utils, AI Navigation, Test Framework) are
replaced by small stubs in `RealApi/Stubs`.

```bash
UNITY_EDITOR_DATA="C:/Program Files/Unity/Hub/Editor/6000.3.16f1/Editor/Data" bash Tools/CompileCheck/RealApi/run-real-api-check.sh all
```

Expected output: `OK` for UnityEngine.UI, Unity.TextMeshPro, Stubs, Runtime, Editor and Tests.
It needs an installed editor but no licence and no project import.

What it proves: every call into the Unity engine, the Unity editor, UGUI and TextMeshPro
matches the real 6000.3 API. What it does not prove: runtime behaviour, and the exact API of
the stubbed packages.

## 2. Stub check with logic tests (instruments only)

`run-compile-check.ps1` compiles the four instrument scripts and `Module2InstrumentTests`
against hand-written stub assemblies with the Visual Studio Build Tools compiler, then runs
the instrument tests in a console process. It needs no Unity installation at all.

```bash
powershell -ExecutionPolicy Bypass -File Tools/CompileCheck/run-compile-check.ps1
```

## 3. Assembly definition references

`check-asmdef-references.py` checks that every `.asmdef` of the module references the assemblies
its code uses (for example `UnityEngine.UI` when a script uses a uGUI or TextMeshPro type). The
two checks above compile everything in one go and cannot see a missing reference; in Unity it is
a compile error.

```bash
python Tools/CompileCheck/check-asmdef-references.py
```

## 4. Headless simulation

`Tools/HeadlessSim` goes further than compiling: it executes the scene builder, plays the scene
and runs the PlayMode tests against a functional stand-in for the engine. See its README.

None of these checks replaces opening the project in Unity and running the PlayMode tests
(Window > General > Test Runner).
