---
name: unity-editor-automation
description: >-
  Use this skill whenever the agent needs to act on the Unity Editor or verify
  Unity-side results: creating/editing scenes, prefabs, ScriptableObjects or
  project settings, reading the Unity console, checking that C# compiles after
  edits, running EditMode/PlayMode tests, making Android builds, or writing
  Editor tooling. Covers the MCP for Unity server, the batch-mode fallback
  script, folder/asmdef layout, and safe-editing rules for Unity YAML files.
---

# Unity Editor Automation

Two ways to drive Unity, in order of preference:
1. **MCP for Unity** (`unity-mcp` server in `.agents/mcp_config.json`). This is a live connection to the open Editor.
2. **Batch mode** via [unity-batch.ps1](scripts/unity-batch.ps1). It only works when the Editor is
   **closed**, because Unity locks the project.

If neither is available, say so and ask the user to open Unity or close it. Never claim code compiles
without one of these checks.

## 1. One-time setup (verify, don't assume)
- `uv` must be installed (`uvx --version`). The installer puts it in `%USERPROFILE%\.local\bin`.
  Restart the IDE after installing so the MCP host picks up the new PATH.
- The Unity package `com.coplaydev.unity-mcp` must be in `Packages/manifest.json`. Then in Unity:
  **Window → MCP for Unity** → start the bridge and confirm it shows "Connected".
- If the `unity-mcp` tools don't appear in the agent's tool list, the server failed to start.
  Check uv, then the Unity bridge window, then restart the IDE.

## 2. The edit → verify loop (mandatory after every C# change)
1. Save the script files.
2. Ask Unity to refresh/recompile (MCP: refresh assets / request script compilation).
3. Wait for compilation to finish (poll editor state; don't read the console mid-compile).
4. Read console **errors and warnings** (MCP: read console, filter to Error + Warning).
5. Fix every error *you* introduced. Report any pre-existing ones, but don't silently "fix" unrelated code.
6. Run the relevant EditMode tests (MCP: run tests, or `unity-batch.ps1 test-editmode`).
7. Only then report the task as done, including the test counts.

## 3. Scenes, prefabs, and assets
- Prefer creating content **through the Editor API** (MCP tools or Editor scripts) over hand-writing
  `.unity`/`.prefab` YAML. Hand-edited YAML breaks fileID/GUID references.
- **Never** create, delete, or rename an asset without its `.meta` file following, and never invent GUIDs.
  Let Unity generate them.
- Small edits to `ProjectSettings/*.asset` YAML are acceptable **only while the Editor is closed**.
  An open Editor overwrites them on its next save. With the Editor open, change settings via MCP or an Editor script.
- Tunable gameplay values live in ScriptableObjects under `Assets/_Project/Settings/`. Create them
  through `[CreateAssetMenu]` + MCP, not by writing YAML.

## 4. Project layout (create on first use, keep consistent)
```
Assets/_Project/
├── Scripts/
│   ├── Runtime/            VRVolley.Runtime.asmdef      (namespaces VRVolley.*)
│   │   ├── Physics/  XR/  Gameplay/  Rules/  AI/  UI/  Audio/  Core/
│   ├── Editor/             VRVolley.Editor.asmdef       (Editor platform only)
├── Tests/
│   ├── EditMode/           VRVolley.Tests.EditMode.asmdef (Editor only, refs Runtime + nunit)
│   └── PlayMode/           VRVolley.Tests.PlayMode.asmdef
├── Prefabs/  Scenes/  Settings/  Art/  Audio/  Materials/
```
- Every runtime script lives in an asmdef. Nothing new goes into `Assembly-CSharp`.
- Editor-only code goes in the `Editor` asmdef **or** is wrapped in `#if UNITY_EDITOR`. Both are fine;
  the asmdef is preferred.
- Third-party and SDK folders (`Assets/Oculus`, `Assets/XR`, `Assets/XRI`, `Assets/Plugins`) are
  **read-only** for agents unless the user asks.

## 5. Batch-mode fallback
[unity-batch.ps1](scripts/unity-batch.ps1) resolves the Editor path from `ProjectVersion.txt`.
```powershell
.\.agents\skills\unity-editor-automation\scripts\unity-batch.ps1 compile          # import + compile, prints errors
.\.agents\skills\unity-editor-automation\scripts\unity-batch.ps1 test-editmode    # NUnit XML → Logs/
.\.agents\skills\unity-editor-automation\scripts\unity-batch.ps1 test-playmode
.\.agents\skills\unity-editor-automation\scripts\unity-batch.ps1 build-android    # needs VRVolley.Editor.BuildScript.BuildAndroid
```
Exit code 0 = success. On failure the script prints the error lines from the log (`error CS`,
`Exception`, failed tests).

## 6. Editor tooling conventions
- Menu items under `VR Volley/…` (e.g., `VR Volley/Physics/Validate Ball Profiles`).
- Validation tools must be idempotent and report via `Debug.LogError` with the asset as the context
  object, so a click in the console selects it.
- A `BuildScript.BuildAndroid()` static method in `VRVolley.Editor` is the single source of truth
  for build settings (scenes list, IL2CPP, ARM64, output `Builds/VRVolley.apk`).

## Validation
After any automation task, report (a) which path was used (MCP or batch), (b) the console error
count after compile, and (c) test results, e.g. `EditMode: 24 passed, 0 failed`.