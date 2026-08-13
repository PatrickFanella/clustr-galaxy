# Clustr Galaxy

A controller-first Unity client for exploring Clustr's published community
universe as a flyable galaxy.

## Foundation

- Unity `6000.4.10f1`
- Built-in render pipeline for the prototype (URP package available)
- Unity Input System
- Linux and Windows desktop first
- Read-only consumption of Clustr's revision-pinned `spatial-scene-v1` API

The Unity client never connects directly to the PostgreSQL database on Almaz.
It pins an immutable graph revision through `/api/graph/manifest`, then streams
bounded overview, region, community, and entity payloads from the public API.

The accepted entity metaphor, visual grammar, metric mappings, orbital model,
and stability rules are defined in [`docs/galaxy-world-model.md`](docs/galaxy-world-model.md).

## Open the project

In Unity Hub, choose **Add > Add project from disk** and select this directory.
Use editor version `6000.4.10f1` when prompted.

## Current milestone

The current local vertical slice provides an assisted six-degree-of-freedom
observer probe, controller and keyboard input, targeting, deterministic Kepler
orbits, post planets, comment moons/rings, user ships, bounded local gravity,
and an information panel. Real API streaming and relationship routes come next.

## Prototype controls

Press **Play** in any scene. The current prototype is created at runtime.

| Action | Keyboard and mouse | Controller |
| --- | --- | --- |
| Forward/back and strafe | WASD | Left stick |
| Pitch/yaw | Mouse | Right stick |
| Rise/fall | Space / Left Ctrl | Triggers |
| Roll | Q / E | Bumpers |
| Boost | Left Shift | Left-stick press |
| Brake | X | East button (B/Circle) |
| Target reticle candidate | F | South button (A/Cross) |
| Toggle gravity/momentum flight | G | North button (Y/Triangle) |

The displayed bodies are deterministic prototype data. No Almaz database or
direct database connection is made. At runtime Unity reads the public Clustr
overview with a strict revision/catalog pin when the manifest route is
available. Until that route is deployed, the HUD labels the public overview as
`LEGACY / UNPINNED` and keeps the deterministic local system as the flyable
fallback.
