# Clustr Galaxy

A controller-first Unity client for exploring Clustr's published community
universe as a flyable galaxy.

Run `python3 scripts/full_pass.py` from this directory for the automated pass
before human testing. It builds both Linux players and serializes correctness,
controller, campaign, persistence, simulation, visual and performance checks.
See [the qualification procedure](docs/linux-qualification.md) and
[the completion ledger](docs/beta-completion.md) for evidence and unresolved gates.

## Foundation

- Unity `6000.4.10f1`
- Universal Render Pipeline (URP 17.x)
- Unity Input System
- Linux beta target (Windows qualification is outside this milestone)
- Read-only consumption of Clustr's revision-pinned `spatial-scene-v1` API

The Unity client never connects directly to the PostgreSQL database on Almaz.
It pins an immutable graph revision through `/api/graph/manifest`, then streams
bounded overview, region, community, and entity payloads from the public API.

The accepted entity metaphor, visual grammar, metric mappings, orbital model,
and stability rules are defined in [`docs/galaxy-world-model.md`](docs/galaxy-world-model.md).

The current [flight-deck presentation iteration](docs/visual-direction.md) adds a cockpit, instrument HUD, command console, smooth orbit guides and much larger demo planets with a warm sun. Its build and visual evidence are recorded separately from release qualification.

The next [orbital flight implementation](docs/superpowers/plans/2026-09-08-orbital-flight.md) follows the approved Newtonian/assisted-orbit direction. [Technical research on Elite Dangerous and Star Citizen](docs/space-simulation-research.md) records primary sources, historical limitations, implementation choices and outstanding travel systems.

## Open the project

In Unity Hub, choose **Add > Add project from disk** and select this directory.
Use editor version `6000.4.10f1` when prompted.

## Current milestone: Linux beta implementation, not qualified

The architecture migration and Linux qualification are in progress. The current
checkout has a retained uGUI HUD, a session-backed runtime, transactional Survey
updates, strict snapshot validation, v2 caching, and Linux build entrypoints.
It must not be described as a completed or qualified beta. See the
[completion ledger](docs/beta-completion.md) for tests, player evidence, known
implementation gaps, and external qualification blockers.

Normal launch attempts live data, then verified v2 cache. If both are unavailable,
Actions offers the explicitly authored deterministic demo. `--clustr-demo` selects
that namespace directly. `--clustr-offline` sends no Clustr requests and requires
verified v2 cache; absent cache displays unavailable. v1 storage is untouched.

A session pins its revision and spatial catalog until a new session is created.
Bounded responses require matching revision, catalog, scope, and root. On
2026-09-06 the public overview loaded, but the community response omitted required
scope/root and continuation metadata. The client rejected it and retained the
last usable view. Full public hierarchy compatibility is therefore unresolved.

Validate with `python3 scripts/validate.py`; build configurations separately with
`python3 scripts/build.py --configuration development` and
`python3 scripts/build.py --configuration release`. Set `UNITY_EDITOR` to the
licensed 6000.4.10f1 editor with Linux support installed. Commands retain timestamped
logs and results under `Builds/`. A passing test XML does not override a failing
Unity process exit. Runner setup is in [Linux qualification](docs/linux-qualification.md).

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
`ClustrGalaxy/reports/<timestamp>-<run-id>.json` (the separate
`latest-report-path.txt` points to the latest report) and records the pinned IDs,
resolution, CPU/GPU p95, maximum frame stall, p95 and peak per-frame GC allocation,
player memory, GPU memory, and an exact threshold verdict. Missing GPU counters
are a failed/unavailable measurement, never silently treated as a pass.

Example isolated release-artifact evidence run (Vulkan, Wayland, direct rendering):

```bash
python3 scripts/hierarchy_smoke.py --live --active-features --profile-seconds 600 \
  --window-system wayland --single-threaded-rendering
```

The harness uses `Builds/LinuxRelease/ClustrGalaxy.x86_64`, a private compositor,
and isolated local storage. Run the deterministic near-limit scenario separately
with `--stress` in place of `--live`. Passing one scenario does not qualify the
other, or replace the functional, platform, and human acceptance gates.

## Prototype controls

Press **Play** in any scene. The current prototype is created at runtime.

| Action | Keyboard and mouse | Controller |
| --- | --- | --- |
| Forward/back thrust | W / S | Right / left trigger |
| Sideways thrust | A / D | Bumpers |
| Pitch / yaw | Mouse | Right stick vertical / left stick horizontal |
| Rise/fall | Space / Left Ctrl | Left stick vertical |
| Roll | Q / E | Right stick horizontal |
| Boost | Left Shift | Left-stick press |
| Brake | X | East button (B/Circle) |
| Target reticle candidate | F | South button (A/Cross) |
| Change pilot mode | `[` / `]` | D-pad left / right |
| Scan known target (hold) | R | West button (X/Square) |
| Galaxy map / Survey map | M / V | Select / D-pad up |
| Confirm Survey starting system | Enter | South button (A/Cross) |
| Charge route jump / plot autopilot | T / P | D-pad down / Right-stick press |
| Developer performance HUD | F10 | Keyboard only |

Gravity is active in every pilot mode. Assist uses bounded counterthrust to control velocity; Mastery retains momentum and responds to gravity. Brake counters translation and rotation. Severe impacts and stellar exclusion entry recover to a safe hold without removing progress; choose **Resume Flight** to coast from that hold. These changes are under simulation qualification; see [the execution ledger](docs/superpowers/plans/2026-09-08-flight-first-simulation.md).

Select a planet, open **Tab / Start → Selection**, and choose **System Cruise · Auto Approach** or **System Cruise · Manual**. Cruise has a three-second charge and is explicitly a fictional drive, separate from local Newtonian propulsion. Manual cruise uses the accepted attitude controls; triggers adjust its throttle and B brakes. The console provides **Cancel Cruise · Safe Drop**. The automatic nearby-planet journey is verified at approximately 82 simulated seconds including physical departure; arrival returns to local flight outside the safety volume and below 20 m/s relative closing speed. This is functional journey evidence, separate from release performance qualification.

For another system, open **Selection → Jump** and choose an eligible loaded connection, or use **T / D-pad down** on a selected eligible system. Jumps charge for three seconds, validate the frozen-snapshot destination, and then enter its local physical frame. **Cancel Jump** leaves the current system in place. Connection choices remain available after render objects unload; Survey keeps unknown labels hidden. Opening source data is inspection and does not move the ship. No new source connections are generated.

Observatory, editing and replay views display a local-simulation pause notice. Closing them restores the prior simulation rate, velocity and flight/menu state. The command console itself keeps physics active. See [flight/travel authority](docs/adr/0006-flight-and-travel-authority.md) and the [dedicated simulation journey](scripts/simulation_journey.py) for implementation limits and repeatable verification.


The displayed demo bodies are deterministic prototype data. No Almaz database
or direct database connection is made. Use `--clustr-offline` (or the legacy
single-dash spelling `-clustr-offline`) to require the last verified local
cache; when no cache exists the HUD stays in an honest offline-unavailable
state instead of attempting a network fallback. In normal mode Unity reads the
public overview with a strict revision/catalog pin when the manifest route is
available. If a future deployment removes that route, the HUD labels public
data as `LEGACY / UNPINNED` and keeps the deterministic local system as the
flyable fallback.

## Exploration expansion

The beta scope now includes twelve optional exploration systems in a cozy cosmic-curiosity direction. See [the feature catalogue](docs/exploration-features.md), [implementation roadmap](docs/superpowers/plans/2026-09-06-exploration-expansion.md), and [completion ledger](docs/beta-completion.md). Specifications are not completion claims: the expansion and existing Linux qualification gates remain open. Story content is authored demo fiction; live activities preserve published source relationships.

The expansion currently includes optional scan tuning, local musical feedback,
constellation layout editing and PNG postcards. A session-owned rolling recorder
keeps up to 15 minutes in memory at 10 Hz; Actions → Save rolling replay writes an
explicit local `.clustr-replay` file. Switching Explore/Survey or flight mode
starts a separate recording. The local library supports replay viewing, bounded imports, exports and explicit deletion; full player acceptance remains open.
See the [completion ledger](docs/beta-completion.md) for exact automated/player
evidence and the unresolved release gates; these working-tree builds are unqualified.

Optional flight activities are available through Actions: start or preview the ring
run, figure eight or stable-orbit exercise; select a resident body to see up to
eight eligible published routes with three scenic profiles each. Autopilot previews
are unscored; Assist and Mastery keep separate local records. End flight activity
returns to free flight. Complete a manual activity, save its flight recording, and
choose View latest flight recording for local top-down playback with play/pause,
restart and back. Playback suppresses flight input and grants no progression.
Enable Save personal-best ghosts in Actions to keep a recording with future best
records (off by default). Compatible ghosts appear during practice and can be
hidden or shown. Scores and ghosts save together; unsuccessful saves can be
retried. Figure-eight and orbit courses include decorative training bodies.
Normal-flight pacing, complete controller acceptance and feature performance remain
unfinished.

The demo campaign now includes authored clues near six familiar demo bodies.
Look toward a nearby identified body for three seconds to notice available clues,
then open **Campaign notebook** in Actions to read collected observations. Trails
can be followed or unpinned; completed trails retain a named keepsake. Gathering
all five observations for a mystery enables three supported interpretations with
an optional written explanation. Live mode provides personal notes instead of
demo fiction. Campaign content and its UI remain under qualification; see the
ledger for the distinction between automated evidence and player acceptance.

Mystery investigations now open an evidence board after all five observations are discovered. Arrange cards with the right stick/WASD or mouse dragging, draw a personal connection, then submit a supported interpretation. Board and interpretation save together; old completed cases remain complete. See [the custom exploration art inventory](docs/exploration-art.md) for editable original asset sources. The catalogue remains in progress; evidence-board integration does not complete all twelve feature gates.

Use **Actions → Capture Screenshot** for a clean rendered-view preview. Choose **Save Image** to keep it or **Discard Image** to leave no permanent file. Local Creations views PNGs and replays, counts image metadata in storage totals, and keeps deletion separate from selection. Replay playback has a seekable timeline and 0.5×/1×/2× speed. Complete current-playground replay exports carry an explicit course anchor; **Use Selected Practice Ghost** selects one for the next matching course/mode/tuning. Legacy packages remain standalone. These features are still undergoing the full acceptance matrix.


The observatory has six durable displays with original keepsakes, saved constellation geometry and palettes, actual recorded flight paths, and journal captions. **Clear Display Slot** retains the saved original. **Browse Journey Diary** opens the latest 200 automatic entries plus saved bookmarks retained until removal; keyboard text entry uses explicit **Save Bookmark**. Diary and replay viewers export 1920×1080 PNG postcards into **Local Creations**. Returning from a child view restores its originating control and keeps flight suspended until the parent closes. Feature artwork and complete player acceptance remain in progress; see the completion ledger for artifact-specific evidence.


Actions now groups **Flight & Selection**, **Discoveries**, **Creations & Diary**, **Routes & Expeditions**, and **Audio & Accessibility**, with **All Actions** available. Confirm/back navigation and the approved ship bindings remain unchanged. In the diary, **Keep Selected Moment** preserves an automatic moment; **Remove Saved Bookmark** requires a second confirmation and can be cancelled.

Replay format 3 preserves up to 256 plain-text annotations for chosen bookmarks, committed discoveries and completed activities. Versions 1 and 2 remain readable; annotations never grant progression during playback. Use `--clustr-profile=my-pilot` for a separate local profile (1–64 letters, digits, hyphens or underscores). Omitting it preserves the existing `offline-pilot` profile. Profiles are local storage identities, not accounts.

The feature workload preflight is `python3 scripts/hierarchy_smoke.py --stress --profile-seconds 30 --active-features`. It records observed audio, ghost visibility, course guidance and pooled celebration activity alongside the unchanged performance limits. Its synthetic practice ghost is fixture content, not a player record. Missing workload or measurement evidence fails the run; a 30-second preflight never qualifies a release. `python3 scripts/replay_import_journey.py --source /absolute/path/to/survey.clustr-replay` verifies standalone import under a fresh named recipient, with no progression writes and recoverable malformed/missing media errors.

For separate editor/observatory measurements, run `python3 scripts/interaction_profile.py --profile-from /absolute/path/to/completed-demo/ClustrGalaxy/profiles/v2`. It copies only the explicit v2 fixture profile into new evidence-owned storage, records each UI window including entry/exit, and verifies both source and copy remain unchanged. UI allocations are reported separately from the zero-allocation ordinary-flight requirement. Missing counters, overflow and insufficient duration fail diagnostics; these reports never qualify a release.


On the current reference host, the recorded complete GPU diagnostic path is
`--window-system wayland --single-threaded-rendering` on both profiling scripts.
This requests native Wayland with direct Vulkan rendering; the evidence log
records `kGfxThreadingModeDirect`. Normal threaded runs have had missing GPU
samples. Keep results tied to the recorded rendering configuration, and retain
failed runs. A passing UI diagnostic or numerical 30-second preflight does not
qualify the release.

For hands-on feature inspection, `scripts/manual_acceptance.py` accepts all three
target resolutions and optional `--profile-from` with a completed fixture's v2
profile directory. It copies progress into isolated storage and labels the run
as seeded. Use a fresh run for discoverability/pacing acceptance. See the
[physical-controller checklist](docs/manual-controller-acceptance.md).

Current artifact, automated results and the explicit remaining gates are collected in the [feature acceptance checkpoint](docs/feature-acceptance-checkpoint.md).


### Cockpit stations

The local chart is projected into the cockpit. Opening it suppresses pilot input while gravity, motion and guidance continue; closing it returns control without restoring an old velocity. Creations, diary, observatory and replay views visibly pause local simulation. Nested views keep that pause until the last view closes. The accepted flight bindings are unchanged.

Scenic corridors and playgrounds use body exclusion clearances. If safe space is unavailable, the activity reports that condition. Course version 2 keeps older recordings available for standalone viewing while separating current competitive ghosts. Expedition stop proximity is measured outside the destination exclusion boundary, so a star can be visited without entering it. Player evidence and unresolved release gates are tracked in `docs/beta-completion.md`.


### Cached process startup evidence

`scripts/warm_start_smoke.py --cache-from /path/to/cache/v2` copies a verified
v2 cache into a private configuration and launches the release player offline.
For a deterministic fixture cache, also pass `--fixture`; an owned HTTP sentinel
then verifies that no fixture requests occur. The source cache is hashed before
and after and is never used as writable player storage.

The three-second measurement begins before process creation and ends when the
player has submitted the overview and cockpit HUD, saved a 2560×1440 screenshot,
and published its readiness marker. The external monotonic observer includes
screenshot writing and log-poll latency, so it is a conservative rendered-startup
bound. Missing markers, failed rendering, incorrect resolution, mismatched source
identity or elapsed time above three seconds fail the command. Compositor setup
and copying the already warm cache precede measurement. This gate is distinct
from the performance report's explicitly labelled cached-source-open timer.

The full-pass command runs fixture and public-cache startup checks alongside its
functional and 600-second performance matrix. Neither a passing performance
report nor a passing startup check establishes deferred human, Ubuntu licensing,
or hosted CI acceptance.


Latest local automated pass (2026-09-09): `ecba4baffca5fb5e21d637150bdf4232b31faeea` passed all 24 stages of `scripts/full_pass.py` from a clean checkout, including both 600-second performance scenarios. The [completion ledger](docs/beta-completion.md) records exact results. Local candidate artifacts and notices are under `Builds/handoffs/ecba4ba`; human acceptance, licensed Ubuntu verification and licensed CI remain open. This is not release qualification.

### Free presentation assets

The September 19 free-only presentation pass at `6f2546b` has passed clean-source Unity tests, both Linux builds and the complete automated gameplay/screen matrix. The original full run passed 23/24 stages; live performance passed, and an unchanged-artifact monitored repeat passed the full stress requirements. The original stress failure remains preserved. Exact evidence and remaining human/Ubuntu/CI gates are tracked in the completion ledger. The local candidate is under `Builds/handoffs/6f2546b-free-assets`. It adds textured celestial surfaces, curated cockpit/ship models, bounded audio feedback and controller glyphs. See [asset notices](docs/free-asset-notices.md) and the [implementation/verification record](docs/superpowers/plans/2026-09-19-free-presentation-assets.md). No new qualified player is claimed.
