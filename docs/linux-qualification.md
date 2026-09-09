# Linux build and qualification procedure

Status: unqualified implementation candidate, 2026-09-06. See beta-completion.md.

Use Unity 6000.4.10f1 with the Linux Standalone module and a valid editor license.
Keep Packages/manifest.json and packages-lock.json unchanged. A clean checkout has
no Library, Temp, or pre-existing player directories. Record git SHA, git status,
editor version, OS release, kernel, GPU/driver/backend, and runtime host separately.

## Entrypoints

From the repository root, run `python3 scripts/full_pass.py` for the serialized
automated pass before human testing. It runs Python checks, both Unity suites,
both Linux builds, controller smoke, the full campaign/restart journey,
resolution-specific visual and simulation journeys, replay import, separate
cockpit interaction measurements, fixture hierarchy/offline jumps, development
smoke, 600-second active stress, and bounded public hierarchy/jump reads. Public
performance runs only after that public hierarchy prerequisite succeeds.

Each run writes `Builds/evidence/<timestamp>-full-pass/summary.json`, linking all
child reports and the full release/development artifact hashes. Failed child
processes, absent/invalid/extra reports and changed release artifacts fail the
matrix. A failed compilation or build stops before player execution. Other
independent checks continue to expose more than the first failure. Reports are
preserved; rerunning creates a new evidence directory. `--profile-seconds 30`
and `--no-live` are diagnostic subsets and cannot produce a full-pass verdict.

This command does not supply clean-checkout Arch evidence, a licensed Ubuntu
environment, hosted CI, or human controller/enjoyment acceptance. It never marks
the release qualified. Scored-course and campaign fixture placement remains
labelled separately from real propulsion, orbit, recovery and cruise journeys.

```sh
export UNITY_EDITOR=/path/to/6000.4.10f1/Editor/Unity
python3 scripts/validate.py
python3 scripts/build.py --configuration development
python3 scripts/build.py --configuration release
```

`validate.py` defaults to both suites and fails on process failure, compilation
errors/project C# warnings, failed tests, empty or missing XML. Unity shutdown
timeouts remain failures even when test XML passes. Evidence is timestamped in
Builds/validation and Builds/build-logs. Build JSON records artifact hashes and
never grants qualification by itself. Dirty source is labelled `-working` and
cannot pass the qualification provenance gate.

The shared process wrapper starts private null-sink audio services and waits for
actual Unity process termination. Install PulseAudio, or PipeWire, pipewire-pulse,
WirePlumber and pactl. Do not point jobs at the desktop audio server. A timeout
captures the owned process session before terminating it and remains a failure.

For isolated virtual-controller player checks at all three resolutions:

```sh
python3 scripts/ui_smoke.py --player Builds/Linux/ClustrGalaxy.x86_64
```

This requires Weston with headless GL and Xwayland, supplied by the system or
`WESTON_ROOT`. `CLUSTR_DRI_PRIME` selects the reference GPU. The runner creates
private display/audio sessions and separate player storage, saves screenshots,
and records real player exit codes. A virtual-controller pass does not replace
real-controller or full gameplay acceptance.

## Isolated Ubuntu 24.04

```sh
docker build -t clustr-galaxy-ubuntu-build:24.04 scripts/ubuntu
```

Prepare an independent source checkout owned by UID 1000. Mount it at `/project`
and the exact Linux editor installation read-only at `/opt/unity`. Supply a valid
Unity license through the organization's licensed runner mechanism. Do not copy
personal access tokens into the image or repository. Run the same validation and
build commands in that container. The container shares the runtime host kernel;
record Ubuntu userland/build environment separately from the Arch runtime host.
The test attempted on 2026-09-06 stopped with exit 198: no valid Unity license.
Ubuntu build and Ubuntu-built-player smoke remain uncompleted.

## Licensed Gitea runner

The workflow requires a runner labelled `unity-6000.4.10f1-linux`, Python 3.11+,
Git, a C11 compiler, exact Unity editor and Linux module, a valid headless editor entitlement,
and writable isolated project/work directories. Configure UNITY_EDITOR in the
runner environment. Use one Unity job per checkout. Register the label as a host
runner only on an isolated build machine; do not expose production credentials.
Runner installation/registration and CI execution have not been demonstrated.
The checked-in workflow is configuration, not a passing CI result.

## Player acceptance

`Builds/Linux/ClustrGalaxy.x86_64 --clustr-demo` starts authored deterministic data.
The release configuration is under Builds/LinuxRelease. Normal launch attempts
live data then verified v2 cache. `--clustr-offline` prohibits Clustr requests.
Tab/Start opens Actions; Escape/B returns; keyboard/mouse text entry is supported.
Controller navigation still needs a real attached-controller acceptance run.
Use separate XDG_CONFIG_HOME directories for isolated journeys. Keep v1 sentinel
files and verify their hashes before and after the save/restart journey.

Exercise Explore, Survey starting system, held scans, all contracts and authored
discoveries, flight modes, local constellation save and queue confirmation,
restart, thread closure, selection, streaming, and floating origin. Check essential
controls at 1280x720, 1920x1080 and 2560x1440. Live acceptance requires overview,
community, system, discussion and thread under the same revision/catalog pin.

## Qualification boundary

The opt-in live preflight is `--clustr-live-smoke --clustr-beta-profile=30`.
Full duration is `--clustr-live-smoke --clustr-beta-profile=600`, at 2560x1440.
Reports are timestamped beneath the player's ClustrGalaxy/reports directory;
latest-report-path.txt points to the most recent without replacing historical JSON.
A 30-second pass never qualifies a release. Missing smoke, provenance, required
counters, sample counts, duration, or measurements must fail. Report write failure
must exit nonzero, including failure to replace the latest-report pointer. Report
write and pointer failures have deterministic tests; a durable JSON is preserved
when the pointer update fails. Near-limit fixture traversal, labelled intervals,
moving flight, and verified-source cache timing/usage are implemented. Complete
Radeon GPU duration coverage and process-to-rendered cached overview evidence
remain open before the harness covers the entire accepted plan. Do not interpret the current verdict alone as milestone completion.

Package only after the completion ledger is all passed at one source revision:
development/release players, controls, launch instructions, local storage/privacy,
provider limitations, dependency and asset notices, SHA-256 sums, environment and
qualification manifest. Public publishing is outside this procedure.

## Deterministic provider fixture

```sh
python3 scripts/hierarchy_smoke.py
python3 scripts/hierarchy_smoke.py --stress
python3 scripts/hierarchy_smoke.py --stress --profile-seconds 30
python3 scripts/hierarchy_smoke.py --live
python3 scripts/hierarchy_smoke.py --jump --offline-restart --window-system wayland --single-threaded-rendering
```

The fixture binds loopback only and never forwards traffic. The opt-in player
argument is `--clustr-fixture-url=http://127.0.0.1:PORT/api`; it is incompatible
with offline/demo flags. Fixture Survey identity and cache are separate from
published snapshots and authored demo progress. Small and near-limit functional
hierarchy runs passed on the Radeon host. The 30-second diagnostic failed, so
600-second qualification remains uncompleted. Selectable rendered objects drive
traversal; direct loader calls do not count as player hierarchy evidence.

Vulkan returned GPU timing samples on this host, with missing frames still failing
the gate. Unity documents Linux Vulkan timing support and delayed timing results:
[Unity 6.4 frame timing](https://docs.unity3d.com/6000.4/Documentation/Manual/frame-timing-manager.html).
Rendering support alone does not establish complete measurement coverage.

## Demo persistence integration journey

`python3 scripts/demo_journey.py --player Builds/LinuxRelease/ClustrGalaxy.x86_64`
launches separate create/restart players against one newly owned storage root,
with private display/audio sessions. It uses virtual-controller scan holds and
HUD confirmation for the authored six-member journey, local save and queue
request/cancellation/confirmation. Actions are reached with D-pad focus traversal, and scan targets use controller
reticle selection. In-range probe placement remains fixture setup, not manual
flight acceptance.
The harness requires both process exits, player results, unchanged persisted
profile on restart, and byte-identical v1 sentinel files. Evidence is timestamped
under `Builds/evidence/*-demo-journey/`. A missing report or timeout fails.
This does not replace the mandatory real-controller run.

The performance scenario now alternates a virtual-controller flight input every
five seconds to keep movement bounded. It reports traveled distance and rejects a
stationary run. Warm overview availability is measured through an independent
verified-cache-only source before sampling, with no network requests, and the
whole cache size is reported. RSS covers the entire player process; GPU samples
must have finite values and increasing timestamps. New fields fail closed when
absent from older reports. None of these checks relax the 600-second thresholds.

## GPU timing investigation

The preserved 30-second run `20260907T030012920953Z-hierarchy` contains all
3,601 GPU frame timestamps but only 3,599 valid durations. It is a missing-value
failure, not two missing timestamp records. The next report includes the exact
invalid GPU timestamps after sampling; this adds diagnostics without changing
thresholds or treating invalid values as zero-duration frames.

Unity documents a four-frame reporting delay and explicitly notes that it does
not guarantee correct GPU results. See the [Unity Frame Timing Manager
introduction](https://docs.unity3d.com/6000.0/Documentation/Manual/frame-timing-manager.html)
and [GetLatestTimings API](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/FrameTimingManager.GetLatestTimings.html).
The existing bounded drain and same-timestamp replacement stay in place. Missing
values remain failures pending a supported measurement or attributable profiler
record; no older preflight qualifies a changed artifact.

The local isolated-display harness also discovers an extracted Weston runtime at `Builds/tools/weston` before the legacy `/tmp/clustr-weston` path. `WESTON_ROOT` still overrides discovery. Extracted tooling is local test infrastructure, not a system package installation or a qualification result. `python3 scripts/art_preview.py` produces an isolated 1920×1080 asset contact sheet and artifact-linked report.


## Flight-first candidate evidence (2026-09-08)

The current flight-first execution record is [the simulation ledger](superpowers/plans/2026-09-08-flight-first-simulation.md). Use `python3 scripts/simulation_journey.py --resolution 1280x720` for the dedicated real-propulsion departure/cruise/safe-handoff journey and cockpit-pool/modal regression checks. It records its accelerated simulation clock explicitly and is not a performance benchmark.

Release build `20260908T191533Z` has a successful numerical active-feature 30-second preflight under explicitly recorded direct Wayland/Vulkan rendering, including all GPU samples and zero managed allocation. The report still fails provenance because the source is a working tree. This supersedes the earlier allocation defect for this artifact only; it does not supply 600-second stress/live qualification, default-renderer coverage, clean-source builds, or platform/human acceptance. Historical failed reports remain available. The Ubuntu 24.04 build image is present locally; licensed Ubuntu clean build/runtime verification and Gitea activation remain unverified.

### Charged jump and cached restart checks

The `--jump --offline-restart` fixture journey checks the published hierarchy, cancelled charging, safe outbound and return arrivals, then repeats the journey in a second process using the same private cache with `--clustr-offline`. It fails if the fixture receives any request during the second process. Stable IDs permit the return journey after the original system render objects are pooled. Distinct fixture systems have distinct child IDs. The fixture endpoint is loopback-only and selects the fixture cache namespace; it does not disable the offline prohibition.

Use `--live --jump` for bounded public hierarchy and jump reads; this is separate from fixture evidence. `--single-threaded-rendering` is a recorded diagnostic rendering configuration, and these functional runs do not sample or qualify performance. Jump and performance switches cannot be combined. Reports preserve artifact identity, online/offline process outcomes and fixture request counts. Deferred human acceptance remains open.


Editor tooling observation (2026-09-08): `20260908T213654Z/BuildLinuxBeta.log` records a native named-pipe `ToFileDescriptor` assertion and exit -6 before any artifact completion. Unity documents the same call path in its [Linux editor issue](https://issuetracker-mig.prd.it.unity3d.com/issues/crash-on-systemnative-fcntlsetcloseonexec-when-creating-a-new-3d-project), but that historical issue does not establish the cause on this host. During the unchanged retry, `20260908T213834Z/fd-observation.json` samples 99 process states, with highest observed descriptor 81 against the inherited 524288 limit. Ordinary descriptor exhaustion is not demonstrated. No Unity/package version or system limits were changed. Failed build records remain preserved; a later success does not erase the tooling failure.
