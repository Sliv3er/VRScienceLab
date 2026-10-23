# Compile check (no Unity required)

`run-compile-check.ps1` compiles every C# file of Module 2 (runtime scripts, editor scene
builder and PlayMode tests) with the Roslyn compiler shipped with the Visual Studio Build
Tools, against small stub assemblies that mirror the Unity, XR Interaction Toolkit,
TextMeshPro, Input System, UnityEditor and NUnit APIs the module uses. It then executes the
real test methods from `Module2InstrumentTests` in a console process.

```bash
powershell -ExecutionPolicy Bypass -File Tools/CompileCheck/run-compile-check.ps1
```

What it proves: the scripts are valid C# 9, every API call matches the signatures listed in
the official package documentation, and the instrument logic behaves as the tests expect.

What it does not prove: anything Unity-specific (physics triggers, rendering, serialization,
the real XRI components, the generated scene). Two tests are skipped for that reason and must
be run in Unity's Test Runner. The stubs are deliberately minimal and must never be copied
into the `Assets` folder.
