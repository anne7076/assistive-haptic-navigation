# Assistive Haptic Navigation (`assistive-haptic-navigation`)

A Unity 3D simulation and assistive-wearable prototyping platform designed to help visually impaired users navigate indoor and architectural environments safely. The application combines real-time URP visual impairment shaders, first-person physics locomotion with automatic step and stair climbing, a 360° proximity-sensing haptic belt controller (hardware serial or software mock), a full 3D BIM digital twin of the INSA Rennes campus, and an experimental telemetry logger for quantitative A/B evaluation.

---

## Key Features

- **360° Spatial Haptic Obstacle Belt (`HapticObstacleFeedback`)**
  - **Dedicated Obstacle Awareness:** Vibrations are reserved strictly for spatial obstacle proximity (rather than mixing obstacle warnings with turn-by-turn navigation cues on the same actuators).
  - **True Body-Surface Clearance:** Computes obstacle distance outward from the user's physical collider radius (`bodyRadius`) rather than from the sensor origin alone, ensuring danger alerts reach 100% intensity prior to physical contact.
  - **Multi-Height & Diagonal Coverage:** Scans knee level (`-0.55 m`), primary waist/chest level, and head-overhang height (`+0.45 m`), while blending ±32° front diagonals to catch doorframes, narrow pillars, and corners.
  - **Corridor-Friendly Asymmetric Ranges:** Longer forward lookahead (`2.0 m`) with tighter lateral (`0.80 m`) and rear (`0.60 m`) thresholds, plus automatic rear-channel muting while walking forward.
  - **Anti-Numbness Pulse Modulation:** Emits rhythmic pulses (`2–8 Hz`) in the caution zone and switches to continuous solid vibration only in close danger zones (`dangerSolidThreshold = 0.72`).

- **Hardware-Agnostic Serial Driver Bridge (`VibrationManager_minimal` & `Driver`)**
  - **Desktop Mock & Hardware Modes:** Runs out of the box on any workstation with `useMock = true`, or drives a physical 4-channel ERM/LRA actuator controller over serial (`115200` baud, `0–254` PWM bytes + `255` end marker) when `useMock = false`.
  - **Configurable Channel Mapping:** Reassigns logical directions (`Front`, `Back`, `Left`, `Right`) to physical hardware channels `0–3` directly in the Inspector, with thread-safe emission and dirty-packet filtering.

- **Real-Time Visual Impairment Simulation (`VisualDefectController`)**
  - **7 Screen-Space URP Shaders** on a camera-attached overlay quad (`ShaderScreenQuad`):
    - Unilateral Hemianopsia (right visual field loss)
    - Bilateral Hemianopsia (peripheral left/right tunnel narrowing)
    - Central Scotoma
    - Tunnel Vision
    - Patchy Loss (_Parcellaire_ noise)
    - Animated _Parcellaire_ (time-varying Voronoi)
    - Low-Acuity Blur (13-tap Gaussian screen-space blur via URP `_CameraOpaqueTexture`)
  - Instant runtime switching via keyboard hotkeys or Inspector dropdown (`VisualDefectMode`).

- **Locomotion, Audio Checkpoints & Experimental Telemetry**
  - **Smooth Keyboard Locomotion & Step Climbing (`UserControl`):** Framerate-independent `Rigidbody` walking (`4.2 m/s` default, `1.7x` sprint modifier) with automatic doorstep, curb, and staircase climbing (`maxStepHeight = 0.45 m`) and continuous collision detection.
  - **Decoupled Waypoint Audio Cues (`Navigation` + `ExperimentMetricsLogger`):** Plays a synthesized procedural chime when passing waypoints so checkpoint feedback never interferes with haptic obstacle signals.
  - **Quantitative Trial Logger:** Tracks elapsed time, distance walked, obstacle collisions (filtering out floor contacts), total wall-contact duration, and waypoint progress, with one-key CSV export.

- **Portable Player Rig (`HapticPlayerRig.prefab`)**
  - All locomotion, camera, visual defect overlays, haptic sensing, and telemetry components are packaged into `Assets/Prefabs/HapticPlayerRig.prefab` for drag-and-drop integration into larger environments or XR rigs.

---

## Controls & Runtime Hotkeys

All controls are supported in Unity Play Mode (compatible with both the Unity Input System and legacy Input Manager):

| Key                     | Category   | Action                                                                  |
| :---------------------- | :--------- | :---------------------------------------------------------------------- |
| **`W` / `Up Arrow`**    | Locomotion | Walk forward                                                            |
| **`S` / `Down Arrow`**  | Locomotion | Walk backward                                                           |
| **`A` / `Left Arrow`**  | Locomotion | Turn left                                                               |
| **`D` / `Right Arrow`** | Locomotion | Turn right                                                              |
| **`Left Shift`**        | Locomotion | Hold for brisk walk (`1.6x` speed)                                      |
| **`0`**                 | Vision     | Normal vision (no impairment overlay)                                   |
| **`1`**                 | Vision     | Unilateral Hemianopsia (right field loss)                               |
| **`2`**                 | Vision     | Bilateral Hemianopsia                                                   |
| **`3`**                 | Vision     | Central Scotoma                                                         |
| **`4`**                 | Vision     | Tunnel Vision                                                           |
| **`5`**                 | Vision     | Patchy Loss (_Parcellaire_)                                             |
| **`6`**                 | Vision     | Animated _Parcellaire_ (Voronoi)                                        |
| **`7`**                 | Vision     | Low-Acuity Blur                                                         |
| **`H`**                 | Haptics    | Toggle haptic motor output **ON / MUTED** (for A/B testing)             |
| **`R`**                 | Telemetry  | Reset current trial counters (time, distance, collisions, contact time) |
| **`L`**                 | Telemetry  | Append current trial summary row to CSV log                             |

---

## Requirements

- **Unity Editor:** `6000.6.4f1` (Unity 6)
- **Render Pipeline:** Universal Render Pipeline (`17.6.0`) with **Opaque Texture enabled** (`PC_RPAsset` / active URP Asset) for the Low-Acuity Blur shader
- **Packages:** `com.unity.inputsystem`, `com.unity.ai.navigation`, `com.unity.render-pipelines.universal`
- **Hardware (Optional):** 4-channel serial haptic interface connected via USB-UART (CP210x driver provided in parent workspace) when disabling mock mode

---

## Quick Start

1. Open the `3D Modelisation` folder in **Unity 6 (`6000.6.4f1`)**.
2. Open the prototyping obstacle scene at `Assets/Scenes/TP5.unity` (or drop `Assets/Prefabs/HapticPlayerRig.prefab` into a custom scene).
3. Select **`Cylinder`** (`HapticPlayerRig`) in the Hierarchy to verify default simulation settings:
   - **`VibrationManager_minimal`**: Keep `Use Mock` checked for desktop/software simulation, or uncheck and set `Device Port` (e.g., `COM4`) for physical hardware.
   - **`VisualDefectController`**: Choose an initial impairment or leave on `None` and switch live with keys `0–7`.
4. Press **Play** in the Unity Editor:
   - **Top-left HUD:** Real-time motor PWM outputs (`0–254`), obstacle clearance distances, caution/danger state, and `[ON]`/`[MUTED - H]` indicator.
   - **Top-right HUD:** Active visual impairment mode and quick hotkey reminder.
   - **Bottom-left HUD:** Elapsed time, distance walked, obstacle collision count, wall-contact duration, and waypoint progress.

---

## Experimental Evaluation & CSV Export

Designed to support repeatable **A/B user studies** (e.g., _Unassisted vs. Haptic-Assisted_ navigation under a chosen visual impairment):

1. Select a visual impairment (`1–7`).
2. **Baseline Run (Haptics OFF):** Press **`H`** to mute haptics and **`R`** to zero timers/counters. Complete the course, then press **`L`** to save the baseline row.
3. **Assisted Run (Haptics ON):** Press **`H`** to re-enable haptics and **`R`** to reset counters. Complete the course, then press **`L`** to append the assisted row.

Logged CSV files are written inside the project folder (in the Unity Editor):

```text
3D Modelisation/ExperimentLogs/haptic_experiment_trials.csv
```

*(In standalone player builds, logs fall back to `Application.persistentDataPath/haptic_experiment_trials.csv`.)*

Columns recorded per trial:
`Timestamp, Scene, Haptics, VisualImpairment, ElapsedSec, DistanceMeters, Collisions, WallContactSec, Waypoints`

---

## Project Architecture

```text
3D Modelisation/
├── Assets/
│   ├── Materials/          # URP defect materials (incl. Mat_Shader_Blur.mat)
│   ├── Prefabs/
│   │   └── HapticPlayerRig.prefab   # Portable first-person player, camera, defects & haptics rig
│   ├── Scenes/
│   │   ├── TP5.unity                # Simple wall-course prototyping & validation scene
│   │   └── INSA_Campus.unity        # Full INSA campus architectural environment
│   ├── Scripts/
│   │   ├── Driver.cs                     # Low-level 5-byte serial & mock packet driver
│   │   ├── VibrationManager-minimal.cs   # Thread-safe Unity-to-driver bridge & channel mapper
│   │   ├── HapticObstacleFeedback.cs     # 360-degree clearance scanning & pulse modulator
│   │   ├── UserControl.cs                # Physics & kinematic keyboard locomotion
│   │   ├── VisualDefectController.cs     # Runtime impairment switcher & HUD
│   │   ├── ExperimentMetricsLogger.cs    # Trial telemetry, collision filter, CSV & chime
│   │   └── Navigation.cs                 # Optional NavMesh path & waypoint tracker
│   ├── Settings/           # URP pipeline assets (PC_RPAsset with Opaque Texture enabled)
│   └── Shaders/            # Custom URP HLSL screen-space defect shaders
└── Captures/               # Verification renders of simulated visual impairments
```

---

## Porting to Larger Environments & XR Hardware

1. Drag **`Assets/Prefabs/HapticPlayerRig.prefab`** into your target scene (or attach `VibrationManager_minimal`, `HapticObstacleFeedback`, `VisualDefectController`, and `ExperimentMetricsLogger` to an existing XR Origin).
2. In VR/XR setups, assign the tracked **XR Main Camera** to `HapticObstacleFeedback.sensorOrigin` and configure `sensorHeightOffset` (e.g., `-0.35 m` scans at chest/belt height from head pose). Movement automatically decouples from head pitch/roll onto the horizontal XZ plane.
3. Ensure all walkable walls, pillars, and furniture in the target environment have **Colliders** on layers included in `HapticObstacleFeedback.obstacleLayer`.
