"""Checks that every assembly definition of Module 2 references the assemblies its code uses.

Unity compiles each .asmdef only against the assemblies listed in its "references" (plus the
engine modules). The stub and real-API compile checks in this folder compile everything in one
go, so they cannot see a missing asmdef reference; in Unity it would be a compile error such as
"The type or namespace name 'UI' does not exist in the namespace 'UnityEngine'".

Usage (from the repository root):   python Tools/CompileCheck/check-asmdef-references.py
Exit code 0 when every reference is declared.
"""
import glob
import io
import json
import os
import re
import sys

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
MODULE = os.path.join(ROOT, "Assets", "Module2_Equipment")

# namespace prefix -> assembly definition that provides it
PROVIDERS = [
    ("UnityEngine.XR.Interaction.Toolkit", "Unity.XR.Interaction.Toolkit"),
    ("UnityEngine.InputSystem", "Unity.InputSystem"),
    ("Unity.XR.CoreUtils", "Unity.XR.CoreUtils"),
    ("Unity.AI.Navigation", "Unity.AI.Navigation"),
    ("UnityEngine.UI", "UnityEngine.UI"),
    ("UnityEngine.EventSystems", "UnityEngine.UI"),
    ("TMPro", "Unity.TextMeshPro"),
    ("UnityEngine.TestTools", "UnityEngine.TestRunner"),
    ("VRScienceLab.Module2.EditorTools", "VRScienceLab.Module2.Editor"),
    ("VRScienceLab.Module2.Tests", "VRScienceLab.Module2.Tests"),
    ("VRScienceLab.Module2", "VRScienceLab.Module2.Runtime"),
]

# Using a type of the first assembly drags in base types of the second one: TMP_Text derives from
# UnityEngine.UI.MaskableGraphic, XRUIInputModule from UnityEngine.EventSystems.BaseInputModule.
IMPLIED = {
    "Unity.TextMeshPro": ["UnityEngine.UI"],
}
IMPLIED_BY_TYPE = {
    "XRUIInputModule": "UnityEngine.UI",
    "TrackedDeviceGraphicRaycaster": "UnityEngine.UI",
}

USING = re.compile(r"^\s*using\s+(?:static\s+)?([A-Za-z_][\w.]*)\s*;", re.M)
QUALIFIED = re.compile(r"\b((?:UnityEngine|Unity|TMPro|VRScienceLab)(?:\.[A-Z]\w*)+)")


def provider(namespace):
    for prefix, assembly in PROVIDERS:
        if namespace == prefix or namespace.startswith(prefix + "."):
            return assembly
    return None


def strip_comments(text):
    text = re.sub(r"/\*.*?\*/", "", text, flags=re.S)
    return re.sub(r"//[^\n]*", "", text)


def main():
    failures = 0
    for asmdef_path in sorted(glob.glob(os.path.join(MODULE, "**", "*.asmdef"), recursive=True)):
        asmdef = json.load(io.open(asmdef_path, encoding="utf-8"))
        name = asmdef["name"]
        declared = set(asmdef.get("references", [])) | {name}
        folder = os.path.dirname(asmdef_path)

        # Sources of this assembly: every .cs below the folder that is not below a nested asmdef.
        nested = [os.path.dirname(p) for p in glob.glob(os.path.join(folder, "**", "*.asmdef"), recursive=True) if os.path.dirname(p) != folder]
        needed = {}
        for source in glob.glob(os.path.join(folder, "**", "*.cs"), recursive=True):
            if any(source.startswith(n + os.sep) for n in nested):
                continue
            text = strip_comments(io.open(source, encoding="utf-8").read())
            namespaces = set(USING.findall(text)) | set(QUALIFIED.findall(text))
            for namespace in namespaces:
                assembly = provider(namespace)
                if assembly:
                    needed.setdefault(assembly, os.path.relpath(source, MODULE))
            for type_name, assembly in IMPLIED_BY_TYPE.items():
                if re.search(r"\b%s\b" % type_name, text):
                    needed.setdefault(assembly, os.path.relpath(source, MODULE))

        for assembly in list(needed):
            for implied in IMPLIED.get(assembly, []):
                needed.setdefault(implied, needed[assembly] + " (base types of " + assembly + ")")

        missing = sorted(a for a in needed if a not in declared)
        print("%s: uses %s" % (name, ", ".join(sorted(needed))))
        for assembly in missing:
            failures += 1
            print("  MISSING reference '%s' (needed by %s)" % (assembly, needed[assembly]))
        if not missing:
            print("  OK, every used assembly is referenced")

    print("asmdef reference check: %s" % ("OK" if failures == 0 else "%d missing reference(s)" % failures))
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
