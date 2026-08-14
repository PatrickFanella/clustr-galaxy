# Clustr Galaxy

A controller-first Unity client for exploring Clustr's published community
universe as a flyable galaxy.

## Foundation

- Unity `6000.4.10f1`
- Universal Render Pipeline (URP 17.x)
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

## Architecture

Runtime code is split into two dependency-directed assemblies:

- `ClustrGalaxy.Core` contains snapshot, resident-galaxy, Survey, navigation,
  galaxy-session, persistence-port, and HUD-presentation state machines. It has
  no Unity lifecycle, scene search, input action, or `MonoBehaviour` dependency.
- `ClustrGalaxy.Unity` contains the thin networking, file-format, input,
  rendering, physics, camera, and IMGUI adapters. `GalaxySessionBehaviour` owns
  their lifetime and the plain-C# `GalaxySession` composition root.

Survey transitions are atomic intents. A successful intent produces one
immutable outcome and exactly one durable write; presentation observers cannot
start nested mutations. Loaded graph facts and active/targetable projections
are separate resident-galaxy queries. See [`CONTEXT.md`](CONTEXT.md) and the
ADRs in [`docs/adr`](docs/adr).

This release intentionally starts clean storage under `ClustrGalaxy/profiles/v2`
and `ClustrGalaxy/cache/v2`. It never reads, migrates, overwrites, or deletes v1
profile/cache files.

## Current milestone: offline Survey gameplay beta

The local vertical slice provides an offline-safe exploration loop. It has
assisted six-degree-of-freedom flight, deterministic Kepler orbits, post
planets, comment moons/rings, user ships, bounded local gravity, a signal
scanner, discovery-gated Survey rendering, local constellations, contracts,
achievements, and HUD-only comment threads. The same revision-pinned loader is
ready to consume bounded public graph data whenever the manifest is published;
the deterministic demo remains an explicitly local fallback.

### Explore and Survey

**Explore** exposes every entity currently loaded by the selected snapshot. It
supports map navigation and fast travel, but never changes Survey progress.

**Survey** begins by choosing one loaded subreddit/system. Only that starting
system is known. Target a known signal, hold Scan, and its compatible routes
and destinations are revealed. Survey progress is saved locally under the
revision/catalog key, so it neither leaks into another snapshot nor silently
merges different data revisions. Unknown signals cannot be fast-travelled to;
inspect, scan, and fly between the systems you have actually discovered.

### Constellations, threads, and privacy

The demo has an **authored overlay** constellation. Scanning a member reports
progress such as `Example Constellation 1/6`; it is presentation content and
never rewrites the factual graph. The local data model can create a personal
constellation from known stable IDs with a local name and note/reason, and it
is saved only on this machine. Choosing to queue a saved constellation creates
a local outbox record marked
`QUEUED LOCALLY — SUBMISSION SERVICE UNAVAILABLE`; no network request, account
identity, credentials, or automatic publication is involved.

In Survey, use **ADD CONSTELLATION MEMBER** in the compact left HUD to
add the current identified target, review up to 12 member rows, and enter a
local name and reason. A constellation needs 3–12 unique known entities.
**SAVE LOCALLY** is the only action that writes a constellation. Queuing is a
separate two-step action: **QUEUE FOR REVIEW**, then **CONFIRM LOCAL QUEUE**.
The confirmation writes only the local outbox and displays
`QUEUED LOCALLY — SUBMISSION SERVICE UNAVAILABLE`; it never makes a network
request or publishes anything. Text fields deliberately use keyboard/mouse
entry. Controller flight, targeting, scanning, and navigation remain usable,
but controller text entry is not currently claimed or implemented.

Comment replies are an inspection HUD tree, bounded to three depths. Replies
are not physical planets, moons, or constellation entities.

### Flight modes and diagnostics

World mode and flight controls are independent and can be changed at any time:

- **Autopilot** can plot an eligible known-system approach.
- **Pilot Assist** is the default damped flight mode.
- **Pilot Mastery** preserves manual control and can earn arrival progression.

The normal HUD keeps p95 frame time and memory compact. Press **F10** to show
the developer performance counters: active/pooled/visible/known entities, scan
events, per-frame GC allocation, and main-thread time.

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
| Change pilot mode | `[` / `]` | D-pad left / right |
| Scan known target (hold) | R | West button (X/Square) |
| Galaxy map / Survey map | M / V | Select / D-pad up |
| Confirm Survey starting system | Enter | South button (A/Cross) |
| Fast travel / plot autopilot | T / P | D-pad down / Right-stick press |
| Developer performance HUD | F10 | Keyboard only |

The displayed demo bodies are deterministic prototype data. No Almaz database
or direct database connection is made. Use `--clustr-offline` (or the legacy
single-dash spelling `-clustr-offline`) to require the last verified local
cache; when no cache exists the HUD stays in an honest offline-unavailable
state instead of attempting a network fallback. In normal mode Unity reads the
public overview with a strict revision/catalog pin when the manifest route is
available. Until then the HUD labels public data as `LEGACY / UNPINNED` and
keeps the deterministic local system as the flyable fallback.
