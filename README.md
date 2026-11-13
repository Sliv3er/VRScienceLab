# VR Science Lab – Unity project

Group university project: an interactive VR laboratory built with Unity 6.3 LTS, C# and the
XR Interaction Toolkit 3.x. Each student owns one module under `Assets/Module<N>_*`.

This repository currently contains **Module 2 – Scientific Equipment and Its Behavior**:
a complete laboratory room with four working instruments (digital scale, hot plate, stopwatch,
thermometer), a sample system (create / select / move / scale / remove), an autonomous
laboratory assistant robot on a NavMesh, a scientist avatar for the VR user and three
world-space screens (welcome + measurement summary, sample management, robot control).

* **Evaluation checklist (requirement → implementation → how to demonstrate):**
  [`Assets/Module2_Equipment/Documentation/TECHNICAL_VALIDATION.md`](Assets/Module2_Equipment/Documentation/TECHNICAL_VALIDATION.md)
* Instruments in depth: [`Assets/Module2_Equipment/Documentation/README.md`](Assets/Module2_Equipment/Documentation/README.md)
* Test checklist: [`Assets/Module2_Equipment/Documentation/TEST_CHECKLIST.md`](Assets/Module2_Equipment/Documentation/TEST_CHECKLIST.md)
* Requirement mapping (instruments): [`Assets/Module2_Equipment/Documentation/REQUIREMENTS_MAPPING.md`](Assets/Module2_Equipment/Documentation/REQUIREMENTS_MAPPING.md)

## Running the project on another PC

Only sources are committed (scripts, packages manifest, project version, documentation).
Unity regenerates `Library/`, the package cache, the `.meta` files and the generated
scene/prefabs/materials on the first open, so a fresh clone takes about 10 minutes
(mostly package download).

### 1. Install the tools

* **Unity Hub** from https://unity.com/download, then in the Hub install **Unity 6000.3.x LTS**
  (any 6000.3 patch; the project was written against 6000.3.16f1). Add the
  *Windows Build Support* module (default) and, only for a standalone Quest build,
  *Android Build Support*.
* **Git** (https://git-scm.com). No other tool is required; Visual Studio or Rider is optional
  for editing scripts.

### 2. Get the project

```bash
git clone https://github.com/Sliv3er/VRScienceLab.git
```

### 3. Open it in Unity

1. Unity Hub > **Add** > *Add project from disk* > select the cloned `VRScienceLab` folder > open it.
   If the Hub offers to change the editor version, choose your installed 6000.3.x.
2. First import takes a few minutes (packages from `Packages/manifest.json` are downloaded).
3. A dialog asks to enable the **new Input System backends** and restart: click **Yes**.
4. Menu bar: **VR Science Lab > Module 2 > Set Up Project**. It imports the TextMeshPro
   essential resources and the XR Interaction Toolkit samples (Starter Assets, simulators) and
   sets the colour space and input backend. If a dialog offers to install Shader Graph, accept it.

### 4. Build the scene and play

1. Menu bar: **VR Science Lab > Module 2 > Validate Project Setup**. The Console must say
   "Project setup looks complete".
2. Menu bar: **VR Science Lab > Module 2 > Build Lab Scene**. This generates the materials,
   meshes, textures, prefabs, the NavMesh and `Scenes/Module2_Lab.unity`, and opens the scene.
3. Press **Play**. Without a headset the XR simulator is loaded automatically: mouse looks,
   WASD moves, the on-screen panel shows the controller keys. Press **Enter** to begin.
4. Window > General > **Test Runner** > PlayMode > *Run All* to execute the automated tests.

Keyboard shortcuts without a controller: F1/F2 scale power/tare, F3/F4/F5 hot plate on/off/reset,
F6/F7/F8 stopwatch start/pause/reset, F9/F10 thermometer measure/reset, B/N/M/K create samples,
U/I/O/P move the selected sample, 6-0 send the robot to a station, Home resets the laboratory.
The full list is in the evaluation checklist.

### 5. Headset (optional)

Edit > Project Settings > XR Plug-in Management: enable **OpenXR**, add your controller
interaction profile in the OpenXR settings, run **Project Validation > Fix All**, connect the
headset (Quest Link / SteamVR) and press Play. Full details are in the Module 2 README, section 4.

### Troubleshooting

| Symptom | Fix |
|---|---|
| Console: "TextMeshPro essential resources are not imported" | Run *Set Up Project* (step 3.4) |
| Builder warns "Starter Assets sample not found" and the rig has no controllers | Run *Set Up Project* (step 3.4), then Build Lab Scene again |
| Nothing happens in Play mode with keyboard/mouse | Run *Set Up Project*, rebuild the scene; `Lab Systems/XR Simulator Loader` must reference the simulator prefab |
| The robot does not move ("not on a NavMesh") | VR Science Lab > Module 2 > Bake NavMesh, save the scene |
| Keys F1–F10 do nothing | Edit > Project Settings > Player > Active Input Handling must be *Input System Package (New)* or *Both* |
| Pink materials | The project uses the Built-in render pipeline; if you switched to URP, re-run Build Lab Scene (the builder picks URP shaders automatically) |
| A different Unity version is installed | Any 6000.0 or newer works; let the Hub upgrade the project, then re-run Build Lab Scene |

`Tools/CompileCheck` contains compile checks that run without opening the Unity editor
(see `Tools/CompileCheck/README.md`); it is not part of the Unity project.
