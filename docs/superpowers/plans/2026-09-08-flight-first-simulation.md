# Complete Clustr's flight-first simulation

Latest verification: the September 8 local / September 9 UTC full automated
pass is recorded in [the completion ledger](../../beta-completion.md) and
[its machine-readable index](../../../Builds/evidence/20260909T011215566712Z-full-pass/summary.json).
The final release passes all local functional journeys and all numerical
600-second active-stress thresholds, but remains unqualified: working-tree
provenance fails qualification and public revision 241/catalog 14 fails community
traversal. A separate bounded diagnostic receives HTTP 403. Public performance
is not attempted after that prerequisite fails. The pass corrected a music
caption overlap and a moving-time ghost assertion; it does not close deferred
human, clean Arch/Ubuntu, CI, packaging or remaining implementation gates.

Approved plan: Newtonian local flight, assisted orbit capture, compressed distances, 60–120 second nearby-planet trips, cockpit stations and recoverable incidents. Preserve accepted controls and all twelve exploration features. Unity 6000.4.10f1 and locked packages remain fixed. No fuel/heat/damage/repair/power gameplay, combat, landing, atmospheres, walkable interiors, backend changes or public publishing.

## Ordered implementation and gates

1. Shared deterministic demo/live celestial layout, satellite envelopes, surface-relative scanning/arrivals, double-precision frames and bounded visual detail. Gate: maximum live fixture plus actual safe approach/scan and origin-shift journeys.
2. Versioned ship/flight/propulsion contracts, bounded force and torque, inertia, explicit braking, always-on player gravity, swept contacts and safe recovery above 20 m/s inward impact or stellar exclusion. Gate: response/saturation/continuity and no tunnelling or progress loss.
3. Tidal-safe capture, moving centers, shared propulsion, trajectory instruments and correct residency release. Gate: ten-period stability, capture/release and rejected unsafe orbits.
4. Session-owned local/charging/cruise/approach and jump charging/transit/arrival states with cancellation, generation identity, published-route eligibility, validated destination loading and safe drops below 20 m/s. Gate: nearby-planet 60–120 second journey and complete cancellation/failure/offline matrix.
5. All gameplay screens as cockpit stations; one input-context owner; explicit paused observatory/editor/replay; activity/course/replay scale compatibility; cached accessible presentation. Gate: all feature journeys and 720p/1080p/1440p controller navigation.
6. Clean-source development/release builds; original qualification matrix plus simulation journeys, 600-second stress/live performance, Arch/Ubuntu, licensed CI, local packaging. Missing mandatory measurements or human acceptance keep the beta unqualified.

## Invariants and delivery

The session owns intent and outcomes; Unity applies physics/presentation. Human and automated flight share limits. Source projections and frozen snapshot/Survey eligibility remain authoritative; artistic physics stays separate. Preserve existing v2 progress with companion settings; never read or modify v1. Version tuning/course identities; incompatible ghosts remain viewable without competitive eligibility. Recovery never awards or removes progression. Preserve failed reports and relate every result to its actual artifact.

Validation entrypoints: scripts/validate.py, scripts/build.py, scripts/visual_smoke.py, scripts/demo_journey.py; extend with real-flight simulation journeys. Serialize Unity processes. Keep all checks isolated while hands-on testing is deferred. No phase is player accepted without evidence from the user.

## Current execution

Phase 1 foundation automatically verified; human acceptance remains pending. Validation `Builds/validation/20260908T163354807489Z/summary.json` passes 228 EditMode / 17 PlayMode tests without compiler diagnostics. Release build `20260908T163517Z` has artifact tree SHA-256 `2c4d49e76e79d311bce3360a14ff2c49aa579f60974bea4e53c00d26fb8f72f1`.

Isolated player evidence on that artifact:
- `20260908T163703831210Z-visual-smoke`: real physical approach from outside scan range, completed scan, surface clearance, origin preservation, moving-planet orbit capture and released coasting pass at 720p.
- `20260908T163819518979Z-hierarchy`: full-limit fixture hierarchy passes at 1440p using direct Wayland/Vulkan rendering. This is functional smoke, not performance qualification.
- `20260908T163840794834Z-demo-journey`: create/export/restart passes at 720p, preserving v1 bytes and saved artifacts.

Failed compiler, old arrival-anchor, stalled editor and initially rejected planet-approach runs remain preserved. The stalled editor was retried on unchanged source; missing results were not accepted. A verified orphan player from an older interrupted stress run was stopped, with cleanup evidence alongside its original incomplete report.

Phase 2 propulsion/contact foundation automatically verified. Validation `Builds/validation/20260908T165322417483Z/summary.json` passes 234 EditMode / 19 PlayMode tests with no compiler diagnostics. Build `20260908T165450Z` has tree SHA-256 `dcad7aa1950a74ab2db4fb6fcff29718f952f9e32d5574698d09586376d5c7d1`. Player run `20260908T165837408568Z-visual-smoke` passes real approach/scan, capture/coast, high-speed collision recovery, cockpit-button resume, and unchanged Survey/activity state. Create/restart `20260908T170118234641Z-demo-journey` passes on the same artifact.

The reference ship now has explicit mass/inertia and force/torque envelopes. Assist controls velocity with finite counterthrust; Mastery retains momentum under gravity. The former Assist free-fall test now exercises Mastery, while a core test proves insufficient propulsion cannot cancel excessive gravity. Gravity has no ordinary player toggle. Recovery holds a residency reference and cannot start navigation until explicitly resumed. This is not new release/performance or human acceptance evidence.

Phase 3 orbit/trajectory foundation automatically verified. Validation `Builds/validation/20260908T173135471202Z/summary.json` passes 238 EditMode / 21 PlayMode tests without compiler diagnostics. Isolated circular integration covers ten periods with bounded radius and energy drift; tidal-band and insufficient-thrust rejection preserve prior guidance. Moving-body approach uses relative velocity. Planet and satellite layout reserves conservative periapsis Hill envelopes, and physical moons now have their own gravity.

Build `20260908T173403Z` has artifact tree SHA-256 `608c3318369776f004ac3d650737df9cae552a87ea9d592a8835059a65329212`. Evidence on that artifact:
- `20260908T173527910348Z-visual-smoke`: passes real approach/scan, moving-planet capture and released coast, moon capture and released coast, trajectory availability, high-speed planetary recovery, slow stellar exclusion recovery, explicit cockpit resume, unchanged progression, and rebasing at 720p.
- `20260908T173715648448Z-hierarchy`: full-limit provider fixture passes using Wayland/Vulkan. No performance qualification was sampled.
- `20260908T173810411130Z-demo-journey`: creation/export and restart pass, preserving v1 sentinel bytes and saved creations/replays.

The 720p flight and orbit captures were visually inspected: new altitude, radial speed, closing stopping distance, periapsis/apoapsis, period, and prograde/retrograde instruments are visible. They remain two-body estimates, not guaranteed trajectories in the combined field. Full cockpit station integration and three-resolution acceptance remain phase 5 work.

A further phase 1 precision defect was found and repaired: offset Unity hierarchy parents can quantize otherwise double-precision ephemerides. Celestial organizational frames now stay at the render origin, and static gravity anchors/ephemerides restore logical positions after rebasing. A real Unity test exercises nested frames at 80 million metres and repeated between-tick shifts while preserving ship velocity. Failed player run `20260908T172958318990Z-visual-smoke` exposed an extra clock advance during rebasing; refreshing now preserves the last simulated time. The failed report remains intact, and the succeeding run above verifies the correction.

Phase 4 has started with destination staging: validated loads remain outside the active snapshot scene set and resident graph until the current receipt is committed exactly once. Cancellation (including a provider that ignores the caller token), superseding requests, cross-session receipts and wrong snapshot namespaces are rejected. This is a loading foundation; cruise, jump UI/physics integration, transition cancellation and the 60–120 second journey are still open. Phases 4–6 and full original qualification remain open. Source is the working tree rooted at `775f826f51ee70d8f25e3e421887eaa5a6f10801`, not a clean accepted revision.


### Cruise vertical slice (2026-09-08)

The next release build `20260908T182530Z` has artifact tree SHA-256 `140c19880bf0d43483d10dfcb1817c6e7c04224af1d7058a31f0cd43ab50dc9a`. Its validation `20260908T182409138333Z` passes 261 EditMode / 21 PlayMode tests with no compiler diagnostics. New plain-core contracts provide session/operation/snapshot-bound travel transitions, three-second charging, cancellation in every transition, exact-once arrival, published route direction/eligibility, staged destination receipts, and a double-precision fictional cruise model. The current Unity integration exposes automatic/manual **system cruise** and safe cancellation from the command console. Route-based jumps are not yet connected to this transaction path; the old fast-travel path must still be retired before phase 4 can close.

Dedicated `scripts/simulation_journey.py --resolution 1280x720` run `20260908T182719194973Z-simulation-journey` passes real propulsion departure from open-source AI, charging/cruise cancellation, and cruise/approach to right-to-repair. Departure plus successful cruise/approach takes 81.7569 simulated seconds, ending with 304.4558 m clearance and 15.4249 m/s relative closing speed. It records 669 validated logical-position/clearance samples, with no recovery or progression mutation. The harness explicitly runs simulation time at 4x; this is functional trajectory evidence, not a real-time performance measurement or human acceptance. Its 720p arrival capture was visually inspected.

The same artifact passes the full-limit fixture hierarchy (`20260908T182859011264Z-hierarchy`) and creation/restart (`20260908T182928214606Z-demo-journey`). Modal capture now pauses local simulation, preserves velocities, restores enabled flight/navigation components and previous menu state, and displays a pause notice. The simulation player verifies pause/restore and the existing creation/restart player verifies its ordinary use. This resolves the former component-restoration defect; complete cockpit station/input-context acceptance is still open.

Remaining phase 4 work includes actual route-jump arrival/loading integration, retiring direct fast-travel bypasses, comprehensive manual overspeed/cancellation player evidence, live/offline traversal and consistent system frames. Remaining phase 5 work includes all cockpit stations, shared input ownership, surface-safe activity placements, transition/progression guards and full catalogue journeys. Phase 6 and deferred human/controller acceptance remain open.


### Surface-safe campaign and renewed validation (2026-09-08)

Validation `20260908T190335925922Z` passes **265 EditMode / 22 PlayMode** tests with no compiler diagnostics; all nine Python harness tests pass. Both players rebuild successfully in `Builds/build-logs/20260908T190449Z/`. The release artifact tree SHA-256 is `aa0c0a1ce4f7a73e5adbc8629b94ddb7522ecc349cb018bb8ca0df2d42cb2ba6`; source remains `775f826f51ee70d8f25e3e421887eaa5a6f10801-working`.

Static live bodies now initialize from explicit logical anchors, including loading after rebasing. Reused bodies no longer inherit velocity from disabled pooled orbits. The new test covers a fractional logical anchor after a large origin shift. Failed validation `20260908T185543552151Z` is preserved: EditMode aborted inside Unity/Mono named-pipe startup before results existed, while one PlayMode assertion incorrectly assumed a zero render origin. Corrected frame-aware assertions pass in `20260908T190126064525Z` and the subsequent full validation above.

Campaign observation distance now uses clearance from the physical or stellar exclusion surface, with negative clearance rejected. Authored anomalies are placed 160 m outside that surface; anomaly discovery requires proximity and looking toward the actual visible anomaly. Reduced-motion placement is stationary. Tests cover 120 m and 300 m planetary radii and 2250 m and 2750 m safety radii. The campaign harness places observations outside the enlarged safety volumes and labels its placement as fixture evidence; it does not establish real-propulsion campaign traversal.

The earlier release `20260908T184846Z` produced a **failed** 30-second active-feature stress preflight at `Builds/evidence/20260908T185314929575Z-hierarchy/`. Its 3420 samples include CPU p95 10.684 ms, GPU p95 0.719 ms, maximum ordinary frame 12.909 ms, and 32 B/frame ordinary-flight managed allocation. Player peak memory is 1,208,492,032 B, GPU peak 432,001,024 B; warm overview 0.0125 s and cache 14,915,595 B. Failure reasons remain build provenance unavailable and nonzero allocation. These measurements used explicitly recorded direct Wayland/Vulkan rendering, not the default threaded renderer. This historical 30-second report neither measures the newest artifact nor replaces full 600-second release qualification. Allocation attribution and correction remain open.


### Cockpit allocation repair and campaign evidence

Release `aa0c0a1ce4f7a73e5adbc8629b94ddb7522ecc349cb018bb8ca0df2d42cb2ba6` passes the full campaign create/restart run `20260908T190810489354Z-demo-journey` at 720p, including all eleven campaign activities, observatory displays, designs/exports and byte-identical v1 sentinels. The notebook capture was visually inspected. Range/placement is explicitly fixture-assisted; real-propulsion campaign travel and complete cockpit presentation remain unverified.

Subsequent code review found cruise action creation duplicated in `ProbeHud.Update` and `ReadModel`, in addition to its proper presentation-refresh location. The per-frame path creates a closure and can grow the button pool while an eligible body is selected. Those two copies are removed; only the refresh path builds cruise controls. The simulation journey now requires a stable action-pool count over open and closed menu frames. Anomaly angular motion is also scaled so exterior placement around large stars stays below 15 m/s relative to the anchor, with deterministic reduced-motion behavior.

Validation `20260908T191319244722Z` passes 265 EditMode / 22 PlayMode tests without compiler diagnostics. Both builds in `20260908T191533Z` pass their build gates. Development artifact tree SHA-256: `5768a6817c246f8894da1e6d8860a64bcbf6b3225fe3a415da949b065556dd8e`. Release artifact tree SHA-256: `cfa59860fda9033e38e9ecdd09a9d2fcea7f868c1db6a955198c2aca14dc006b`. These remain working-tree artifacts, not a qualified clean-source release.


The new release passes `20260908T191848900864Z-simulation-journey`: stable action-pool count, modal pause/restore, departure, charge/cruise cancellation, safe arrival, no recovery and no progression mutation. Departure plus successful cruise is 81.7472 simulated seconds at the recorded 4x harness time scale, with 687 valid trajectory samples, final clearance 304.4559 m and relative closing 15.4190 m/s.

Active-feature stress preflight `20260908T191949455232Z-hierarchy` confirms the allocation correction on that release: **3343 samples over 30.0115 seconds, zero managed allocation**, CPU p95 **11.3628 ms**, GPU p95 **0.6937 ms**, and maximum ordinary frame **14.9205 ms**. The sole report failure is **Build provenance unavailable**. The earlier allocation-failure report remains preserved. This direct Wayland/Vulkan preflight does not qualify the release, replace 600-second stress/live measurements, or establish default-renderer measurement coverage.


The same release artifact also passes the renewed full campaign/create/restart player run `20260908T192115270340Z-demo-journey` at 720p after the bounded anomaly-speed and HUD fixes. Both processes pass, exports and saved records restore, and v1 sentinel bytes remain unchanged. Current automated evidence is therefore tied to release tree `cfa59860fda9033e38e9ecdd09a9d2fcea7f868c1db6a955198c2aca14dc006b`. Human acceptance, real-flight campaign traversal and complete phase 4–6 gates remain open.


### Authoritative charged jumps and system inspection (2026-09-08)

The ordinary Unity fast-travel action now starts a three-second, session/operation/snapshot-bound jump. Destination loading is staged, cancellation and provider failure retain the current usable scene, and only the exact session-issued preparation can authorize arrival. Replayed, superseded, expired, cross-session and unsafe arrival receipts fail. The core tests include a provider that ignores cancellation and failure/retry through cached outcomes. Explore travel leaves Survey and activity progress unchanged. The internal legacy FastTravelIntent still exists, but the Unity action no longer uses its direct-arrival path.

Destination choices use validated resident identities and route direction rather than render-object presence. The cockpit list preserves Survey anonymity, supports filtering and pagination, and permits return travel after the previous star is pooled. Loading another system for inspection does not replace the current physical system; discussion and thread inspection do not move the ship. Current map geometry is retained while a physical system is active; a separately updated chart adapter remains open. Live jumps re-enter the shared central stellar envelope using logical coordinates, while new Kepler bodies initialize at the current simulation clock.

Validation `20260908T201135436663Z` passes **268 EditMode / 22 PlayMode**, with no compiler diagnostics. All **10 Python harness tests** pass. Both builds in `20260908T201405Z` pass: development tree SHA-256 `b07d1b4c7b2d9fe6bdd1b2ef5f3c802e22be61a77e14afa26c27ff1035d78954`; release tree SHA-256 `9b8ca2c0e3db3fbfbf721f40bf0079f06c465c95d1ed07bdf0a0418fb0ec9ed3`. Source remains the working tree rooted at `775f826f51ee70d8f25e3e421887eaa5a6f10801`, not a clean accepted revision.

Historical jump evidence: demo simulation `20260908T194503746562Z` passes charge cancellation, safe demo jump, unchanged progression and the 81.75-simulated-second cruise. Fixture hierarchy/jump `20260908T195200159253Z` passes on its earlier artifact. These do not qualify the newest release. Validation failures at `20260908T193341276112Z`, `20260908T193735725484Z` and `20260908T193938949544Z` remain preserved; fixes addressed a deprecated Unity lookup, an empty fixture ID and old direct-teleport test assumptions without removing the behavioral journeys.

Round-trip run `20260908T200927841118Z-hierarchy` passed online but failed cached thread presentation on its older release. The offline process sent **zero provider requests**. Investigation found synchronous cache completion could precede inspection-root initialization; bootstrap now establishes the thread listener before the navigation callback. The failed report is retained. The harness now exits nonzero promptly on this failure. Fixture child IDs are globally distinct between systems; offline fixture launches keep their namespace and explicit offline provenance.

Still open: full phase 4 manual overspeed/transition player coverage; rollback from an unexpected Unity materialization failure after the core commits; separate live chart presentation; phase 5 cockpit/input ownership and real-flight catalogue adaptation; all phase 6 clean-source, 600-second stress/live, Ubuntu, CI and deferred human acceptance gates. These implementations and functional reports do not qualify the beta.


Cached retest `20260908T201736092441Z-hierarchy` still failed thread presentation (zero offline requests). The first subscription-order correction was insufficient: the earlier bootstrap-owned `LiveGalaxySelectionController` listener could still request data before inspection initialization. Its initialization now belongs to the authoritative session setup, after inspection/session selection listeners and navigation are bound. Full validation `20260908T201904651047Z` again passes 268 EditMode / 22 PlayMode without compiler diagnostics. Both failed cached runs remain preserved pending renewed player evidence.


The corrected bootstrap passes the full online and cache-only round trip in `20260908T202522422929Z-hierarchy`, on release tree `2367370916d7ad3e157fe2061d868379c9c53ee0e716ce57bf93d9e2c0d2b430`. Both processes complete the hierarchy and outbound/return jumps with unchanged progression and safe arrivals; the offline process makes **zero provider requests**. This closes the observed cached-thread initialization defect.

Subsequent review found that arrival target selection could reopen data inspection and the map. The Unity selection adapter now provides an explicit arrival-selection path that preserves target notifications while suppressing inspection. The player harness additionally requires a closed map, enabled flight controls and a dynamic Rigidbody after both arrivals. Validation `20260908T202550818357Z` passes 268 EditMode / 22 PlayMode without compiler diagnostics for this final code change; renewed artifact/player results follow separately.


### Final artifact for the charged-jump chunk

Build `20260908T202809Z` passes for both configurations after the arrival-selection correction. Development tree SHA-256: `f0504fde62cd1ff552cbac6b31a977dbcbefd160773ff7b91a627286acb0456a`. Release tree SHA-256: `de13cb9ff3b3cbc8c0c957e1ea4a1d8ca5d8e062c63a7d566bb8859b1b453131`. Per-file checksums are in each build JSON. Final-source validation is `20260908T202550818357Z` (268 EditMode / 22 PlayMode, no compiler diagnostics), with ten Python tests passing and a clean whitespace check.

`20260908T203201711402Z-hierarchy` passes the full fixture hierarchy and cancelled/retried outbound/return jumps at 2560×1440, then repeats successfully after cache-only restart. Both arrivals assert safe clearance, unchanged progression, a closed map, a dynamic ship and enabled flight controls. The online fixture receives eight requests; the second process receives **zero**. Both exit zero, with no runtime errors. This uses explicitly recorded direct Wayland/Vulkan rendering and is functional evidence, not performance measurement or human acceptance.


Fresh public run `20260908T203348169321Z-hierarchy` **fails** on revision **238**, catalog **14**. The overview loads (300 nodes / 180 links), but community `c:new:c3875a74587b5df7` is rejected with `BOUNDED SCOPE REJECTED / CURRENT VIEW RETAINED`; the hierarchy deadline exits the player with code 2. A single bounded diagnostic GET of the same pinned community returns HTTP 403 (`diagnostic-read.json`). No backend was changed, validation was not weakened, and public hierarchy/jump compatibility remains unresolved. This result is distinct from passing deterministic online/offline fixture journeys.


### Interpolated-frame correction discovered by settled arrival checks

A strengthened demo assertion invalidated the earlier safety-only arrival claim. `20260908T204028268785Z-simulation-journey` measures **82,095 m** exclusion clearance after the demo jump: safe, but nowhere near the intended 500 m handoff. It remains a failed report. The focused `InterpolatedShipJumpAndRebasePreserveDestinationClearance` Unity test reproduces the defect in failed validation `20260908T204159351918Z`: an interpolated ship retains a pose from the old render frame and can trigger an additional origin shift.

FloatingOrigin now checks physical Rigidbody position and synchronizes physical/render poses while resetting interpolation history during a frame change. Nested celestial frames use the same Rigidbody shift operation. Full validation `20260908T204349936531Z` passes **268 EditMode / 23 PlayMode**, including the new reproduction and existing nested-frame precision/velocity checks, with no compiler diagnostics. The demo harness now measures destination-relative clearance and speed after several physics ticks and requires 450–550 m exclusion clearance, relative speed below 20 m/s and active local controls. Final rebuilt player evidence follows; historical safety-only jump reports do not establish correct destination proximity.


### Corrected frame artifact and settled demo arrival

Both builds in `20260908T204455Z` pass without compiler diagnostics. Development tree SHA-256 is `0d5e87d70b3c170343536c33507766a139b11d9c9d91916c9a653b69b2d0990c`; release tree SHA-256 is `4132dab1b5b5f3f12bac37fbd8c2f4d42c1803e7ffabc49d852c346a18335387`. They correspond to the 268 EditMode / 23 PlayMode validation at `20260908T204349936531Z`, with source still labelled `775f826f51ee70d8f25e3e421887eaa5a6f10801-working`.

The strengthened 720p player journey `20260908T204844082880Z-simulation-journey` **passes**. Departure/cruise takes **81.7479 simulated seconds**, with 687 valid trajectory samples, 304.456 m final exclusion clearance and 15.419 m/s closing speed. The cancelled/retried demo jump then settles at **499.782 m** outside the destination exclusion boundary and **1.143 m/s** relative speed, with active local flight, no recovery and unchanged progression. The 720p capture was inspected: the HUD correctly shows approximately 750 m physical surface altitude (the star adds a 250 m exclusion margin). The recorded cruise harness time scale is 4x; these are functional measurements, not performance or human acceptance.


Final-artifact fixture run `20260908T204939547797Z-hierarchy` also **passes** on release tree `4132dab1b5b5f3f12bac37fbd8c2f4d42c1803e7ffabc49d852c346a18335387`. It completes the hierarchy and cancelled/retried round-trip jumps, then repeats after cache-only restart with **zero provider requests**. Both directions retain safe placement, unchanged progression and usable local-flight controls. This is 1440p direct Wayland/Vulkan functional evidence. All failed/historical reports remain preserved, and the beta is still unqualified.


### Cockpit stations and full-campaign scale regression

The session now owns cockpit command/chart/modal input policy. Eight retained feature views use a common projected cockpit surface; the chart uses a separate render texture and leaves the physical ship and primary cockpit camera active. Validation `20260908T211347377205Z` passes both suites (270 EditMode / 23 PlayMode), with no compiler diagnostics. Release build `20260908T212102Z` succeeds.

The 720p simulation journey `20260908T212246383613Z-simulation-journey` passes chart physics/camera assertions, modal restoration, action pooling, departure, cruise cancellation and safe cancelled/retried jumps. The nearby-planet trip takes 81.7436 simulated seconds; settled jump clearance is 499.783 m at 1.143 m/s relative speed. Chart, studio and observatory 720p captures were visually inspected; this is automated/visual evidence, not human acceptance.

Full campaign/flight/expedition attempt `20260908T212341395455Z-demo-journey` fails. The creation, campaign and observatory screens are reached, but legacy presentation fixture positions enter enlarged bodies, and scenic endpoints still used a 250 m centre offset. Recovery then prevents the old flight fixture from starting, exposing a null access in its diagnostic path. The isolated failed process was stopped after recording the exception; its report remains preserved.

Corrections under verification: safe scenic corridors and bounded practice-space search, runtime hazard cancellation, course version 2, distance-to-corridor tracking for long scenic segments, and expedition stops measured beyond body exclusion volumes. Celebration/expedition fixtures now use safe radial offsets. The chart core no longer emits obsolete pause/resume momentum effects. New player checks cover manual overspeed drops, approach cancellation and target unavailability. These changes require fresh validation/build/player evidence before any gate closes.

Validation `20260908T212920185223Z` caught the old hard-coded competitive ghost version (270/271 EditMode); its stalled PlayMode process was stopped and remains failed. `20260908T213438995060Z` records an editor startup crash and a missing qualified HUD namespace, now corrected. The compatibility gate uses `FlightCourse.CurrentVersion`, with an explicit test that version-1 packages remain readable but cannot be selected competitively. None of these failed attempts counts toward completion.


Post-correction validation `20260908T213513255513Z` passes 271 EditMode / 23 PlayMode without compiler diagnostics. Both builds succeed in `20260908T213834Z`. Release tree `4f100570d72cbbcf4688d542161592b7aa06d0cf7ca79ea923353314379d9fe0` passes the expanded 1080p simulation journey `20260908T214225380670Z-simulation-journey`, including real manual overspeed drop, Approach cancellation and target-loss abort. The chart and local-navigation core now agree that the map is closed after arrival. Full campaign/restart evidence is still being rerun; its older failed result remains open until the new run passes.

The post-correction full campaign run `20260908T214327997497Z-demo-journey` remains failed: the new activity guard correctly rejects start during recovery. The remaining cause is the celebration screenshot fixture's bounds-based camera distance, which can still lie inside the 2 km star. That fixture now includes the stellar exclusion radius in camera distance and validates the chosen location against all active bodies before positioning. This changes diagnostic setup, not recovery behavior or activity eligibility.

Run `20260908T214859450365Z-demo-journey` passes the earlier unsafe placements, all three scenic profiles, playground completion, recording save and replay viewing, then fails the ghost-show assertion. The fixture creates a very short personal-best replay, which can expire during controller focus traversal now that command menus correctly leave physics running. The assertion now verifies the enabled preference and either current rendering or confirmed recording expiry, then starts fresh practice and requires the ghost to render again. Hiding a live ghost and origin-shift alignment remain required. This corrects a test assumption without extending or looping recordings in gameplay.


Final station-layer source validation `20260908T220930135997Z` passes 271 EditMode / 23 PlayMode without compiler diagnostics. Build `20260908T221121Z` succeeds for both configurations: development tree `a811f899b4704b4ad9eea9c4ca5accf709737a87b20d5788f6a7f4143d175a74`, release tree `ede6423d9f5258cb9ae1bae10fae8ec0e92d45e5cf0ac5053d8adfcc04a555dd`. The pilot and chart cameras exclude the dedicated diorama layer; a player assertion enforces the pilot-camera boundary. Final-artifact resolution and full-journey results follow below.


Final release tree `ede6423d9f5258cb9ae1bae10fae8ec0e92d45e5cf0ac5053d8adfcc04a555dd` passes the full **2560×1440** campaign/flight/expedition journey `20260908T221439102982Z-demo-journey`: create 301.286 s, independent expedition resume 80.579 s, restart 37.791 s. All three processes exit zero and preserve v1 sentinels; required v2 state, postcards and replay packages satisfy the restart checks. The populated observatory capture was inspected after the long flight/rebase sequence: its models remain inside the dedicated view, with readable controls and no prior camera-layer leakage. Virtual-controller and fixture coverage remain distinct from real-controller acceptance and physically flown scored challenges.

Final-artifact fixture run `20260908T222151912609Z-hierarchy` passes the complete online hierarchy and cancelled/retried round-trip jumps, then passes a cached restart with **zero network requests**. It uses recorded direct Wayland/Vulkan rendering and is functional evidence, not public-provider compatibility or performance qualification.

The final artifact also passes creation/restart journeys at **1280×720** (`20260908T222234308966Z-demo-journey`) and **1920×1080** (`20260908T222337171755Z-demo-journey`). Together with the full 1440p campaign run, these establish automated resolution coverage for the shared cockpit surfaces and core creation/observatory flow. The final 720p studio capture was inspected for control legibility; exhaustive human screen acceptance remains deferred.

Final-artifact replay recipient run `20260908T222454459002Z-recipient-import` passes in a separate `replay-recipient` profile, creates **no progression files**, preserves source replay bytes and v1 sentinels, and reports no runtime errors.

Final 1440p simulation run `20260908T222545840242Z-simulation-journey` passes on release tree `ede6423d9f5258cb9ae1bae10fae8ec0e92d45e5cf0ac5053d8adfcc04a555dd`. The trip takes 81.7482 simulated seconds, with 679 finite safe samples, 304.453 m destination exclusion clearance and 15.322 m/s closing speed. Settled jump clearance is 499.784 m with 1.143 m/s relative speed. Manual overspeed drop, approach/target-loss cancellation, chart physics/camera stability, modal restoration and unchanged progression all pass. These functional fixtures remain distinct from 600-second performance qualification.


Final active-feature stress preflight `20260908T222651869392Z-hierarchy` remains **failed / unqualified** with the single report failure **Build provenance unavailable** (the source identifies an uncommitted working tree). The thresholds were not changed. At 2560×1440, recorded direct Wayland/Vulkan rendering, it completes hierarchy smoke and measures 30.0129 seconds / 3,352 samples, all with valid attributable GPU timing and no invalid samples. CPU p95 is 10.9924 ms; GPU p95 0.5963 ms; maximum ordinary frame 15.9506 ms; all ordinary-flight managed allocations are zero. Peak player RSS is 1,326,415,872 bytes; GPU requested buffers 486,039,552 bytes. Cache usage is 18,225,621 bytes; verified warm cached-source open takes 0.02328 s (not process-to-visible-overview timing).

The scenario records 14,053 resident nodes / 57,000 resident routes across retained scopes, moving flight over 878.286 m, guidance and ghosts throughout the window, 893 ghost-in-view frames, 3,300 audio frames, and 2,226 celebration frames. Maximum active voices and celebrations are both three. This is a short numerical regression check, not either required 600-second clean-artifact qualification run. Failed provenance, public-provider compatibility, clean Arch/Ubuntu evidence, CI activation and deferred human/physical-flight acceptance keep the beta unqualified.
