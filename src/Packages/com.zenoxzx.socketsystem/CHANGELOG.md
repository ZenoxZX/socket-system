# Changelog

All notable changes to this package are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0]

### Added

- `SocketSet` ScriptableObject storing sockets and a rig signature.
- Socket editor window with embedded 3D preview, Orbit/Fly camera, and animation-clip scrubbing.
- On-preview move handle and a collapsible socket list.
- "Open in Scene View" custom `PreviewSceneStage` for editing sockets with native Handles, writing back on exit.
- Visual tab to attach per-socket preview objects (editor-only).
- Rig identity validation via bone-hierarchy hashing (FNV-1a), tolerant of container renaming and LOD/mesh differences.
- `SocketBinder` runtime component with `GetSocket(name)` lookup.
- Humanoid and Generic rig support.
