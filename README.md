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

## Current milestone: live-pinned Survey beta

The vertical slice provides an offline-safe exploration loop and traverses the
published immutable Clustr catalog from community region through subreddit,
post discussion, and a lazily inspected comment thread. It has
assisted six-degree-of-freedom flight, deterministic Kepler orbits, post
planets, comment moons/rings, user ships, bounded local gravity, a signal
scanner, discovery-gated Survey rendering, local constellations, contracts,
achievements, and HUD-only comment threads. The deterministic demo remains an
explicitly local fallback and is never presented as source data.

Every live session pins one manifest revision and spatial catalog. Bounded
responses are merged only when their revision, catalog, scope, and root match
the request. Published continuation cursors are followed up to the client
ceiling; changing selection invalidates the older request. If a full server
page omits a continuation cursor, the HUD says
`SERVER DID NOT PUBLISH CONTINUATION` rather than implying the region is
complete. The current public region response may also omit user nodes, so the
client cannot synthesize region traffic or user ships that were not published.

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

In Survey, open **AUTHOR PERSONAL CONSTELLATION** in the compact left HUD to
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

While the map is open, the HUD's `MAP CURSOR` row previews the nearest valid
next-level entity before selection. Explore displays the source label; Survey
uses `UNIDENTIFIED SIGNAL` until that entity is identified. This keeps labels
in the HUD instead of turning high-volume users into physical characters.

### Live and performance qualification

`--clustr-live-smoke` drives the real bounded hierarchy through overview,
region, system, discussion, and one parent thread. Add
`--clustr-beta-profile=600` to warm up, sample ten minutes, write a JSON report,
and exit the Linux player. Duration is bounded to 30–3600 seconds. The report
is written beneath Unity's persistent data directory at
`ClustrGalaxy/reports/latest-beta-profile.json` and records the pinned IDs,
resolution, CPU/GPU p95, maximum frame stall, p95 and peak per-frame GC allocation,
player memory, GPU memory, and an exact threshold verdict. Missing GPU counters
are a failed/unavailable measurement, never silently treated as a pass.

Example release-evidence run:

```bash
Builds/Linux/ClustrGalaxy.x86_64 --clustr-live-smoke --clustr-beta-profile=600 \
  -screen-fullscreen 0 -screen-width 2560 -screen-height 1440 \
  -logFile /tmp/clustr-galaxy-beta-profile.log
```

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
available. If a future deployment removes that route, the HUD labels public
data as `LEGACY / UNPINNED` and keeps the deterministic local system as the
flyable fallback.
