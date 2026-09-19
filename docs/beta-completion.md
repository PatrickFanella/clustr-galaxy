# Linux beta completion ledger

Baseline: `775f826f51ee70d8f25e3e421887eaa5a6f10801` on main.
Status: **NOT QUALIFIED — implementation remains incomplete** (2026-09-06).
All work is local and uncommitted. There is no final qualified source SHA.
Historical binaries and reports remain historical evidence.

## Full automated pass — September 8 local / September 9 UTC

The final run is [20260909T011215566712Z-full-pass](../Builds/evidence/20260909T011215566712Z-full-pass/summary.json).
**Local functional checks pass; the full gate remains failed and the beta is not
qualified.** Validation `20260909T011215942793Z` passes 271 EditMode and 23 PlayMode
tests, with zero skips and no compiler diagnostics. Twelve Python harness tests
pass. Builds `20260909T011411Z` pass in development and release configurations.

- Release tree SHA-256: `eb80105fff185a1f0b765894738fac7e76021897282c40f8b5a65549fe0d6a12`.
- Development tree SHA-256: `6af3489d88cc59caa305f7af25a6cbc715b092445e086fb6836bf94e73202dcc`.
- Source remains `775f826f51ee70d8f25e3e421887eaa5a6f10801-working`. The run records
  a separate 1,093-file input fingerprint; it is not a clean commit. Captured
  inputs and player artifacts remained unchanged during the final journeys.

Passing player evidence includes controller Actions/Back smoke at all three
resolutions; the full 1440p campaign, flight fixtures and three expedition moods
across create/resume/restart; 720p/1080p creation and restart; all three resolution
orbit, moon capture/coast, scan approach, collision recovery, cruise and jump
safety journeys; separate-profile replay import without progression; cockpit
interaction profiling; online fixture hierarchy/jumps followed by cache-only
restart with zero provider requests; and development-player hierarchy smoke.
The full campaign phases take 302.044, 80.842 and 38.291 seconds. v1 sentinels
remain byte-identical. Captured essential screens were inspected at 720p, 1080p
and 1440p. This is not human acceptance or proof of physically flown scored
fixture courses.

Two findings were corrected before the final run: the visual music caption
overlapped the studio footer, and a ghost rebase assertion compared different
moving replay instants while command menus correctly kept physics running. The
caption now occupies the lower margin in paused stations. The rebase assertion
uses a paused local clock and end-of-frame rendering while preserving its 0.5 m
tolerance and requiring the ghost to remain visible. Failed/interrupted runs
`20260909T005010539685Z-full-pass` and `20260909T005942533713Z-full-pass`, including
the failed campaign `20260909T010528106595Z-demo-journey`, remain preserved.

Stress run `20260909T013439514048Z-hierarchy` measures **600.0196 seconds** at
2560×1440 using recorded direct Wayland/Vulkan rendering on the Ryzen 9 9900X /
Radeon RX 7900 XT host. All 64,941 GPU samples are valid; CPU p95 is 12.5177 ms,
GPU p95 0.8049 ms, maximum ordinary/all-frame stall 23.8991 ms, and managed
allocation is zero. Peak process RSS is 1,456,422,912 bytes, GPU requested buffers
489,226,240 bytes, and cache usage 18,225,621 bytes. The retained graph contains
14,053 nodes and 57,000 routes, not necessarily that many rendered bodies. The
scenario records moving flight, guidance, a ghost, music and capped celebrations.
Every numerical threshold passes, but the report correctly fails with
**Build provenance unavailable**. Cached-source open is 0.0141 seconds; this does
not establish process-to-visible warm-overview latency.

Public attempt `20260909T014507633119Z-hierarchy` loads revision 241/catalog 14's
300-node/180-link overview, then fails community traversal with `BOUNDED SCOPE
REJECTED / CURRENT VIEW RETAINED`. A separate single bounded diagnostic GET
returns HTTP 403; this does not establish why Unity rejected its response.
Public performance is not run after this failed prerequisite. Backend services
and validation policy are unchanged. Public compatibility, clean Arch and
licensed Ubuntu builds/runtime, Gitea activation, complete release packaging,
remaining implementation/physical-flight evidence and deferred human acceptance
remain open.

The accepted boundary is the September 6 completion plan. Public publication,
backend/database changes, Windows, accounts and network constellation submission
are excluded. A test XML pass, completed build pipeline, screenshot, and release
qualification are different claims.

## Current evidence

- Release rebuild with the current functional fixes passed at
  `Builds/build-logs/20260907T002848Z/BuildLinuxRelease.json` (working source).
  Full artifact-tree SHA-256:
  `07222398e00b94d7223d0b3fe372657400d897c65496d8f3833fb5d1c898e22c`.
- The demo harness now checks PNG dimensions against the requested resolution and
  configures the owned display to match. An initial 720p request correctly failed
  at `20260907T003009419803Z-demo-journey` because its display remained 1440p,
  even though gameplay passed. Corrected create/restart journeys passed at
  `20260907T003100453916Z-demo-journey` (720p) and
  `20260907T003136557582Z-demo-journey` (1080p), and
  `20260907T003211848344Z-demo-journey` (1440p), with unchanged v1 sentinels and
  unchanged saved profile bytes after restart. Full-resolution Actions captures
  were inspected for readable text and essential controls.

- Continuation on September 7 UTC: the queue-depth-one Wayland diagnostic at
  `Builds/evidence/20260907T002324659236Z-hierarchy/` failed honestly with 3,593
  valid GPU durations for 3,601 measured frames. CPU p95 was 8.335 ms, ordinary
  maximum 9.188 ms and managed allocation zero. The queue experiment did not
  resolve missing measurements and was removed; the default renderer queue is
  retained. This is historical evidence for that experimental artifact.
- Qualification report storage now preserves immutable timestamped JSON documents
  and atomically replaces only the latest-report pointer. Tests exercise report
  destination failure and pointer failure (which preserves the durable report but
  still fails the operation). Cache-source construction is included in the cache
  availability timer; this remains distinct from process-to-rendered-overview time.

- Current automated validation passes: **109/109 EditMode and 11/11 PlayMode**,
  both actual process exit 0, no timeout and no project compiler diagnostics.
  Evidence: `Builds/validation/20260907T002737220932Z/summary.json`.
  This is working-tree evidence, not a clean-checkout release gate.
- The native shutdown hang was traced with gdb to FMOD's PulseAudio shutdown
  callback, and reproduced in an empty Unity project. The shared process wrapper
  now uses private null-sink audio services; it does not alter desktop audio.
  Diagnostic: `Builds/shutdown-diagnostics/gdb-quit-backtrace.txt`.
- Development and release process/build gates passed in
  `Builds/build-logs/20260906T215636Z/`. The JSON records per-file hashes and actual
  exit 0. Those binaries are historical and remain unqualified. The subsequent
  `Builds/build-logs/20260906T222328Z/` rebuild also passed both configurations,
  including residency/cache/UI repairs. Fixture integration changes came afterward.
- Real development and release players passed virtual-controller Actions/back at
  1280x720, 1920x1080 and 2560x1440, with actual exit 0 and captured PNG dimensions
  checked. Evidence: `Builds/evidence/20260906T222541078782Z-ui-smoke/summary.json`
  and `Builds/evidence/20260906T222708344928Z-ui-smoke/summary.json`.
  Screenshots confirm the status panel remains visible; this is the narrow menu
  regression check, not full gameplay/controller acceptance.
- Full small-fixture hierarchy passed through rendered target selection, real
  navigation and local HTTP at `Builds/evidence/20260906T223222872974Z-hierarchy/`.
  The near-limit hierarchy also passed: community/system 5,000 nodes + 15,000
  links each; discussion 2,048 + 15,000; thread 2,000 + 12,000, across 34 requests.
  Evidence: `Builds/evidence/20260906T223259946363Z-hierarchy/summary.json` and
  checksum-verified aggregate cache documents in its isolated config directory.
- A 30-second development stress diagnostic **failed**. Vulkan timings and memory
  counters were available, but only 5,452/5,644 GPU timing samples were valid;
  map-state allocation p95 was 410,404 B/frame. The source was dirty. Evidence:
  `Builds/evidence/20260906T223341551791Z-hierarchy/`. No full qualification run
  follows a failed preflight. Subsequent changes reuse map candidates/hover text,
  prepare sample storage before warm-up, and distinguish ordinary flight/loading/
  other gameplay. Those changes still need fresh player performance evidence.
- The release player now measures managed allocations through an opt-in preloaded
  Mono profiler callback and GPU buffer memory through per-process DRM fdinfo.
  Both availability probes passed on the isolated Radeon host. The 30-second run
  at `Builds/evidence/20260906T225938552153Z-hierarchy/` still failed: incomplete GPU
  timing coverage, dirty source and 16,672 bytes allocated per ordinary frame.
  That exact allocation came from the starfield matrix array; a reusable buffer
  replaced it. The next 120 fps release diagnostic at
  `Builds/evidence/20260906T231701086708Z-hierarchy/` measured 3,600/3,600 valid GPU
  samples and zero ordinary-flight managed allocation over 30 seconds. CPU p95
  was 8.335 ms, GPU p95 0.815 ms. It still failed dirty build provenance.
  This is an idle ordinary-flight interval after stress hierarchy loading;
  moving-flight, full-window timestamp attribution and 600-second gates remain open.
- The demo's route contract was unreachable when the final post completed its
  system: the scan only considered routes originating at that post. Completion
  now reveals remaining weak routes originating at the completed system. Both
  authored destinations and route renderers are present. The persisted journey
  test now uses the actual two authored routes and verifies strongest-first
  revelation and an unidentified destination. Fresh suites above passed; the
  subsequent scripted real-player demo journey passed as recorded below.
- The strengthened release demo journey passed at
  `Builds/evidence/20260906T234017447287Z-demo-journey/summary.json`: actual D-pad
  focus traversal, confirm/back, controller reticle selection, held scans, local
  save/queue and restart. It uses fixture placement for scan range. Both player
  builds passed at `Builds/build-logs/20260906T233649Z/`. Retained screenshots show
  corrected Survey navigation status. Full-resolution inspection and independent
  OCR confirm the saved text and controls are readable; reduced previews were
  not reliable evidence of missing glyphs. Hardware-controller and manual-flight gates stay open.
- Moving-flight measurement now records whole-process RSS, verified cache timing/
  size, finite counters and a timestamp-bounded GPU window. The X11 run at
  `Builds/evidence/20260907T000654801208Z-hierarchy/` accounted for all 3,586 frame
  identities, but only 3,539 had valid GPU durations. CPU p95 8.362 ms, maximum
  ordinary frame 14.154 ms, zero managed allocation. Native Wayland at
  `Builds/evidence/20260907T000940482933Z-hierarchy/` improved to 3,590/3,601 GPU
  durations, CPU p95 8.335 ms and maximum 10.237 ms, still **failed**.
  Missing GPU durations remain failures. Renderer queue configuration is being
  investigated; no 600-second qualification follows these failed diagnostics.
- Identical duplicate overview nodes collapse to one resident identity. Conflicting
  route duplicates are rejected both within a page and across continuation pages
  before cache mutation. Three new source tests passed in the validation above.
- Release demo create/restart integration **passed** at
  `Builds/evidence/20260906T233146881082Z-demo-journey/summary.json`, built from
  `Builds/build-logs/20260906T233024Z/BuildLinuxRelease.json`. Six actual held scans
  completed four contracts, scanner level 3, the authored constellation/discovery
  and applied/restored the cosmetic. HUD local save, queue request, cancellation
  and confirmation passed. A separate process restored exactly the same v2
  profile bytes and all v1 sentinel bytes remained unchanged. This run used
  fixture range placement and direct initial button focus; real hardware, flight
  modes and broad controller traversal remain open. Both screenshots were retained.
  Earlier failed fixture-placement runs remain under the 232202 and 232421
  demo-journey evidence directories; no failed evidence was replaced.
- Screenshot review exposed stale "choose a starting system" navigation status
  after the HUD start action. Session-owned successful Survey start now advances
  navigation only after persistence succeeds. The failure/retry regression and
  D-pad traversal version of the player journey passed in the evidence above.
- Fresh public hierarchy smoke failed after a valid overview and bounded-community
  rejection, retaining the view and returning nonzero. Evidence:
  `Builds/evidence/20260906T223605226440Z-hierarchy/summary.json` and player log.
- Isolated Xvfb display `:93`, development player, 1280x720: demo startup,
  Actions menu, Survey system choice, and technology Survey start were exercised.
  Screenshots `/tmp/clustr-beta-demo.png`, `clustr-beta-menu.png`,
  `clustr-beta-survey.png`, `clustr-beta-survey-started.png`; player log
  `/tmp/clustr-beta-demo.log`. This is not the full player/restart journey.
- Live overview loaded; community response at revision 205/catalog 14 omitted
  `scope`, `root_id`, `truncated`, `next_cursor`. Strict validation rejected it
  and retained the overview. Raw response `/tmp/clustr-beta-community.json`,
  screenshot `/tmp/clustr-beta-live.png`, player log `/tmp/clustr-beta-live.log`.
- Ubuntu 24.04 image ran exact Unity 6000.4.10f1 against a separate source tree
  without Library. Exit 198 before compile: no valid Unity Editor license.
  `/tmp/clustr-ubuntu-project-20260906/ubuntu-build.log`. No Ubuntu build pass.
- Isolated headless Weston now renders on Radeon RX 7900 XT; Vulkan player logs
  confirm RADV NAVI31. Evidence: `Builds/shutdown-diagnostics/weston.log` and
  `Builds/evidence/current-hud-1440p/player.log`. Hardware rendering is available;
  memory and allocation probes work; full GPU timing coverage and thresholds remain unproven.
  No controller joystick device was attached in the last `/dev/input/by-id` inventory.

Evidence copies and their SHA-256 manifest are retained under
`Builds/evidence/20260906/`; temporary original paths above identify their provenance.

## Behavior and gate mapping

| Commitment | Implementation/evidence | Gate state |
| --- | --- | --- |
| Compiling runtime and migrated HUD | Session bootstrap and retained uGUI compile; fresh suites above | Working-tree suites passed; clean checkout pending |
| Repeatable validation | scripts/validate.py, XML/log retention, nonzero timeout/errors | Both suites and process gates passed |
| Stable snapshot namespace | Reopen rejection test, explicit demo and v2 source | Core test passed |
| Async live/cache opening | SpatialSceneSnapshotSource and session async flow | Tested subset; lifecycle needs more coverage |
| Failed save/retry and intent replay | CoreArchitectureTests candidate commit and retry | Core tests passed |
| Save/restart awards, discoveries, queue | SurveyJourneyTests authored six-member journey and file reload | Core test passed; player journey pending |
| Explore independence | GalaxySession rejects Survey intents; Mastery guarded | More integration coverage needed |
| Nested outcome mutation | Unity outcome reentrancy guard; duplicate Start notification removed | Integration regression still needed |
| Shared scope ownership and streaming visibility | ResidentGalaxy scope dictionaries; core test | Passed subset |
| Selection/navigation ownership pin | Core reference owners wired to selection and navigation before scope release; regression coverage | Core tests passed; streamed player retention pending |
| Resident-change-set rendering | Core owns projection but loader/store/materializer still adapters with duplicate raw projection state | Migration incomplete |
| Full hierarchy and pagination failure matrix | SnapshotSourceTests covers offline, wrong root, escaped/repeated cursors; existing paging contracts | Coverage incomplete |
| Strict revision/catalog/scope/root | Bounded response rejection in source; live rejection observed | Implemented; real hierarchy blocked |
| Transient retry/cancellation/stale results | Async retry and generation checks retained | Exhaustive injected failure tests pending |
| Limits and honest completeness | Existing cumulative limit policy; status labels retained | Boundary tests need expansion |
| Verified v2 cache and interrupted replacement | Hash verification, content filenames, atomic index; regression test | Passed subset |
| Physical 10 GiB ceiling | Physical bytes include index, temporary and quarantine files; atomic replacement reservation, orphan recovery, protected-base and quota tests | Tests passed; full 10 GiB stress pending |
| v1 byte preservation | Binary sentinels unchanged through separate demo create/restart players; `20260907T000050941643Z-demo-journey` | Passed demo journey; final-source repeat pending |
| Addressables and fallback | Local catalog group, async provider, lifetime release, primitive fallback | Failure/teardown tests pending |
| Controller focus, back and scrolling | D-pad focus/confirm/back demo journey passed; Survey start now offers Return to Explore | Full flight and real hardware journey pending |
| Full Explore/Survey flight loop | Existing flight/scanning adapters reconnected; Survey startup observed | Full Linux journey pending |
| Thread HUD tree and teardown | Replies displayed in scrollable Actions; no physical reply creation intended | Player acceptance pending |
| Personal draft and saved selection | Core validation plus real-player save, queue request/cancel/confirm and unchanged-profile restart | Scripted demo passed; real controller and final-source repeat pending |
| Provenance and loaded search | Demo/source labels, identified-only Survey filter, local queue notice | Source metrics UX still incomplete |
| Allocation hotspots | Registry/map/starfield buffers and retained UI; moving stress preflights measured zero ordinary-flight managed allocation | 30-second evidence passed this metric; 600-second and origin-shift coverage pending |
| Deterministic orbits/pooling/origin | Existing behavior preserved; stable asset hash added | Cross-effects/pooling tests incomplete |
| 720p/1080p/1440p legibility | 720p demo and 1440p live observed | Actions status-panel regression repaired and captured at all resolutions; full control matrix pending |
| Strict qualification report | Negative counter/window/threshold cases plus immutable report and failed-write/pointer tests | Automated coverage expanded; same-artifact full qualification pending |
| Ordinary-flight/loading intervals | Preallocated timestamp window and tagged moving-flight samples in X11/Wayland preflights | Short-run evidence recorded; full qualification pending |
| Deterministic near-limit stress | Loopback fixture and real player navigation reached every cumulative scope cap | Functional smoke passed; performance unqualified |
| Warm cache timing/cache usage | Offline verified-source timing and physical cache bytes recorded in moving preflights | Implemented source measurement; process-to-rendered-overview journey pending |
| 600-second reference performance | No valid stress/live reference runs | Blocked and uncompleted |
| Release-configured telemetry | Release build entrypoint exists | Exact artifact qualification pending |
| Arch clean-checkout gates | Working-tree validation/build exits repaired with isolated audio | Clean-checkout evidence pending |
| Ubuntu clean build/player smoke | Docker configuration; license failure | Blocked on licensed environment |
| Licensed Gitea CI | Workflow and runner requirements checked in | Activation/result unverified |
| Final packaging and notices | No qualified release package | Pending |

An earlier development player had an Actions status-panel defect, preserved at
`Builds/evidence/20260906/current-actions-status-defect-1440p.png`. The later
three-resolution menu checks above verified its repair. Startup screenshots alone
do not prove a full retained-UI journey. The warning
inventory is `Builds/evidence/20260906/third-party-warning-inventory.json`.

## Continuation order

1. Native shutdown is resolved through owned private audio with actual process-exit
   verification. Preserve FMOD backtrace and historical failed runs.
2. Finish resident change-set rendering, navigation/selection ownership and scope
   release; harden the cache's physical byte ceiling and asset teardown/fallback.
3. Complete fixture hierarchy/failure matrix, controller integration and full
   deterministic player/restart/v1-sentinel journey, with UI metrics and allocation fixes.
4. Complete stress and loading-interval qualification harness, warm-cache timing,
   invalid counter/report-write tests. Obtain attributable Radeon GPU measurements.
5. Re-run public hierarchy once provider supplies the required contract metadata;
   backend changes are outside authorization. Do not fill missing provider fields.
6. Supply licensed Ubuntu/CI environment and a real controller; finish host and
   hardware gates, then clean-checkout tests/builds at one source revision.
7. Produce local release package, notices, checksums, manifest and all-passed
   acceptance record. No public publishing.

No unchecked historical plan is reconciled as complete by this implementation.

## Accepted exploration expansion

All twelve features are now mandatory beta scope. See [catalogue](exploration-features.md) and [roadmap](superpowers/plans/2026-09-06-exploration-expansion.md). Existing failed and incomplete gates remain open.

| ID | Feature | Implementation | Automated evidence | Player acceptance |
|---|---|---|---|---|
| F01 | Signal tuning | Implemented | `ActivityTests`, scanner PlayMode; 127/12 checkpoint | 720p scripted create/restart passed; full matrix pending |
| F02 | Curiosity trails | Proposed | Pending | Pending |
| F03 | Scenic route flight | Three profiles, contextual starts and guidance implemented | Deterministic geometry, published eligibility, ownership and navigation handoff | 720p three-profile fixture traversal passed; normal flight/live acceptance pending |
| F04 | Orbital playgrounds | Evaluators, preview/start/cancel, timers and durable records implemented | Gate/orbit rules, preview isolation, record failure/retry and mode separation | 720p actual autopilot preview + fixture scored runs/restart passed; automatic best ghosts and manual acceptance pending |
| F05 | Discoveries as instruments | Partial implementation | Stable mapping and preference tests | Audio/mute/voice-cap and performance acceptance pending |
| F06 | Expressive personal constellations | Implemented | Design persistence and studio navigation regression | Scripted creation/music/postcard/restart passed at 720p, 1080p and 1440p; real controller and full accessibility matrix pending |
| F07 | Authored anomalies | Proposed | Pending | Pending |
| F08 | Probe observatory | Proposed | Pending | Pending |
| F09 | Mood-based expeditions | Proposed | Pending | Pending |
| F10 | Connection mysteries | Proposed | Pending | Pending |
| F11 | World celebrations | Implemented in part | Committed-only cues; three-slot cap and event deduplication | Startup/journey passed after retained-shader correction; full visual/performance acceptance pending |
| F12 | Journey diary and sharing | Recording and package foundation implemented | JourneyReplayTests: cadence, allocation, bounds, interpolation, compatibility, malformed input, durable save | 720p explicit save/restart passed; viewing, import UI, sharing and deletion unfinished |

### Expansion foundation evidence

- `Builds/validation/20260907T015153143278Z/summary.json`: 124 EditMode and 11 PlayMode tests passed, no project compiler diagnostics, actual exit 0.
- `ActivityTests` covers failed save/retry, intent replay, nested listener rejection, wrong profile/snapshot/mode, ordered clues, all three mystery interpretations, live-fiction prohibition, 200-entry diary retention, companion round trip, preserved existing Survey/v1 sentinels, corrupt-file preservation, immutable records and accepted content inventory.
- `ActivityReferencesRespectOtherOwnersAndReleaseOnModeChange` covers session-owned residency and mode isolation.
- The definition catalogue contains stable identities and observation keys, not yet complete story text, placements, visuals or player interactions. No F01–F12 feature is marked complete from this foundation alone.

### First expansion interaction checkpoint

- Foundation release build: `Builds/build-logs/20260907T015247Z/BuildLinuxRelease.json`.
- Foundation 720p demo/restart: `Builds/evidence/20260907T015408517941Z-demo-journey/summary.json`, both phases pass; v1 and saved-profile hashes preserved.
- Tuning/music validation: `Builds/validation/20260907T020408361539Z/summary.json`, 127 EditMode + 12 PlayMode, no compiler diagnostics. F01 and F05 runtime work is implemented in part; strengthened player journey is pending.
- Phase two is not complete: constellation layout/postcard editing and the full three-slot celebration system remain pending.

### Studio integration checkpoint

- `Builds/validation/20260907T021548383493Z/summary.json`: 129 EditMode + 12 PlayMode, no project compiler diagnostics.
- Tuning/music release `Builds/build-logs/20260907T020506Z/BuildLinuxRelease.json` passed the strengthened 720p scan/journal/preference create/restart journey at `Builds/evidence/20260907T020643341980Z-demo-journey/summary.json`.
- Studio release `Builds/build-logs/20260907T021815Z/BuildLinuxRelease.json` built, but `Builds/evidence/20260907T021930773633Z-demo-journey/summary.json` failed controller focus before export. No studio acceptance is claimed from that run. The failed evidence remains preserved.

### Celebration and integration failures retained

- `Builds/validation/20260907T022717065717Z/summary.json`: 130 EditMode + 12 PlayMode pass, including committed-only celebration cues and three-slot deduplication.
- Studio focus still failed at `Builds/evidence/20260907T022401614321Z-demo-journey/summary.json`; the added release interval did not fix it. Runtime navigation-link diagnostics were added.
- `Builds/evidence/20260907T023306031530Z-demo-journey/summary.json` failed earlier at startup because celebration shader lookup returned null in the stripped release player. The implementation now uses the game's retained shader fallback and guards incomplete effect initialization. Reverification is required; failed evidence remains historical.

### Studio navigation cause and correction

The diagnostic run `Builds/evidence/20260907T023621861535Z-demo-journey/summary.json` restored demo startup and showed that moving the right stick had already moved UI focus from Member 1 to SAVE DESIGN. Unity's installed default UI map binds both sticks (and WASD) to navigation. StudioNavigationScope now temporarily binds focus to D-pad/left stick/arrows, leaving right stick/WASD for node editing, then restores the previous action reference on close. The owned action asset/reference is released with the scope. `StudioNavigationReservesEditingStickAndRestoresPreviousBindings` passes in `Builds/validation/20260907T023828088287Z/summary.json` (130 EditMode + 13 PlayMode). Real-player reverification remains pending.

### Studio player verification and recording foundation

- Release `Builds/build-logs/20260907T024026Z/BuildLinuxRelease.json` passed create/restart at 1280×720 (`Builds/evidence/20260907T024210874134Z-demo-journey/summary.json`), 1920×1080 (`Builds/evidence/20260907T024607884380Z-demo-journey/summary.json`) and 2560×1440 (`Builds/evidence/20260907T024702780606Z-demo-journey/summary.json`). All use virtual controllers and fixture placement for scan range, not a real flight/hardware acceptance claim.
- Each run checks committed clean scans, contracts, authored discoveries, constellation editing through controller focus, musical playback with at most eight active voices, an explicit 1920×1080 PNG export, unchanged postcard/profile hashes after restart and byte-identical v1 sentinels. The 720p studio and actual postcard were visually inspected; layout, name, reason, member legend and provenance are present.
- `Builds/validation/20260907T025046880408Z/summary.json`: 140 EditMode and 13 PlayMode pass, actual exits 0, no project compiler diagnostics. Replay tests include a measured zero-allocation core recording loop at a simulated 144 Hz input cadence, exact 10 Hz samples, 9,000-sample/900-second bounds, gap handling, double-coordinate interpolation, strict file parsing and atomic non-overwriting save. This is not a Unity player performance qualification.
- The recorder is session-owned and exposed through explicit Actions saving. No disk writes occur merely because recording is active. Mode changes clear its transient buffer; permanent files remain separate. The file codec has no session/provider/progression references. Player save/restart verification is pending for this new tranche.
- Full phase-two acceptance remains open: the expanded active-feature performance run, manual signal comfort, all palettes/member counts and real-controller checks have not passed. F03/F04 courses and ghosts, full authored campaign, observatory, expeditions, replay browsing/import UI/deletion and remaining F12 interactions are unfinished.

### Recorder player evidence and validation shutdown failure

- Release `Builds/build-logs/20260907T025314Z/BuildLinuxRelease.json` passed the strengthened 720p player journey at `Builds/evidence/20260907T025433667795Z-demo-journey/summary.json`: explicit rolling-replay save, parse/interpolation after restart, unchanged replay SHA-256 and Survey state, plus the preceding scan/studio/postcard checks.
- `Builds/validation/20260907T025455868678Z/summary.json` **failed overall**. EditMode XML reports 140 passing tests and no compiler diagnostics, but Unity exited 255 during native shutdown. The log records SIGSEGV in the HIDInput thread (`PLATFORM_hid_enumerate`) while the main thread waits in `ShutdownControllerHIDAPI`, followed by a Mono reporting assertion/double fault. PlayMode passed 13 tests and exited normally. No systemd core or contemporaneous kernel OOM entry was found; 38 GiB memory was available at inspection. The engine-shutdown location is established; its trigger is not. This failed run remains preserved and does not count as a passing gate.

- Repeat validation `Builds/validation/20260907T025657496400Z/summary.json` passed all 140 EditMode and 13 PlayMode tests with exit 0 and no project compiler diagnostics, on the unchanged runtime source. The previous HID shutdown crash is not erased or considered explained by this repeat.

### Matching working-tree player builds

`Builds/build-logs/20260907T025809Z/BuildLinuxBeta.json` records a successful development build under `Builds/Linux`; `Builds/build-logs/20260907T025314Z/BuildLinuxRelease.json` records the release-configured build under `Builds/LinuxRelease`. Both exited 0 and contain per-file SHA-256 manifests. Both identify `775f826f51ee70d8f25e3e421887eaa5a6f10801-working`, not a clean qualified revision. The development artifact has not yet repeated the expanded player matrix. No final distributable package is claimed.

### Recorder ordinary-flight preflight (not qualified)

`Builds/evidence/20260907T030012920953Z-hierarchy/summary.json` preserves a failed 30-second release-artifact stress preflight. Hierarchy smoke passed every client scope cap; sampling recorded 14,053 resident nodes and 57,000 routes across retained scopes at 2560×1440, Vulkan/Wayland. Across 3,601 ordinary-flight frames, CPU p95 was 8.335 ms, maximum frame time 8.995 ms and managed allocation zero. Peak player RSS was 1,502,683,136 bytes; requested GPU buffers 408,453,120 bytes. Cache used 14,915,595 bytes. The verified cached-source open took 0.0126 seconds; this is not process-to-rendered-overview timing.

The process exited 2 and the report failed: working-tree build provenance, incomplete GPU capture window, and only 3,599 valid GPU timings out of 3,601. Report: `Builds/evidence/20260907T030012920953Z-hierarchy/config/unity3d/Subcult/Clustr Galaxy/ClustrGalaxy/reports/20260907T030108245Z-fc4dd066ead740bf89a210fc1ddfa3c6.json`. No counters or stalls were discarded to pass. Ambient music, visible ghosts, course guidance and active celebrations were not enabled in this scenario; their combined performance scenario remains required, as do both 600-second release qualification runs.

## Flight activity implementation tranche

The next tranche adds three deterministic scenic profiles, the eight-gate ring run, figure-eight checkpoints and the continuous 20-second stable-orbit evaluator. The session owns the run, validates published-route eligibility/Survey visibility, holds activity residency references, cancels on mode changes and releases ownership on completion/cancellation. Starting a course clears previous plotted/queued guidance; explicit normal navigation cancels course guidance. Autopilot previews are unscored. Assist/Mastery records are separate companion entries and use persist-before-commit with explicit retry. The effective flight controls/physics step contribute to the recording tuning identity.

Retained Actions expose starts, previews, cancellation, record saving and local replay viewing. The renderer displays the authored path and next gate, with an elapsed-time display. Completed manual recordings can be saved explicitly and watched in a top-down modal viewer which suppresses local input and never changes Survey or resident data. This is not yet automatic personal-best ghost playback during practice, nor the final observatory integration.

`Builds/validation/20260907T031907336024Z/summary.json` and `Builds/validation/20260907T032330809255Z/summary.json` pass 146 EditMode and 13 PlayMode tests, with no project compiler diagnostics. Further handoff/retry regressions and the extended Linux player journey are being added. Full F03/F04 acceptance remains open.

### First extended flight player journey

`Builds/build-logs/20260907T032507Z/BuildLinuxRelease.json` produced the release player exercised by `Builds/evidence/20260907T032721998389Z-demo-journey/summary.json` at 1280×720 with `--flight-activities`. Creation and restart passed. The ring preview uses actual autopilot movement and produces no record. Scored ring/figure-eight/stable-orbit completion uses explicitly labelled pose fixture placement to verify evaluators, persistence and restart, not to claim manual flight feel. Three local best records restored; the completed orbit recording saved and reopened in the top-down viewer. Input was suspended/restored and Survey state was unchanged by playback. The replay screen was visually inspected at 720p. Three-profile scenic player traversal and other resolutions remain pending.

`Builds/validation/20260907T032723302953Z/summary.json` remains a failed run: all 149 EditMode assertions passed but the editor exited -11 during shutdown while a player journey was running. PlayMode passed with exit 0. A serialized repeat is being run; concurrent execution is a correlation, not an established cause.

The serialized repeat `Builds/validation/20260907T033014846211Z/summary.json` passed 149 EditMode and 13 PlayMode tests with process exits 0 and no project compiler diagnostics. It includes failed-flight-save retry, unauthorized result rejection, separate flight records, published-route ownership and clearing previous navigation guidance. This repeat does not erase the native shutdown failure.

### Complete first flight integration journey

Release build `Builds/build-logs/20260907T033144Z/BuildLinuxRelease.json` passed create/restart at `Builds/evidence/20260907T033323741652Z-demo-journey/summary.json` using `python3 scripts/demo_journey.py --resolution 1280x720 --flight-activities`. This adds contextual selection/start and fixture checkpoint traversal for all three scenic profiles, followed by actual autopilot ring preview, fixture-scored ring/figure-eight/stable-orbit runs, durable records, explicit flight-recording save and viewer restart. Preview/scenic runs produce no scored records. The stable-orbit record lasted 20.12 seconds in the fixture. Imported files are not exercised by this journey; there is still no import-selection UI.

The new release remains a working-tree artifact, not a clean qualified revision. Other resolutions for the expanded flight journey, real controller/manual comfort, live scenic compatibility and active-course performance remain pending. The previous development artifact predates this flight tranche. Automatic personal-best ghost restoration during a new practice run, authored two-body playground presentation and complete observatory integration remain unfinished. No F03/F04/F12 completion or release qualification is claimed solely from this integration checkpoint.

## Personal-best ghost tranche

Implementation adds an explicit `SAVE PERSONAL-BEST GHOSTS` preference (off for existing/new profiles), an optional full-run recording embedded in each best flight record, course-anchor-relative replay positioning, and one pooled practice ghost with hide/show controls. Scores and recordings now share the activity document's atomic write; a failed replacement cannot publish a newer score with an older recording. Records exceeding the 15-minute recording window retain their score without falsely offering a full competitive ghost. Existing records without ghost fields remain valid.

Compatibility checks require matching snapshot, world mode, authored content/course version, flight mode and tuning. The ghost is attached once at practice start; per-frame positioning does not allocate or dispatch gameplay outcomes. Gravity-mode changes cancel active comparable practice. Pooled decorative training bodies make the figure-eight lobes and stable-orbit centre visible without creating source entities or adding physical forces. Restart, origin-shift and rendering verification are in progress.

`Builds/validation/20260907T152532402016Z/summary.json` passed 155 EditMode and 13 PlayMode tests with clean exits; additional atomic-failure/legacy-file cases have since been added and await execution.

### Atomic ghost persistence verification

`Builds/validation/20260907T153328945623Z/summary.json` failed compilation because the new legacy-file test referenced a JSON dependency unavailable to the test assembly. The test now removes optional fields from its serialized fixture without adding a package dependency. The failed run is preserved.

`Builds/validation/20260907T153444776949Z/summary.json` passes **158 EditMode and 13 PlayMode tests**, with both processes exiting 0. Coverage includes failed score/ghost replacement and retry, legacy saves without ghost fields, explicit opt-in, incompatible identities, large-coordinate course translation, initial recording samples at a 0.25-second step, and zero allocation in 10,000 core playback samples. Five Python harness tests and `git diff --check` also pass. Core playback allocation evidence is not a complete active-feature player performance qualification.

The first ghost-enabled release (`Builds/build-logs/20260907T152758Z/BuildLinuxRelease.json`) passed create/restart at 720p in `Builds/evidence/20260907T152945590665Z-demo-journey/summary.json`. It exercises compatible personal-best restoration, hide/show, origin rebasing, and abandonment without saved-state changes, alongside the previous scenic/playground/studio/export journey. Profile, postcard, replay and v1 sentinel hashes are preserved across restart. Fixture placement and virtual-controller automation remain explicitly labelled; this is not real-controller/manual-flight acceptance. A subsequent release rebuild includes the final initial-sample, distinct gold ghost material, and fixed-step cancellation refinements.

### Latest release ghost journey — 1440p

`Builds/build-logs/20260907T153632Z/BuildLinuxRelease.json` is the successful updated release build, with per-file checksums and explicit `qualified: false` working-tree provenance. `Builds/evidence/20260907T153758335647Z-demo-journey/summary.json` passes both create and restart at **2560×1440**, including compatible ghost restoration, hide/show and floating-origin alignment, three scenic profiles, the actual autopilot preview, fixture-scored playgrounds, explicit recording/viewer actions, and prior studio/export checks. Profile, postcard, replay and v1 sentinel hashes remain unchanged across restart. The studio screenshot was inspected and its essential controls/hints fit at this resolution. The flight viewer screenshot was also inspected; this does not establish every screen's visual acceptance.

The 720p ghost journey used the preceding build; the latest refinements have 1440p player evidence. A matching latest-build 720p/1080p matrix, real-controller/manual comfort evaluation, active-feature performance, final development artifact, clean-revision qualification, Ubuntu licence/build/runtime verification, public provider compatibility and licensed CI activation remain open. No full F03/F04/F12 or expanded-beta acceptance is claimed.

## Authored campaign implementation

The next tranche supplies 28 authored observations (12 trail clues, six anomaly discoveries, ten mystery observations), with explicit demo anchors, prose, hints, three named keepsakes and the six supported mystery interpretations. Observation timing lives in the session's plain-core campaign service; only successful companion commits reveal journal entries. Failed persistence exposes explicit retry. Live snapshots do not trigger fiction, and the Unity adapter requires identified Survey anchors before observing or showing anomaly visuals. A pinned trail retains its eligible next anchor and releases that reference when progress or mode changes.

A retained campaign notebook presents gathered observations, optional written interpretations, and controller confirm/back navigation. Live mode offers personal notes without fictional cases. Six bounded primitive anomaly visuals are authored presentation objects, not source entities. The complete authored assets/behaviors, evidence-constellation assembly, trail pacing and manual campaign acceptance remain open.

Initial validation `Builds/validation/20260907T154835279326Z/summary.json` passed both Unity suites; subsequent allocation and player-journey coverage is being verified. `scripts/demo_journey.py --campaign` now exercises actual timed discovery with explicitly labelled anchor placement, notebook interpretation controls, no Survey progression changes, and restart restoration. This section does not yet claim that player journey passed.

### Campaign and observatory verification in progress

`Builds/validation/20260907T155055512733Z/summary.json` passes 164 EditMode and 13 PlayMode tests with no compiler diagnostics. Campaign tests exercise all 28 observation IDs, ordered trail reachability, complete/restart restoration, hidden/range/aim rejection, interrupted observation, save failure/retry, live-fiction prohibition and allocation-free ordinary-flight checks.

The first 720p campaign player run (`Builds/evidence/20260907T155328018751Z-demo-journey/summary.json`) failed because the existing HUD test helper closed the notebook before trying its Next button. A separate modal controller helper now keeps the notebook open. This failed run is preserved and is not player acceptance.

Observatory work adds six bounded companion display slots, canonical copied captions, explicit persist-before-commit placement, a local RenderTexture diorama, constellation playback and saved-flight replay access. A pinned journal caption survives rolling-diary eviction. Validation `Builds/validation/20260907T155752083298Z/summary.json` failed on an enumerable/list mismatch in the music adapter; the adapter now materializes its node list only on explicit playback. Repeat validation and the combined campaign/observatory player journey are in progress. No F08 completion is claimed yet.

### Campaign and observatory 720p integration checkpoint

`Builds/validation/20260907T155833534526Z/summary.json` passes **167 EditMode and 13 PlayMode tests**, both exit 0. Release `Builds/build-logs/20260907T160029Z/BuildLinuxRelease.json` passes create and restart at `Builds/evidence/20260907T160200214442Z-demo-journey/summary.json` with `--campaign` at 1280×720. The player discovers all 28 observations through the real three-second timing evaluator using explicit fixture placement, submits both interpretations through modal controller navigation, saves an observatory item, and restores all eleven completed activities plus the display. Survey progression remains unchanged by the campaign/observatory operations; restart preserves profile/export/replay/v1 hashes. The observatory screenshot was inspected: the six-slot primitive diorama renders and essential controls fit. Full authored artwork, manual pacing, evidence-constellation assembly and broader player acceptance remain open.

### Expedition implementation underway

The session now selects bounded Quiet Drift and published-link Unexpected Connection trips from eligible resident data; no links or missing destinations are fabricated. Pilot Practice uses three committed manual course results. Companion state stores the plan, current stop and active/paused/ready/completed/abandoned status. Closing an otherwise complete trip is explicit and writes one diary card. Starting/resuming, visiting and record persistence retain the activity commit boundary; pending saves expose retry. Active destination references release on pause, completion, abandonment and mode changes. Missing restored stops remain unavailable rather than loading provider data implicitly. Automated and player expedition gates are still in progress.

`Builds/validation/20260907T160702569347Z/summary.json` passes **171 EditMode and 13 PlayMode tests** for the first expedition implementation. Deterministic selector tests cover shorter data, absent connections, exclusion of ineligible nodes, pause/reopen/resume, explicit closing, and ordered Pilot Practice records. Full Linux expedition journeys and pacing remain unverified.

Local creation library work adds listing and aggregate storage usage for explicitly saved replays/postcards, local-path replay import, standalone playback and separate request/confirm/cancel deletion controls. Import validates the entire strict bounded package before creating a destination file. Deletion is restricted to listed files in owned non-symlink creation directories, never v1. Imported competitive-ghost alignment, screenshot saving and broader diary/replay presentation remain open. Tests and player integration are in progress.

## Physical-controller feedback and revised ship controls

The host detects a Microsoft X-Box 360 pad at `/dev/input/event21` / `js0`.
Manual session `Builds/evidence/20260907T161614358573Z-manual-controller` launched
the release built at `20260907T161309Z` with separate evidence-owned storage.
The player reported that the controls felt clunky and should behave more like a
spaceship, with attitude control and a separate throttle/thrust button. This is
negative acceptance feedback, not a passed controller gate.

The player explicitly chose left-stick pitch/roll with trigger thrust. The next
revision maps right-stick yaw/vertical thrust and bumper sideways thrust, preserves
B braking and keyboard controls, adds a softer centre rotation curve and bounded
angular acceleration, and changes the replay tuning identity to `flight-v2`.
Older recordings remain viewable but cannot become competitive ghosts under the
new model. A controller-accessible quit/confirm/cancel flow is also added. Validation `Builds/validation/20260907T162648283836Z/summary.json` passes 178 EditMode and 14 PlayMode tests, with no compiler diagnostics. The new virtual-controller test proves that the left stick rotates without propulsion, the right trigger supplies forward thrust, and disabled flight input cannot drive the ship. Physical acceptance of the revised handling remains pending; the prior negative feedback is retained.

The revised release build passes at `Builds/build-logs/20260907T163120Z/BuildLinuxRelease.json`. Its basic 1280×720 Linux create/restart journey passes at `Builds/evidence/20260907T163239427172Z-demo-journey/summary.json`; five Python harness tests also pass. These results do not qualify the release or replace the pending physical handling assessment and expanded performance gates.

The player revised the requested layout after the flight-v2 demo: left vertical = vertical thrust, left horizontal = yaw, right vertical = pitch, right horizontal = roll. The physical bindings, HUD hints, README, controller checklist, and ordinary-flight profiling input now follow this layout; trigger propulsion and bumper strafing remain. The Unity controller regression exercises all four physical stick axes. Revised validation/build and player acceptance are pending.

Validation `Builds/validation/20260907T165300443728Z/summary.json` passes both suites (178 EditMode, 14 PlayMode) with no compiler diagnostics for the revised stick layout, including each of the four physical stick axes. Physical-controller acceptance remains pending.

Release build `Builds/build-logs/20260907T165446Z/BuildLinuxRelease.json` succeeds with the requested left yaw/lift and right pitch/roll mapping. A replacement visible controller demo was launched for assessment. This build is not release-qualified.

Player feedback on `20260907T180941924977Z-manual-controller`: revised stick layout felt good; acceleration, stopping and physics response felt too slow/soft. The session exited 0, recorded physical controller events, and had no runtime exception/crash markers. Layout is accepted, handling refinement remains open. Acceleration changes 48→96, braking 120→240, angular acceleration 360→720 degrees/sec². Neutral Pilot Assist uses braking strength; Mastery and gravity momentum behavior remain. Replay tuning identity is flight-v3 so previous handling ghosts cannot compete under new tuning. Validation and player reassessment pending.

`Builds/validation/20260907T191240581459Z/summary.json` passes both Unity suites for the firmer tuning, including the cruise-to-stop release test. Release build and hands-on reassessment follow.

The firmer-response release builds successfully at `Builds/build-logs/20260907T191433Z/BuildLinuxRelease.json`; basic 1280×720 create/restart journey passes at `Builds/evidence/20260907T191552167616Z-demo-journey/summary.json`. Controller was disconnected after the preceding session; new tuning awaits hands-on feedback.

## Approval of revised flight controls and tuning

The player explicitly approved the revised controls and flight-v3 tuning ("go ahead and approve"). The requested left-stick yaw/vertical thrust, right-stick pitch/roll, trigger propulsion, and firmer acceleration/braking are approved. The preceding physical session established that the stick layout felt good. The latest tuning passed automated validation and the basic Linux create/restart journey; no additional physical-controller run of flight-v3 was observed. This approval closes the requested controls/tuning decision, but does not establish full controller-journey coverage or waive outstanding beta qualification gates.

## Feature catalogue completion: mystery evidence boards

The accepted feature-only completion plan is `docs/superpowers/plans/2026-09-07-feature-catalogue-completion.md`. F10 now has a five-observation evidence-board companion with normalized layout and personal edges. New interpretations require all discovered cards and at least one connection, committed together with the board and explanation. Historical completed mysteries remain complete without a board; adding one does not award progress. No observation card becomes a resident source entity or personal Survey constellation. The retained editor reuses constellation drawing and input navigation, supports dragging/controller movement, explicit draft save, interpretation submission and return to the originating notebook.

Validation `Builds/validation/20260907T230508887229Z/summary.json` passes both Unity suites. Dedicated regressions cover missing connections, unknown/undiscovered cards, save failure/retry/idempotency, file roundtrip, legacy completion and live/wrong-snapshot rejection. The expanded Linux campaign journey is being verified. Asset/pacing and player acceptance work remain open.

Evidence-board release `Builds/build-logs/20260907T230712Z/BuildLinuxRelease.json` passes the full fixture-based campaign create/restart at `Builds/evidence/20260907T230950633689Z-demo-journey/summary.json`. The 720p evidence-board screenshot was inspected; essential controls and observation text fit. The player saves both boards through controller actions and restores them without changing Survey state or v1 sentinels.

Custom asset authoring produced ten original mesh/prefab assemblies with reusable enamel/brass/glass materials and editable generator source. Six anomaly animations are integrated through the local catalogue; keepsake and platform integration remain ahead. `Builds/validation/20260907T231534082087Z/summary.json` failed the zero-warning gate on the preview runner obsolete Unity API, now corrected. Repeat `Builds/validation/20260907T231749648730Z/summary.json` passes both suites, including reduced-motion and allocation tests for all six anomaly animation types. Visual preview and final campaign repeat are in progress.

Asset-enabled release `Builds/build-logs/20260907T231929Z/BuildLinuxRelease.json` passes campaign create/restart at `Builds/evidence/20260907T232113953079Z-demo-journey/summary.json`. The first asset preview at `Builds/evidence/20260907T232053531867Z-art-preview/summary.json` passes rendering and was visually inspected. Amber material intensity and preview spacing were then refined. Final validation `Builds/validation/20260907T232422370019Z/summary.json` passes both suites, adding actual legacy-file-without-board compatibility. A final rebuild and controller-movement journey follow. Original art remains a reviewable first pass, not final player/art acceptance.

Final first-tranche release `Builds/build-logs/20260907T232643Z/BuildLinuxRelease.json` passes the extended controller-card-movement campaign create/restart at `Builds/evidence/20260907T232831738087Z-demo-journey/summary.json`. Refined asset preview `Builds/evidence/20260907T232807082487Z-art-preview/summary.json` passes rendering and was visually inspected. Art review is requested; no artistic or catalogue-wide acceptance is inferred.

The replay continuation corrects ordinary recordings to the actual frozen flight tuning identity and clears the rolling buffer when gravity/momentum mode changes. It adds a controller-seekable timeline, playback speed controls and child-view return to the originating library/observatory focus while keeping flight suspended. Verification is underway. Replay frame/package upgrades, screenshots and the rest of F12 remain open.

Screenshot completion work adds transient capture preview, explicit Save/Discard, paired image/metadata directory commit, bounded image reading, PNG viewing, storage accounting and safe bundle deletion. `Builds/validation/20260907T234306832391Z/summary.json` passes both suites for its first implementation. The first player run at `Builds/evidence/20260907T234757612968Z-demo-journey` failed because the HUD test helper searched a closed menu after image discard; the helper now opens through controller input before lookup.

Replay package version 2 carries an optional explicit course anchor and reads version-1 files as standalone. Imported current-playground ghosts are explicitly selected, compatibility-checked on course start, and cleared on mode/teardown without progress writes. Long truncated recordings omit competitive anchors. Arbitrary event/display-label payloads and scenic route geometry identity remain unfinished; no complete F12 claim.


### Catalogue completion continuation — creation, media and observatory

Validation `Builds/validation/20260907T235743119936Z/summary.json` passed 188 EditMode and 15 PlayMode tests with zero project compiler diagnostics. Release build `Builds/build-logs/20260908T000037Z/BuildLinuxRelease.json` passed the combined 720p campaign/flight create-and-restart journey at `Builds/evidence/20260908T000211973916Z-demo-journey/summary.json`. This covers evidence boards, screenshot Save/Discard and metadata, library child-view return, compatible imported practice ghosts, replay package restoration and byte-identical v1 sentinels. It is working-tree evidence, not clean-revision qualification or player acceptance.

The observatory continuation installs the custom platform and keepsakes, saved constellation nodes/edges/palettes, actual recorded flight paths and journal captions. Clear-slot persistence retains the saved original and is tested for failure/retry and restart. Replay displays do not connect loading gaps. Validation `20260908T001321817713Z` passed during this work; an accidentally overlapping validation `20260908T001455581854Z` was rejected by Unity's project lock and is preserved as failed. A fresh serialized run and rebuilt player are required for the finished changes.


Observatory verification: `Builds/validation/20260908T001705480225Z/summary.json` passed **189 EditMode / 15 PlayMode** with no compiler diagnostics. Release build `Builds/build-logs/20260908T001843Z/BuildLinuxRelease.json` passed the combined 720p campaign/flight create-and-restart journey at `Builds/evidence/20260908T002007822261Z-demo-journey/summary.json`. All six displays restored, all four item kinds were exercised, clear/re-place persisted and displayed replay Back restored observatory focus while flight remained suspended. Screenshot inspection prompted larger framing and higher exhibit placement; these adjustments and diary/postcard work require the next artifact's evidence.

Diary continuation adds controller browsing, bounded text pages, explicit retryable bookmark saving, observatory child-view return and 1920×1080 diary/replay postcards with local/snapshot provenance. The creation library retains the selected file when a child export changes sort order. Full replay event/display-label package support and full feature acceptance remain open.


Diary/media verification: `Builds/validation/20260908T002451622755Z/summary.json` passed 189 EditMode / 15 PlayMode; `Builds/build-logs/20260908T002641Z/BuildLinuxRelease.json` produced the release-configured artifact for `Builds/evidence/20260908T002800003306Z-demo-journey/summary.json`. Combined campaign, flight, observatory, diary, screenshot and replay create/restart passed at 1280×720. All three 1920×1080 postcards, screenshot/metadata, replay packages, v2 profiles and v1 sentinels were preserved. Diary, postcard and six-slot screenshots were visually inspected. An internal flight tuning hash exposed in the observatory caption is now replaced with a player-facing course/mode/time label.

Log review found Unity cloud analytics initialization attempts despite analytics being disabled. `UnityConnectSettings` now disables service enablement and automatic Analytics/Ads startup without package changes; local qualification telemetry is retained. Fresh artifact startup verification is required before claiming the attempts are eliminated.


Final checkpoint for this continuation: `Builds/validation/20260908T003343155154Z/summary.json` passes **189 EditMode / 15 PlayMode**, zero project compiler diagnostics. Build `Builds/build-logs/20260908T003606Z/BuildLinuxRelease.json` passed the complete combined journey at **2560×1440**, create and restart, in `Builds/evidence/20260908T003728861248Z-demo-journey/summary.json`. Three postcards have verified 1920×1080 dimensions; profiles, images plus metadata, replay packages and v1 sentinels survive restart unchanged. Both logs contain no runtime exceptions or Unity cloud analytics endpoint mentions after service startup was disabled. This log observation is not a packet-level network audit. The preceding 720p artifact's equivalent journey is also preserved. Five Python harness tests and `git diff --check` pass.

These remain working-tree artifacts rooted at `775f826f51ee70d8f25e3e421887eaa5a6f10801`; no clean-source or release qualification claim is made. Artifact tree SHA-256: `36db1a91f0cddf9fd1d92391cb64c955c55e920f9761469a4adfc92fd530d19a`. Normal-play campaign/flight pacing, expeditions, complete live notebook tools, replay event payloads and permanent bookmark retention, accessibility/active-feature performance and manual/controller/art acceptance remain open in the feature-completion plan.


### Permanent bookmarks, replay annotations and expedition restart continuation

Permanent bookmarks are now distinct from the latest 200 automatic diary entries. Explicit Save and Keep Selected Moment retain entries until user removal; removal has request/confirmation/cancellation in the diary. Failed persistence preserves state and permits retry. Older v2 entries with the prior explicit `bookmark:` identity restore as retained; old automatic notes retain their previous meaning. The former expiry behavior is no longer applied to newly saved bookmarks. `PermanentBookmarkTests` covers eviction, legacy restoration, retry and intent replay.

Replay format 3 adds at most 256 bounded annotations with safe plain labels and explicit kinds. Versions 1/2 remain readable. The recorder subscribes to committed activity outcomes; imported annotations only display in playback. Codec and buffer tests cover malformed labels/kinds/timing/counts and rolling-window boundaries. Validation `Builds/validation/20260908T011922414268Z/summary.json` passed after these core changes.

The Linux journey now optionally uses three processes (`--expeditions` with campaign and flight flags): create and pause Quiet Drift, restart and resume/complete all three moods, then restart again to verify restoration. The middle process explicitly allows activity companions to change while requiring Survey progress to remain byte-identical. Player verification is pending; fixture travel proves transitions, not fifteen-minute pacing or normal-flight enjoyment.


Expedition validation preserved a useful failed run: `Builds/evidence/20260908T012459667984Z-demo-journey` passed creation but failed the resume harness's assumption that a Survey route had two identified endpoints. The application correctly kept Unexpected Connection unavailable. The harness now explicitly verifies that rejection, then exercises connections in Explore without granting Survey progression.

`Builds/evidence/20260908T013412040716Z-demo-journey/summary.json` passed all three launches at 720p using build `Builds/build-logs/20260908T013247Z/BuildLinuxRelease.json`: create/pause, resume/complete Quiet Drift plus both Explore moods, and final restore. It also exercises the live-notebook UI against an explicitly labelled provider fixture, including more than eleven saved notes, duplicate-save prevention, and diary child return. Permanent bookmarks and committed scan replay annotations survive the standard save/restart checks. These are deterministic functional journeys, not live-provider compatibility or normal-flight pacing evidence.

The next presentation continuation groups Actions with plain-core section policy, retains an All Actions view, and preserves global resume/quit controls. Observatory music now uses the displayed design's saved preset and offers play/pause. Demo anomalies add nearby artistic notes within the existing eight-voice pool and matching bounded emission cues; mute retains visual feedback and reduced motion keeps motion frozen. Validation and player verification for this presentation continuation are in progress.


Presentation validation `Builds/validation/20260908T014330065562Z/summary.json` passed 198 EditMode / 16 PlayMode tests with no compiler diagnostics. The first release build at `Builds/build-logs/20260908T014501Z` failed in Unity native code: systemd recorded editor SIGABRT, and the saved backtrace identifies `SystemNative_FcntlSetCloseOnExec` with an invalid descriptor value `140193390808160`. The process descriptor limit was 524288; memory availability was approximately 32 GiB and no corresponding kernel OOM event was found. This points to invalid native handle state, not ordinary resource exhaustion; the root cause inside Unity is not established. `native-backtrace.txt` preserves the diagnostic evidence; the temporary extracted core was removed. A fresh unchanged-source build is being retried. No engine upgrade or host configuration change was made.


### Runtime diagnostic audit and active-feature qualification continuation

The unchanged-source retry `Builds/build-logs/20260908T014751Z/BuildLinuxRelease.json` succeeded after the native editor failure. The three-launch 720p journey `Builds/evidence/20260908T014911792776Z-demo-journey` passed its functional assertions. Celebration tracking/color/tracing refinements then passed validation `Builds/validation/20260908T015540042039Z/summary.json` and built at `Builds/build-logs/20260908T020132Z/BuildLinuxRelease.json`.

A subsequent log audit found `Script error (ExpeditionController): Start() can not take parameters.` The old harness only rejected exception markers and did not catch Unity message-signature errors. Therefore prior functional PASS results must not be treated as zero-runtime-error acceptance. `Builds/evidence/20260908T020306163908Z-demo-journey/runtime-error-audit.json` explicitly rejects that run despite its three passing journey assertions. The helper is renamed `StartExpedition`; all main player harnesses now reject script errors, assertions and exception markers. Historical reports remain intact.

The combined expedition report also now expects three launches when `--expeditions` is enabled, rather than incorrectly requiring two. The stricter runtime check and new active-feature/fresh-profile media journeys require a new artifact and evidence. Active-feature measurements must show actual course, visible ghost, audio and celebration workload; missing evidence fails without relaxing the original performance thresholds.


Active-feature stress preflight `Builds/evidence/20260908T021404488820Z-hierarchy` used release build `20260908T021235Z` and sampled 30.003 seconds at 2560×1440 with 14,053 resident nodes and 57,000 routes. All 3,511 workload frames had active guidance and ghost rendering; the ghost was in view for 980 frames, audio played in 3,475 and celebrations in 2,341, with three simultaneous effects. CPU p95 was 8.89 ms, maximum non-loading stall 34.11 ms, player peak 1.32 GiB and GPU buffers 0.42 GiB. It correctly failed: working-tree provenance, incomplete GPU capture (3,402 valid / 3,511 frames) and an 804-byte allocation peak. Reported GPU p95 0.865 ms is based on incomplete samples and is not qualification evidence. No threshold was relaxed. Celebration event-history preallocation and a 600-event allocation regression are now being validated.

Fresh-profile import `Builds/evidence/20260908T021523053847Z-recipient-import` preserved the source replay and v1 sentinel and wrote no progression files, but failed the corrupt-image selection assumption, then exposed a shutdown callback exception. Selection now targets the actual file; parent library close detaches its panel before closing children, and child-return focus tolerates EventSystem teardown. A new player run is required.


Validation `Builds/validation/20260908T021654405475Z/summary.json` passes **209 EditMode / 16 PlayMode**, no compiler diagnostics. Release build `Builds/build-logs/20260908T021902Z/BuildLinuxRelease.json` succeeds. Its repeat stress/active-feature preflight at `Builds/evidence/20260908T022052914085Z-hierarchy` records **zero ordinary-flight managed allocation** across 3,597 frames, CPU p95 8.34 ms and maximum non-loading stall 12.18 ms. All frames had active guidance and ghost presentation, with 1,008 in-view ghost frames, 3,560 audio frames and 2,390 celebration frames. Player/GPU memory remain below limits. Qualification still fails on working-tree provenance and incomplete GPU timing (3,570 valid / 3,597 samples). The 30-second run cannot qualify a release.


Separate-profile player import passes at `Builds/evidence/20260908T022204303621Z-recipient-import/summary.json` using the same `20260908T021902Z` release artifact. The fresh `replay-recipient` profile imports and watches a Survey recording in Explore, rejects it as an incompatible practice ghost, handles malformed/missing imports and a corrupt PNG, and exits without runtime errors. No v2 progression files are created; source/import bytes match and the v1 sentinel remains byte-identical. This closes the deterministic separate-profile import/media-error journey, not human enjoyment or all-file-format acceptance.


The stricter complete 720p three-process journey passes at `Builds/evidence/20260908T022232420482Z-demo-journey/summary.json`, on artifact tree SHA-256 `aad63caf1a72a13670a952c50e1140fbcb65b88b7ae69142b1a3bb546ea31b5a`. Creation, expedition resume and final restart are all successful with no runtime exception/script-error markers. The 720p studio, evidence board and observatory screenshots were inspected for essential text/control fit. World-effect captures are retained for further visual/player acceptance, not treated as proof of artistic quality or distant legibility.

A bounded public manifest probe at `Builds/evidence/20260908T023056227038Z-provider-probe/summary.json` returned HTTP 403 Forbidden. This establishes the probe's current provider failure, not a successful live journey. No provider/backend changes or validation relaxation were made.


The same artifact passes all three launches at 1920×1080 in `Builds/evidence/20260908T022847296534Z-demo-journey/summary.json`. Diary controls and the observatory with all item types were visually inspected at this resolution. No runtime script errors/exceptions were reported. Final 1440p checks are in progress; hardware, pacing, accessibility coverage and artistic acceptance remain distinct open gates.


The combined multi-resolution outer command exited 143 during the 1440p final restart at `Builds/evidence/20260908T023503762282Z-demo-journey`. Create and resume had passed; the interrupted restart is not accepted. Its exact owned orphan player and private Weston/audio processes were identified by command/log ownership and terminated; desktop audio/display services were untouched. `interrupted-run.json` preserves the incomplete gate. The process harness now converts termination into an unwind, reaps its owned Unity process and writes interrupted evidence. A subprocess regression verifies this cleanup; all seven Python tests pass. A separately launched full 1440p rerun is underway.


The separately launched full 1440p journey passes all three phases at `Builds/evidence/20260908T024213614277Z-demo-journey/summary.json`, completing the strict 720p/1080p/1440p functional matrix for artifact `aad63caf1a72a13670a952c50e1140fbcb65b88b7ae69142b1a3bb546ea31b5a`. That matrix remains tied to its artifact; subsequent diagnostic additions have their own build identity.

UI-only performance instrumentation adds separate entry/interaction/exit windows for the constellation editor and observatory. Initial run `Builds/evidence/20260908T025416959995Z-ui-performance` functionally completed both scenarios without source/copied-profile mutations or runtime errors, but failed measurements: uncapped frame rate overflowed the preallocated buffer before 30 seconds, and the native allocation hook did not recognize the new UI flag. The runner now uses the same 120 FPS cap as the flight profiler and enables the existing allocation probe for `--clustr-ui-performance`. Missing values were failures, never claimed as zero allocation. Palette/preset coverage is now recorded explicitly.


Separate UI profiling repeat `Builds/evidence/20260908T030116818600Z-ui-performance` uses build `20260908T025850Z` (artifact `03342add9f1e80b794ae0007ec5e06dcd8a42e6621d2233881979244157c24af`). Both functional scenarios pass with byte-identical source/copied profiles, no runtime errors, full CPU/GC/memory availability and no sample overflow. Editor: 31.228 seconds / 3,746 frames, 22 controller interaction steps, all six palettes and three presets, CPU p95 8.34 ms, maximum frame 13.86 ms, allocation peak 672,896 bytes. Observatory: 30.584 seconds / 3,664 frames, 18 interaction steps, CPU p95 8.34 ms, maximum frame 12.65 ms, allocation peak 332,234 bytes. All measured frames are explicitly UI intervals; no UI allocations/stalls were hidden in ordinary-flight statistics. Each scenario lacks one GPU sample and therefore fails complete measurement acceptance. No release qualification is claimed.


### Current artifact: native Wayland diagnostics

Validation `Builds/validation/20260908T025645608302Z/summary.json` passes **215 EditMode / 16 PlayMode** with zero compiler diagnostics. Release-configured build `Builds/build-logs/20260908T025850Z/BuildLinuxRelease.json` has tree SHA-256 `03342add9f1e80b794ae0007ec5e06dcd8a42e6621d2233881979244157c24af`. Source remains the working tree rooted at `775f826f51ee70d8f25e3e421887eaa5a6f10801`, not a clean committed revision.

Native Wayland active-feature stress at `Builds/evidence/20260908T030255566948Z-hierarchy` captured **3,601 / 3,601 GPU samples**, zero ordinary-flight managed allocation, CPU p95 8.3345 ms and maximum non-loading frame 10.147 ms. All numerical requirements for this 30-second preflight window were met. The report still fails qualification for unavailable clean build provenance; 30 seconds never substitutes for the required 600-second release run. The corresponding X11 run `20260908T030458570692Z-hierarchy` retained incomplete GPU samples. This establishes one complete Wayland stress measurement, not a universal counter fix.

Native Wayland UI repeat `Builds/evidence/20260908T030657865481Z-ui-performance/summary.json` passes both functional scenarios and preserves source/copied profiles byte-for-byte, with no runtime errors. Editor: 31.281 seconds, 3,746 frames, 3,744 valid GPU samples, CPU p95 8.337 ms, maximum frame 28.893 ms, peak allocation 672,896 bytes, all six palettes and three presets. Observatory: 30.587 seconds, 3,658 frames, 3,650 valid GPU samples, CPU p95 8.339 ms, maximum frame 15.372 ms, peak allocation 332,794 bytes. All frames are classified as UI interactions. **Both measurement verdicts remain failed because GPU samples are incomplete.** Native Wayland must not be described as resolving this UI gate. Historical failed reports remain preserved.


The current `03342add…` artifact passes strict three-process journeys at 720p (`Builds/evidence/20260908T031222779745Z-demo-journey/summary.json`) and 1080p (`Builds/evidence/20260908T031846658548Z-demo-journey/summary.json`). The visual review of its constellation celebration found a fixed-width stroke becoming subpixel at overview distance. A small camera-relative minimum width is now being validated; these preceding runs remain tied to their original artifact and must not be relabelled as evidence for the next build.

The manual controller launcher now accepts all three target resolutions and an optional copied fixture v2 profile. It hashes source progress before/after play, rejects symbolic links, copies JSON progress only and excludes sibling v1 files. Two new harness regressions pass (nine Python tests total). Reports explicitly distinguish seeded inspection from fresh-start play; this convenience cannot establish normal campaign pacing or player acceptance.


### Celebration legibility correction

The fixed six-unit world-space celebration strokes became subpixel at distant viewpoints. A cached-camera calculation now preserves their authored close-up width with a modest 1.75-pixel minimum at distance. Intensity, reduced motion, source endpoints, progression and flight tuning remain governed by their existing settings. Validation `Builds/validation/20260908T032506862861Z/summary.json` passes **215 EditMode / 16 PlayMode** without compiler diagnostics. Release-configured build `Builds/build-logs/20260908T032713Z/BuildLinuxRelease.json` passes the complete 1440p three-process journey at `Builds/evidence/20260908T032848462106Z-demo-journey/summary.json`. The distant constellation capture was inspected and its trace is now discernible. This is a targeted legibility correction, not blanket art/accessibility acceptance.


### Complete measurement path on the revised artifact

Default-threaded Wayland stress `Builds/evidence/20260908T034145328077Z-hierarchy` preserves zero ordinary-flight allocation after the stroke correction but fails GPU completeness (3,330 valid / 3,476 samples). Its CPU p95 is 9.283 ms and maximum frame 46.038 ms. This failure is preserved.

Unity documents `-force-gfx-direct` for single-threaded rendering ([official command-line reference](https://docs.unity3d.com/6000.0/Documentation/Manual/PlayerCommandLineArguments.html)). Both profiling scripts now accept explicit `--single-threaded-rendering`, recording the requested mode and complete command without changing normal launch defaults.

On artifact `c92f1c09967d0225d0124d2dcb463bdc1f8d218a8b4e5966f89208fc8d610af8`, native Wayland plus direct Vulkan rendering passes both UI diagnostic scenarios at `Builds/evidence/20260908T034252464766Z-ui-performance/summary.json`. The log confirms `kGfxThreadingModeDirect`. Editor: 31.237 seconds, **3,742 / 3,742 GPU samples**, CPU p95 8.341 ms, GPU p95 0.181 ms, maximum frame 12.295 ms, peak allocation 673,456 bytes, 22 interaction steps, six palettes and three presets. Observatory: 30.600 seconds, **3,653 / 3,653 GPU samples**, CPU p95 8.344 ms, GPU p95 0.216 ms, maximum frame 17.178 ms, peak allocation 332,794 bytes and 18 steps. Both have complete CPU/GC/memory availability, no runtime errors, unchanged source/copied profiles and no ordinary-flight samples. **UI diagnostic acceptance passes for this recorded configuration.** It does not qualify a release or establish complete counters under normal threaded rendering.

The matching direct-rendering active-feature stress preflight at `Builds/evidence/20260908T034451278359Z-hierarchy` records 14,053 resident nodes and 57,000 routes, with guidance and ghost presentation during all 3,325 measured frames, the ghost in view for 931, audio for 3,291 and celebrations for 2,213. All **3,325 GPU samples** are valid. CPU p95 **12.477 ms**, GPU p95 **0.860 ms**, maximum non-loading frame **19.984 ms**, ordinary-flight allocation **0 bytes**, player peak **1,232,273,408 bytes** and GPU peak **431,669,248 bytes**. All numerical preflight requirements are met. The only qualification failure is **Build provenance unavailable**; the source remains a working tree and this 30-second diagnostic never replaces 600-second release qualification. Both reports/logs retain their artifact/configuration identity.


### Final automated checkpoint for this continuation

Artifact `c92f1c09967d0225d0124d2dcb463bdc1f8d218a8b4e5966f89208fc8d610af8` now passes every strict three-process resolution journey: 720p `20260908T033521962275Z-demo-journey`, 1080p `20260908T034611911491Z-demo-journey`, and 1440p `20260908T032848462106Z-demo-journey`. Fresh recipient import `20260908T035237793255Z-recipient-import` passes on that same artifact, preserving source bytes/v1 and writing no progression files. All nine Python harness tests and the diff check pass.

[The concise feature checkpoint](feature-acceptance-checkpoint.md) and `Builds/evidence/20260908T035331718651Z-feature-checkpoint/summary.json` consolidate this artifact's tests, journeys, complete direct-mode UI measurements, numerical stress preflight and remaining gates. Required physical-controller/accessibility/art/pacing and public-provider acceptance remain open. The source is a working tree; no catalogue-complete or release-qualified claim is made.

## Presentation iteration — 2026-09-08 UTC

The player accepted the current controls for now and requested world-scale and UI improvements. See [flight-deck and solar-system presentation](visual-direction.md) for implemented changes and current-artifact evidence. Previous performance reports remain historical; this iteration does not close artistic acceptance or release qualification.

## Orbital simulation foundation — 2026-09-08 UTC

The approved direction is Newtonian local flight, assisted orbit capture and compressed distances. [Technical research](space-simulation-research.md) records primary-source Elite Dangerous and Star Citizen mechanisms, dated implementation accounts and proposed Clustr engineering contracts. [The orbital implementation checkpoint](superpowers/plans/2026-09-08-orbital-flight.md) distinguishes delivered changes from remaining cruise, propulsion, impact, live-scale and cockpit work.

Current working-tree validation passes 224 EditMode / 17 PlayMode tests without compiler diagnostics (`Builds/validation/20260908T051222242554Z/summary.json`). Release build `20260908T051723Z` has artifact tree SHA-256 `2df4654dea970b11a92bfbe987a9499081be85a568d06762458962d00bfa6238`. Isolated 720p visual/orbit run `20260908T051915404095Z-visual-smoke` passes moving-body capture, 120 simulated seconds of released coasting, origin preservation and current scale checks without runtime errors. The 720p fixture scan/create/export/restart run `20260908T052019939073Z-demo-journey` passes both processes and preserves v1 sentinels and saved artifacts. Fixture scanning does not prove safe free-flight surface approaches. Prior failed runs remain preserved.

No visible game was launched for human review; the player deferred hands-on testing for the night. All outstanding original qualification gates remain open, and prior performance evidence does not qualify this changed physics artifact. Source is still a dirty working tree rooted at `775f826f51ee70d8f25e3e421887eaa5a6f10801`, not an accepted clean source revision.

## Flight-first simulation execution — 2026-09-08

[The approved six-phase execution ledger](superpowers/plans/2026-09-08-flight-first-simulation.md) records shared demo/live layout, precise orbital coordinates, surface-relative scanning, safe approach eligibility, bounded mesh detail and current-artifact verification. The phase-one artifact `2c4d49e76e79d311bce3360a14ff2c49aa579f60974bea4e53c00d26fb8f72f1` passes 228 EditMode / 17 PlayMode, a real approach/scan/orbit player scenario, full-limit provider-fixture materialization and save/export/restart. Human acceptance and original release gates remain open. Further simulation changes require new evidence.

### Flight-first simulation: orbit and precision evidence (2026-09-08)

The [simulation execution ledger](superpowers/plans/2026-09-08-flight-first-simulation.md) records 238 EditMode / 21 PlayMode tests and release artifact `608c3318369776f004ac3d650737df9cae552a87ea9d592a8835059a65329212`. Its isolated player passes planet/moon capture and released coast, trajectory instruments, actual approach/scan, planetary/stellar recovery, and corrected origin rebasing; maximum fixture hierarchy and creation/restart also pass. Between-tick rebase failure evidence is preserved alongside its successful rerun. These are automated and limited visual checks, not human acceptance or performance qualification. Destination staging is in progress; full cruise/jumps, cockpit station integration, activity adaptation and final qualification remain open.

### Cruise vertical slice (2026-09-08)

The [flight-first execution ledger](superpowers/plans/2026-09-08-flight-first-simulation.md#cruise-vertical-slice-2026-09-08) records a release player with 261 EditMode / 21 PlayMode tests and an 81.8 simulated-second real departure/cruise/approach journey. The player verifies charging/cruise cancellation, safe local handoff, unchanged progression, and modal pause/restoration; full-limit hierarchy and creation/restart also pass. Route-jump integration, complete cockpit/activity adaptation, live/offline simulation journeys and original release qualification remain unresolved. The beta is not qualified.


### Flight-first surface and campaign follow-up — still unqualified

The [simulation execution ledger](superpowers/plans/2026-09-08-flight-first-simulation.md) records renewed 265 EditMode / 22 PlayMode passing checks, nine passing Python harness tests, development/release build `20260908T190449Z`, logical-anchor/pooling corrections, and exterior campaign/anomaly placement. Its latest historical stress preflight fails on working-tree provenance and 32 B/frame ordinary-flight allocation. Full jumps, physical campaign/course traversal, cockpit stations and final qualification remain open; human acceptance stays deferred.


The cockpit duplicate-action/closure bug is now fixed and verified on release artifact `cfa59860fda9033e38e9ecdd09a9d2fcea7f868c1db6a955198c2aca14dc006b`. Simulation journey `20260908T191848900864Z` passes stable action pooling and the safe 81.75-simulated-second trip. Stress preflight `20260908T191949455232Z` measures zero managed allocation over all 3343 frames; CPU p95 11.36 ms, GPU p95 0.69 ms, maximum ordinary frame 14.92 ms. Its only report failure is working-tree build provenance. The release remains unqualified; all 600-second, platform, live journey and deferred human gates still apply.


Renewed 720p campaign creation/restart `20260908T192115270340Z-demo-journey` passes on that same release tree, including saved records/exports and unchanged v1 sentinels. This preserves the distinction between fixture-assisted campaign acceptance and the real-propulsion cruise journey; it does not close all flight-first phases.


### Charged jumps and cached inspection — implementation follow-up

The [flight-first execution ledger](superpowers/plans/2026-09-08-flight-first-simulation.md#authoritative-charged-jumps-and-system-inspection-2026-09-08) records session-issued jump preparations, three-second charging, cancellation/failure/stale-result protection, stable-ID return destinations and removal of the ordinary Unity direct-fast-travel path. Source inspection retains the current physical system. Final-source validation `20260908T201904651047Z` passes 268 EditMode / 22 PlayMode with no compiler diagnostics; ten Python harness tests pass.

The new player harness performs an outbound/return jump and then repeats after a cache-only restart, explicitly counting offline requests. Its first two runs passed online and failed cached thread presentation without making any offline request. The session now binds live selection after inspection state, covering synchronous cache completion. Failed reports remain historical evidence, not accepted journeys. Full cockpit/map separation, unexpected materialization rollback, complete activity flight journeys and all original release qualification gates remain open. Human acceptance is deferred and the beta remains unqualified.


Final charged-jump release `de13cb9ff3b3cbc8c0c957e1ea4a1d8ca5d8e062c63a7d566bb8859b1b453131` (build `20260908T202809Z`) passes renewed fixture hierarchy and round-trip travel in `20260908T203201711402Z-hierarchy`. The restarted offline process passes the same journey with **zero network requests**; both directions return to enabled local flight with the map closed. Validation `20260908T202550818357Z` passes 268 EditMode / 22 PlayMode without compiler diagnostics. Both development and release builds pass. This supersedes the failed cached-thread attempts as current functional evidence; failed reports remain intact. Source is still a working tree, and release qualification/human acceptance remain open.


The stronger demo test subsequently exposed an interpolated Rigidbody origin-shift defect missed by safety-only checks. The corrected release tree `4132dab1b5b5f3f12bac37fbd8c2f4d42c1803e7ffabc49d852c346a18335387` (build `20260908T204455Z`) passes `20260908T204844082880Z-simulation-journey`: 81.75 simulated-second departure/cruise and a settled jump arrival 499.8 m beyond stellar exclusion at 1.14 m/s relative speed. Validation `20260908T204349936531Z` passes **268 EditMode / 23 PlayMode**, including the new failing-then-passing interpolation reproduction. Prior false-positive proximity evidence and failed reports are preserved and distinguished in the execution ledger.

Fresh public run `20260908T203348169321Z-hierarchy` remains **failed**: revision 238 / catalog 14 overview loaded, but the selected community response was rejected. A bounded diagnostic GET returned HTTP 403. Public hierarchy/jump compatibility is unresolved; fixture passes do not substitute for it. No final performance, clean-source, Ubuntu, CI or human acceptance gate is closed by this work.


Final-artifact fixture run `20260908T204939547797Z-hierarchy` also **passes** on release tree `4132dab1b5b5f3f12bac37fbd8c2f4d42c1803e7ffabc49d852c346a18335387`. It completes the hierarchy and cancelled/retried round-trip jumps, then repeats after cache-only restart with **zero provider requests**. Both directions retain safe placement, unchanged progression and usable local-flight controls. This is 1440p direct Wayland/Vulkan functional evidence. All failed/historical reports remain preserved, and the beta is still unqualified.


### Flight-first continuation: cockpit stations and activity scale

Implemented: session-owned input contexts with nested modal leases; common projected surfaces for studio, notebook, evidence, observatory, diary, replay and local-library/image views; a cockpit chart camera/feed that preserves local physics; removal of obsolete chart momentum restoration; demo chart candidates; safe scenic/playground placement and long-segment corridor tracking; course version 2 and old-ghost competitive rejection; safe expedition exclusion clearance. The accepted flight bindings remain unchanged.

Automatically verified: `Builds/validation/20260908T213513255513Z/summary.json` passes 271 EditMode / 23 PlayMode with no compiler diagnostics. Ten Python harness tests and whitespace checks pass. The earlier cockpit artifact passes the 720p simulation journey `20260908T212246383613Z-simulation-journey`; chart/studio/observatory captures were visually inspected. The full campaign attempt `20260908T212341395455Z-demo-journey` remains failed and led to the scale corrections. Fresh post-correction artifact/player evidence follows separately.

Not player accepted or release qualified. Physical activity journeys, all-resolution station checks, provider compatibility, full performance measurements, clean-source platform builds and licensed CI remain open until supported by their own evidence.

Post-correction build `20260908T213834Z` succeeds in both configurations: development tree `f97b39336d4fc863cd570bca34868809eea6db3d4d684b95053fcebad224a5a7`, release tree `4f100570d72cbbcf4688d542161592b7aa06d0cf7ca79ea923353314379d9fe0`. Source is still `775f826f51ee70d8f25e3e421887eaa5a6f10801-working`, not a clean checkout.

The expanded 1080p simulation journey `20260908T214225380670Z-simulation-journey` passes on that release artifact. It verifies a controlled manual overspeed drop outside a star, approach cancellation, unavailable-target cancellation, chart physics/camera stability, nested-context restoration, charged jump cancellation/retry, and unchanged activity progression. Nearby-planet travel takes 81.7472 simulated seconds with 684 finite safe trajectory samples; settled jump clearance is 499.783 m at 1.143 m/s relative speed. This uses explicit initial safety fixtures and 4x cruise execution and does not count as performance qualification or human acceptance.


The complete 720p campaign/flight/expedition journey `20260908T220043286666Z-demo-journey` passes on release tree `3753f8a15d3cffd0fbcb551946eddd802876878637d1e769cdc1f944149d0df6`: create (298.972 s), separate-process expedition resume (79.555 s), and final restart (37.275 s). All three preserve v1 sentinel bytes and the required v2 profile/export/replay state. This includes virtual-controller traversal, three scenic profiles, physical autopilot preview, scored fixture courses, saved and imported ghosts, all campaign content, observatory displays, and all three expedition moods. Manual scored-course flight and human enjoyment remain unaccepted. The 1080p basic creation/restart run `20260908T220753933792Z-demo-journey` also passes.

Visual review of the all-display observatory capture found diorama models leaking into the main camera around projected panel edges. The pilot camera now excludes the dedicated diorama layer, with a player assertion. Fresh artifact and visual evidence for this correction follows separately; functional success alone did not close the visual gate.


Final release tree `ede6423d9f5258cb9ae1bae10fae8ec0e92d45e5cf0ac5053d8adfcc04a555dd` passes the full **2560×1440** campaign/flight/expedition journey `20260908T221439102982Z-demo-journey`: create 301.286 s, independent expedition resume 80.579 s, restart 37.791 s. All three processes exit zero and preserve v1 sentinels; required v2 state, postcards and replay packages satisfy the restart checks. The populated observatory capture was inspected after the long flight/rebase sequence: its models remain inside the dedicated view, with readable controls and no prior camera-layer leakage. Virtual-controller and fixture coverage remain distinct from real-controller acceptance and physically flown scored challenges.

Final-artifact fixture run `20260908T222151912609Z-hierarchy` passes the complete online hierarchy and cancelled/retried round-trip jumps, then passes a cached restart with **zero network requests**. It uses recorded direct Wayland/Vulkan rendering and is functional evidence, not public-provider compatibility or performance qualification.

The final artifact also passes creation/restart journeys at **1280×720** (`20260908T222234308966Z-demo-journey`) and **1920×1080** (`20260908T222337171755Z-demo-journey`). Together with the full 1440p campaign run, these establish automated resolution coverage for the shared cockpit surfaces and core creation/observatory flow. The final 720p studio capture was inspected for control legibility; exhaustive human screen acceptance remains deferred.

Final-artifact replay recipient run `20260908T222454459002Z-recipient-import` passes in a separate `replay-recipient` profile, creates **no progression files**, preserves source replay bytes and v1 sentinels, and reports no runtime errors.

Final 1440p simulation run `20260908T222545840242Z-simulation-journey` passes on release tree `ede6423d9f5258cb9ae1bae10fae8ec0e92d45e5cf0ac5053d8adfcc04a555dd`. The trip takes 81.7482 simulated seconds, with 679 finite safe samples, 304.453 m destination exclusion clearance and 15.322 m/s closing speed. Settled jump clearance is 499.784 m with 1.143 m/s relative speed. Manual overspeed drop, approach/target-loss cancellation, chart physics/camera stability, modal restoration and unchanged progression all pass. These functional fixtures remain distinct from 600-second performance qualification.


Final active-feature stress preflight `20260908T222651869392Z-hierarchy` remains **failed / unqualified** with the single report failure **Build provenance unavailable** (the source identifies an uncommitted working tree). The thresholds were not changed. At 2560×1440, recorded direct Wayland/Vulkan rendering, it completes hierarchy smoke and measures 30.0129 seconds / 3,352 samples, all with valid attributable GPU timing and no invalid samples. CPU p95 is 10.9924 ms; GPU p95 0.5963 ms; maximum ordinary frame 15.9506 ms; all ordinary-flight managed allocations are zero. Peak player RSS is 1,326,415,872 bytes; GPU requested buffers 486,039,552 bytes. Cache usage is 18,225,621 bytes; verified warm cached-source open takes 0.02328 s (not process-to-visible-overview timing).

The scenario records 14,053 resident nodes / 57,000 resident routes across retained scopes, moving flight over 878.286 m, guidance and ghosts throughout the window, 893 ghost-in-view frames, 3,300 audio frames, and 2,226 celebration frames. Maximum active voices and celebrations are both three. This is a short numerical regression check, not either required 600-second clean-artifact qualification run. Failed provenance, public-provider compatibility, clean Arch/Ubuntu evidence, CI activation and deferred human/physical-flight acceptance keep the beta unqualified.

## Public community compatibility repair — 2026-09-09

The previous public traversal rejection was a client regression, not established
provider unavailability. The provider's `RevisionHandler.Community` publishes
revision/catalog and scene arrays but omits optional scope/root metadata.
`SpatialSceneSnapshotSource` now binds those omitted community values to its
pinned request using the existing scope contract. Contradictory values remain
rejected; system/discussion/thread responses still require their published fields.
Eight new regression cases cover this boundary.

- Automated: `Builds/validation/20260909T042605014651Z/summary.json`: 279 EditMode,
  23 PlayMode passed, no skipped tests or compiler diagnostics.
- Release build: `Builds/build-logs/20260909T042813Z/BuildLinuxRelease.json`.
- Public player: `Builds/evidence/20260909T042941031541Z-hierarchy/summary.json`:
  hierarchy and route jump passed on revision 243/catalog 14. Overview 300/180,
  community 500/1500, system 69/103, discussion 21/20, thread 14/13 nodes/links.
- The earlier standalone HTTP 403 diagnostic did not explain Unity's rejection.
  Preserve it as historical evidence, not as a current provider-blocker finding.

This repair does not itself establish clean-source, full live performance,
Ubuntu, hosted CI, or deferred human acceptance. Those remain separate gates.


## Reproduced blockers and candidate verification — 2026-09-09

The original working tree and index are preserved. A separate local candidate
branch/checkouts provide immutable source identity without replacing the inherited
implementation or pushing anything. Initial clean candidate
`a63edb20a73b3d27761ef563aaccc20f4a3bc091` passed 279 EditMode and 23 PlayMode
tests and built both Linux configurations. Unity-generated Addressables link
files are excluded from source so a fresh build does not dirty its own checkout.

The same candidate passed the real hierarchy and a 600-second live active-feature
performance run (`20260909T044018161644Z-hierarchy`): CPU p95 8.340 ms, GPU p95
0.301 ms, maximum ordinary frame 34.782 ms, zero managed allocation. Its stress
run (`20260909T045054483312Z-hierarchy`) failed the unchanged 50 ms stall limit
with a 405.880 ms frame. These reports remain historical, artifact-specific
evidence, not qualification of later revisions. The reported warm-cache duration
measures verified cached-source opening, not process-to-visible-overview latency.

Bounded frame traces now retain all samples, native thread/process CPU deltas,
context switches, major faults, GC collections, GPU-memory-query cost, fixed steps
and celestial-update work. A diagnostic stress reproduction on `6e6064c` still
failed with a 74.690 ms stall; its trace rules out a GC collection or slow GPU
memory query for that event and records substantial main-thread work and
preemption. This is diagnosis, not a claimed performance improvement.

A subsequent 30-second attribution run (`20260909T054227644944Z-hierarchy`)
measured 7,046 orbital updates per fixed step and 2.426 ms average orbital work.
The implementation now prepares invariant Kepler coefficients and reuses only
same-time local ephemerides while recomposing parent positions and origin frames.
Exact-equation regression and cache/pooling/frame tests passed (288 EditMode,
24 PlayMode). Post-change player performance must pass independently.

The cold-import failure was a locked third-party package compatibility defect:
serialization 3.1.5 referenced UnityEditor.GUID, then Unity's API updater repaired
two cached files after logging compiler errors. The [documented compatibility
patch](third-party-patches/unity-serialization-3.1.5.md) embeds that same package
version with only those two editor adapter namespace corrections. All other
package bytes, licenses and tests are retained. Warm validation passes 289
EditMode / 433 PlayMode tests, including the newly enabled upstream tests. The
remaining upstream EntityId deprecation warnings are inventoried separately.
A new checkout with no Library is undergoing first-import validation and the full
player matrix at source `49fae2a`; no result is presumed before it finishes.

The Ubuntu container's missing compiler is repaired by rebuilding the documented
24.04 image with build-essential. Its Editor now starts but exits 198 with
"No valid Unity Editor license found" before test discovery. Existing host license
files were mounted read-only; no machine identity or licensing was bypassed.
Evidence: `Builds/evidence/20260909T043033Z-clean-candidate/ubuntu-result.json`.
The existing Gitea runner advertises Ubuntu and kvant labels, but not the required
licensed Unity label. Ubuntu licensing and CI activation remain actual external
dependencies. Human testing remains deferred; the expanded beta is unqualified.


The new rendered-startup check exposed a blank initial fixture chart: the old
fixed camera distance did not contain near-edge clusters in the published
projection envelope. Chart distance now fits the full envelope; marker size uses
the same presentation scale as their positions. Framing regression tests cover
all bound edges at three aspect ratios. The diagnostic waits for the chart camera
to settle before saving its screenshot, rather than accepting the first tiny
object entering the frustum mid-transition.

The first fully rendered timing was 4.732 s (first Unity frame at 2.848 s;
verified scene at 3.140 s; rendered capture at engine time 3.570 s). It failed the
unchanged three-second threshold. The project now disables the optional Unity
splash screen through its supported Player setting, as documented in the
[Unity 6.4 manual](https://docs.unity3d.com/6000.4/Documentation/Manual/class-PlayerSettingsSplashScreen.html).
This applies to normal players too. A fresh measured run is required to establish
the resulting startup time; cache-source timing remains separately labelled.


Rendered startup is now automatically verified on source
`b9d5c8316c8605703a669f82db5e6ca003fe1088`, release build `20260909T070556Z`:
fixture `20260909T070744887708Z-warm-start` passes in 2.686 s with zero observed
fixture requests; public-cache `20260909T070813908093Z-warm-start` passes in
2.538 s, revision 243/catalog 14, 300 resident regions. Both are new offline
processes, include settled chart/HUD frames and 1440p screenshot writes, and
preserve the source cache. The public run enforces offline mode but does not
claim an independent network packet capture. Validation `20260909T070353567394Z`
passes 292 EditMode / 433 PlayMode tests with no project compiler diagnostics.
The fixture screenshot was visually inspected. Full final-source performance,
platform, physical-course and human acceptance still require their own evidence.


### 2026-09-09 full automated pass and remaining stress repair

Clean checkout `a8ba789ff5e9fee03443220d8932c16cd88b2ca3` started without a
Library and passed 292 EditMode plus 433 PlayMode tests, both Linux builds,
all three resolutions of automated cockpit/simulation/creation journeys,
complete campaign/resume/restart, isolated replay import, fixture/offline/jump
journeys, rendered cached startup, and the public hierarchy. Full matrix:
`Builds/worktrees/flight-first-final-20260909T0710Z/Builds/evidence/20260909T070954916173Z-full-pass/summary.json`.

The public 600-second performance run passed (CPU p95 8.338 ms, GPU p95
0.296 ms, ordinary maximum 22.020 ms, zero managed allocation). Near-limit
active stress failed its unchanged 50 ms ordinary-stall threshold: maximum
99.379 ms, CPU p95 12.369 ms, GPU p95 0.803 ms, zero managed allocation.
Raw traces show a five-physics-step catch-up burst without GC collection.
This remains an unresolved gate, not a qualified beta.

Native userspace sampling with exact installed Unity symbols identified
repeated Component/Transform access and job scheduler overhead. An unchanged
artifact trial with eight job workers reduced process CPU but failed with a
79.481 ms stall; no worker-count change was adopted. The subsequent repair
caches Transform references and reads the ship position once per surface
sweep instead of once per resident body. Its validation is pending.

The harness now records ID_LIKE and accepts the exact `arch` token while
preserving Omarchy as the actual OS identity. Two regression tests cover
Arch derivatives, unrelated distributions and substring rejection (20 Python
checks pass). Earlier reports are preserved; their OS classification is not
rewritten. Ubuntu licensing, licensed CI activation and deferred human
acceptance remain separate open requirements.

## Free presentation pass — 2026-09-19

The zero-cost asset pass is implemented in source: six attributed Solar System Scope maps, six CC0 ship models, a satellite dish, a console and its palette atlas, three sound clips and two controller glyphs (20 original asset files). The catalog builder generates project-compatible materials and normalized presentation prefabs. Runtime adapters preserve procedural fallbacks and existing controls/physics, and audio respects mute, reduced audio and local pause. License notices are copied beside future builds.

See [asset notices](free-asset-notices.md) and [implementation plan](superpowers/plans/2026-09-19-free-presentation-assets.md). Status: **implemented, not Unity-verified or player accepted**. All 23 Python checks passed. Standalone Roslyn compilation of Core, Unity, Editor and EditMode test assemblies passed against installed Unity libraries with no diagnostics. Six new Unity asset-contract tests await execution.

Both Unity suites attempted at `Builds/worktrees/free-presentation-assets-20260919/Builds/validation/20260919T164438242790Z/` exited 198 before import/compilation: no valid Unity Editor license. This pass has no new player artifact, shader/import proof, visual/audio acceptance or performance result. Refresh the local free Personal entitlement in Unity Hub, then run the normal validation/build and isolated-player journeys. The prior ecba4ba evidence remains historical; it does not qualify these changes.

### Free presentation follow-up: license restored

The license blockage above was resolved by signing into Unity Hub. Import defects and FBX centering were repaired; validation `20260919T174433348728Z` passes 298 EditMode + 434 PlayMode tests. Both Linux players built. Initial 1080p player screenshots and approach/orbit/recovery checks passed, and exposed a coarse-planet LOD issue now corrected with quality-normalized detail thresholds. An updated visual harness also asserts catalog and audio behavior. Full updated-artifact evidence is recorded in timestamped `Builds/evidence/` reports; historical failures are preserved. Human acceptance, Ubuntu licensing and CI remain distinct requirements.
