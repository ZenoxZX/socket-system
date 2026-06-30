# Socket System — Unity Package

Define named **sockets** on a rig's bones — transforms used to attach objects (weapons, props), drive IK targets, or take measurements — and apply them at runtime without re-walking the bone hierarchy.

A socket is a child transform under a chosen bone with its own local position, rotation and scale. Sockets are authored in a dedicated editor and stored in a `SocketSet` ScriptableObject; a runtime component rebuilds them on any prefab or instance that shares the same rig.

## Features

- **Socket editor window** — two-pane editor with an embedded 3D preview (`PreviewRenderUtility`) on the left and a properties panel on the right. Orbit / Fly camera, animation-clip scrubbing, per-socket move/rotate, and a collapsible socket list.
- **Scene View editing** — "Open in Scene View" opens a custom `PreviewSceneStage` (like Prefab Mode) where sockets are edited with native Unity Handles; changes are written back to the `SocketSet` on exit.
- **Visual preview** — assign a GameObject per socket to preview how an attached object sits (editor-only, not saved).
- **Rig identity validation** — a `SocketSet` stores a rig signature (bone names + hierarchy hash, FNV-1a). The same skeleton matches across different prefabs built from one FBX, regardless of the container object's name; the source asset GUID is kept for editor traceability.
- **Runtime binder** — `SocketBinder` validates the rig, then spawns each socket as a real child transform under its bone and exposes O(1) lookup via `GetSocket("name")`.
- **Humanoid + Generic** — bones are resolved by transform name; on humanoid rigs the `HumanBodyBones` mapping is offered as a fast path.

## Installation (Package Manager — Git URL)

1. Open **Window → Package Manager**.
2. Click **+** → **Add package from git URL...** and add:
   ```
   https://github.com/ZenoxZX/socket-system.git?path=src/Packages/com.zenoxzx.socketsystem
   ```

The package has no third-party dependencies — only Unity itself.

## Usage

1. Open **ZenoxZX → Socket System → Socket Editor**.
2. In the **Rig** tab, assign a rigged GameObject, prefab or FBX. The `Animator` is found automatically.
3. In the **Socket** tab, create a `SocketSet` with **New**, then **+ Add** sockets and pick each socket's parent bone.
4. Position each socket: drag the on-preview handle, type values directly, or click **Open in Scene View** and use native Handles. Use the **Visual** tab to attach a preview object.
5. At runtime, add a **`SocketBinder`** component to your character, assign the `SocketSet`, and call `GetSocket("RightHandWeapon")` to retrieve the socket transform.

## Requirements

- Unity 2022.3 LTS or newer
- No external package dependencies

## License

MIT — see [LICENSE.md](LICENSE.md)
