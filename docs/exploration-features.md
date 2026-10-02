# Exploration feature catalogue

Status: accepted scope; implementation and acceptance are in progress. Creative direction: **cozy cosmic curiosity**. All twelve features are mandatory for the expanded Linux beta; existing qualification gates remain mandatory.

Current evidence is summarized in the [feature acceptance checkpoint](feature-acceptance-checkpoint.md). Counts and runs in the history below remain tied to their original artifacts; no entire feature is player accepted by a fixture result.

Status vocabulary: **proposed** = specified; **implemented** = code/content exists; **automatically verified** = linked passing checks; **player accepted** = linked required player evidence. Partial foundations do not complete a feature.

## Shared contract

Unity 6000.4.10f1 and locked packages remain unchanged. Story content belongs to the deterministic demo; live tools never invent source entities or relationships. Activities are optional. Preserve standard flight and scanning, scanner upgrades, contracts, the existing constellation and easter egg. Controller confirm/back and keyboard/mouse parity apply; text may use keyboard. Modal input suppresses flight. All v1 files remain untouched. Public publishing, servers, accounts and network submission are excluded.

## F01 — Signal tuning

- **Status:** implemented; automatically verified in the current linked checkpoint; scripted player/restart passed at 720p, 1080p and 1440p; hardware and manual comfort acceptance pending.
- **Player purpose:** Feel a signal resolve through careful aim.
- **Controls:** Hold R / controller West for the existing 1.5 seconds; aim within five degrees for cumulative 0.75 seconds for optional clean tuning. Automatic tuning is available.
- **Content inventory:** One moving reticle, clean-signal audio/visual flourish, practice in Explore.
- **Dependencies:** Scanner; committed Survey outcomes; journal.
- **Persistence:** Clean distinction keyed by committed scan; practice never changes Survey.
- **Provenance:** Tuning is an authored instrument effect.
- **Accessibility:** Automatic tuning, visual/audio parity; ordinary scanning never requires the sweet spot.
- **Acceptance:** Ordinary scan remains 1.5 seconds; missed tuning never blocks rewards; failed saves never celebrate.
- **Evidence:** [automated regression source](../Assets/ClustrGalaxy/Tests/EditMode/ActivityTests.cs); [artifact-specific runs and unresolved gates](beta-completion.md). Player acceptance is pending.

## F02 — Curiosity trails

- **Status:** implemented in part. All twelve authored clues, observation timing, notebook entries, follow/unpin, keepsakes and fixture-based campaign create/restart are verified. Normal-play discoverability, trail pacing and player acceptance remain open.
- **Player purpose:** Follow small mysteries at your own pace.
- **Controls:** Discover by exploration; Follow / Unpin in Actions.
- **Content inventory:** Three four-clue trails: repeating harmonic, wandering beacon, archival lights. Two target 10–15 minutes; one spans sessions. Three keepsakes.
- **Dependencies:** Authored triggers; journal; observatory.
- **Persistence:** Current clue, observations and keepsake survive restart.
- **Provenance:** Demo fiction only.
- **Accessibility:** No countdown or missed-day penalties; textual clue equivalents.
- **Acceptance:** All twelve clues reachable; abandon/resume safely; one keepsake per trail.
- **Evidence:** [automated regression source](../Assets/ClustrGalaxy/Tests/EditMode/CampaignTests.cs); [artifact-specific runs and unresolved gates](beta-completion.md). Player acceptance is pending.

## F03 — Scenic route flight

- **Status:** implementation in progress: plain-core course evaluation, retained route/gate presentation, contextual start/preview/cancel controls, session residency and durable flight records. 149 EditMode / 13 PlayMode checkpoint passes; 720p scenic/playground/record/restart integration passes with fixture placement and an actual autopilot preview. Opt-in personal-best ghosts, atomic score/recording persistence, translated-course playback and decorative training bodies are implemented. The 720p ghost restart/origin-shift journey passes. Normal flight/live acceptance and full acceptance remain unfinished.
- **Player purpose:** Enjoy connections as flight experiences.
- **Controls:** Explicit scenic-route action; Autopilot preview, normal Assist/Mastery controls; departure cancels guidance.
- **Content inventory:** Broad current, winding passage, edge-skimming ribbon.
- **Dependencies:** Navigation; resident ownership; double/anchor coordinates.
- **Persistence:** Only explicit journal moments; active route may be resumed if eligible.
- **Provenance:** Authored courses never change source endpoints, weights or labels.
- **Accessibility:** Unscored preview; no penalty on departure.
- **Acceptance:** Only resident, visibility-eligible endpoints; missing routes unavailable; cancel releases ownership.
- **Evidence:** [automated regression source](../Assets/ClustrGalaxy/Tests/EditMode/FlightActivityTests.cs); [artifact-specific runs and unresolved gates](beta-completion.md). Player acceptance is pending.

## F04 — Orbital playgrounds

- **Status:** implementation in progress: plain-core course evaluation, retained route/gate presentation, contextual start/preview/cancel controls, session residency and durable flight records. 149 EditMode / 13 PlayMode checkpoint passes; 720p scenic/playground/record/restart integration passes with fixture placement and an actual autopilot preview. Opt-in personal-best ghosts, atomic score/recording persistence, translated-course playback and decorative training bodies are implemented. The 720p ghost restart/origin-shift journey passes. Normal flight/live acceptance and full acceptance remain unfinished.
- **Player purpose:** Practice expressive flight and improve personal records.
- **Controls:** Practice, Start, Restart and Cancel; normal flight controls.
- **Content inventory:** Eight-gate ring run, two-body figure eight, 20-second stable orbit; ghost and time/accuracy.
- **Dependencies:** Course evaluator; replay recorder; flight modes.
- **Persistence:** Separate Assist and Mastery records and explicitly saved ghosts.
- **Provenance:** Authored challenge geometry.
- **Accessibility:** Autopilot unscored preview; missed gate guides rather than restarts.
- **Acceptance:** All three complete; mode records separate; origin shifts do not alter scores.
- **Evidence:** [automated regression source](../Assets/ClustrGalaxy/Tests/EditMode/FlightGhostTests.cs); [artifact-specific runs and unresolved gates](beta-completion.md). Player acceptance is pending.

## F05 — Discoveries as instruments

- **Status:** implemented in part; mappings/preferences verified, audio/player/performance gates pending.
- **Player purpose:** Hear and play personal discoveries.
- **Controls:** Play/pause, tempo, volume and node audition in Actions/observatory.
- **Content inventory:** Fixed pentatonic identity mapping, bundled instruments, eight voices maximum.
- **Dependencies:** Audio pool; constellation read model; visibility.
- **Persistence:** Music preferences and selected preset.
- **Provenance:** Artistic sonification, never source sentiment/activity analysis.
- **Accessibility:** Mute, reduced audio, visual pulses.
- **Acceptance:** Stable mapping across restarts; hidden Survey identities silent; voice cap enforced.
- **Evidence:** [automated regression source](../Assets/ClustrGalaxy/Tests/EditMode/ActivityTests.cs); [artifact-specific runs and unresolved gates](beta-completion.md). Player acceptance is pending.

## F06 — Expressive personal constellations

- **Status:** implemented; core/design/navigation checks pass. Scripted Linux create/music/postcard/restart passed at 720p, 1080p and 1440p. A separate controller-driven editor diagnostic exercised all six palettes and three presets; complete UI diagnostic measurements pass on recorded native Wayland/direct Vulkan rendering. Real-controller and full boundary/accessibility acceptance remain pending.
- **Player purpose:** Make a meaningful personal creation.
- **Controls:** Mouse drag or controller node selection/movement; connect nodes; explicit Save; separate queue request/confirm/cancel.
- **Content inventory:** 3–12 identified members; 2D arrangement, personal edges, six accessible palettes, music preset; 1920×1080 PNG postcard.
- **Dependencies:** Existing Survey constellation records; companion persistence; export.
- **Persistence:** Layouts and music in v2 companions; existing membership/queue unchanged.
- **Provenance:** Personal interpretation; optional reason and provenance on export.
- **Accessibility:** Keyboard/mouse/controller parity except text entry.
- **Acceptance:** Invalid membership/edges rejected; saved creation restores; PNG dimensions exact.
- **Evidence:** [automated regression source](../Assets/ClustrGalaxy/Tests/EditMode/ActivityTests.cs); [artifact-specific runs and unresolved gates](beta-completion.md). Player acceptance is pending.

## F07 — Authored anomalies

- **Status:** implemented in part. Six deterministic discoveries and original custom mesh prefabs now have distinct presentation animations. Reduced-motion and allocation checks pass in the 182 EditMode / 15 PlayMode checkpoint. Nearby bounded artistic notes and matching emission cues are implemented. Final art review and ordinary-play acceptance remain open.
- **Player purpose:** Encounter gentle surprises.
- **Controls:** Proximity, observation or tuning; no combat or forced timing.
- **Content inventory:** Echo scanner, shy satellite, miniature orrery, illuminated station, lantern cluster, upside-down beacon. Three standalone; three linked clues.
- **Dependencies:** Authored definitions, deterministic placement, journal.
- **Persistence:** One discovery entry per anomaly; ambient behavior repeatable.
- **Provenance:** Demo fiction only; no source entity impersonation.
- **Accessibility:** No punitive timing; text/visual equivalents.
- **Acceptance:** Six reliably reachable anomalies; no random spawn required; no duplicate discovery.
- **Evidence:** [automated regression source](../Assets/ClustrGalaxy/Tests/EditMode/CampaignTests.cs); [artifact-specific runs and unresolved gates](beta-completion.md). Player acceptance is pending.

## F08 — Probe observatory

- **Status:** implementation in progress. Six durable display slots, explicit clear with original retention, custom platform/keepsakes, saved constellation layouts/palettes, actual recorded flight paths, copied journal captions, retained modal input, music and child replay return are implemented. Storage failure/retry checks pass; all-slot 720p restoration and diary child-view return passed. Final artwork review and player acceptance remain open.
- **Player purpose:** Keep a home for discoveries.
- **Controls:** Actions opens observatory; select display slots, music and replays; Back restores prior input/view.
- **Content inventory:** 3D diorama, six slots for keepsakes, constellations, records and diary moments. No walkable interior.
- **Dependencies:** Activity read models; local assets; replay and music.
- **Persistence:** Six display selections and preferences.
- **Provenance:** Labels distinguish personal creations, authored keepsakes and source data.
- **Accessibility:** Flight suppressed; controller focus; reduced motion.
- **Acceptance:** All slot types work; restart restores; no input leakage or focus trap.
- **Evidence:** [automated regression source](../Assets/ClustrGalaxy/Tests/EditMode/ObservatoryTests.cs); [artifact-specific runs and unresolved gates](beta-completion.md). Player acceptance is pending.

## F09 — Mood-based expeditions

- **Status:** implementation in progress. Session-selected eligible stops, published-connection traversal, three-course Pilot Practice, pause/resume/abandon, persisted progress and explicit closing cards are implemented. The three-process 720p fixture journey verifies pause across restart, all three moods, unavailable Survey connections and Explore isolation. Approximately fifteen-minute pacing and ordinary-flight player acceptance remain open.
- **Player purpose:** Find a satisfying short session.
- **Controls:** Select Quiet Drift / Unexpected Connection / Pilot Practice; pause, resume, abandon.
- **Content inventory:** Three scenic stops; up to four published connections; three playground challenges. Target 15 minutes, honest shorter option.
- **Dependencies:** Eligible resident graph; activities; journal.
- **Persistence:** Frozen snapshot plan and progress; no auto-start successor.
- **Provenance:** No invented live destinations or edges.
- **Accessibility:** No penalty on pause/abandon; quiet option.
- **Acceptance:** All moods finish with closing card; insufficient data labelled; restart resumes.
- **Evidence:** [automated regression source](../Assets/ClustrGalaxy/Tests/EditMode/ExpeditionTests.cs); [artifact-specific runs and unresolved gates](beta-completion.md). Player acceptance is pending.

## F10 — Connection mysteries

- **Status:** implemented in part. Both cases now include a five-observation evidence editor with personal connections, controller/mouse positioning, explicit draft save and atomic interpretation/board submission. Legacy completed cases remain valid. The 720p campaign create/restart journey verifies both boards. Live personal-note browsing, permanent saving and diary management have deterministic player-fixture coverage. Normal-play acceptance and public-provider compatibility remain open.
- **Player purpose:** Interpret evidence creatively.
- **Controls:** Inspect observations, assemble evidence constellation, select interpretation and optional explanation.
- **Content inventory:** Two demo cases, five observations and three supported interpretations each. Live personal notebook only.
- **Dependencies:** Authored clues; constellation editor; journal.
- **Persistence:** Observations, chosen interpretation and explanation.
- **Provenance:** Demo fiction; live notes are player interpretations, never generated factual conclusions.
- **Accessibility:** No single hidden correct answer; text entry may use keyboard.
- **Acceptance:** Both cases accept all three interpretations after evidence; no premature completion.
- **Evidence:** [automated regression source](../Assets/ClustrGalaxy/Tests/EditMode/EvidenceBoardTests.cs); [artifact-specific runs and unresolved gates](beta-completion.md). Player acceptance is pending.

## F11 — World celebrations

- **Status:** implemented in part; committed cues, three-slot pool and deduplication verified; entity-following discovery rings, distinct effect colors and progressive constellation tracing are implemented. Full visual/accessibility/performance acceptance pending.
- **Player purpose:** Feel discoveries change the world.
- **Controls:** Automatic after committed progression; intensity and reduced-motion settings.
- **Content inventory:** System route pulse, constellation sky trace, flight ribbon, discovery flourish; pooled maximum three simultaneous.
- **Dependencies:** Committed outcomes; effect pool.
- **Persistence:** Stable event deduplication; restore rewards without replaying celebrations.
- **Provenance:** Authored presentation effects.
- **Accessibility:** Reduced motion, intensity and mute.
- **Acceptance:** Failed saves silent; duplicate outcomes silent; cap and teardown verified.
- **Evidence:** [automated regression source](../Assets/ClustrGalaxy/Tests/EditMode/ActivityTests.cs); [artifact-specific runs and unresolved gates](beta-completion.md). Player acceptance is pending.

## F12 — Journey diary and sharing

- **Status:** implemented in part. Current-tuning rolling recordings, timeline/speed playback, explicit screenshot Save/Discard, PNG viewing, paired capture metadata, storage/deletion, and versioned replay import are implemented. Current-playground packages with explicit anchors can be selected for matching practice; legacy/scenic packages remain standalone. The combined 720p creation, flight and restart journey passed. Diary browsing, explicit bookmark saving and diary/replay postcards passed the combined 720p creation/restart journey. Version-3 bounded replay annotations and permanent bookmarks independent of automatic diary eviction are implemented and automatically verified. Complete F12 player/performance acceptance remains open.
- **Player purpose:** Keep and share a personal journey locally.
- **Controls:** Bookmark, explicit screenshot/replay save, observatory playback, export/import; user-controlled deletion.
- **Content inventory:** 200 automatic diary entries; 10 Hz × 15 minutes = 9,000 preallocated poses; PNG and replay packages.
- **Dependencies:** Double/anchor coordinates; durable storage; safe parser.
- **Persistence:** Profile/snapshot/mode records; rolling buffer volatile until explicit save.
- **Provenance:** Only permitted labels; import never grants progression or requests provider data.
- **Accessibility:** Playback controls and controller focus; no mandatory audio.
- **Acceptance:** 64 MiB / 9,000 sample import cap; reject malformed/version/external-path data; compatible ghost identity required.
- **Evidence:** [automated regression source](../Assets/ClustrGalaxy/Tests/EditMode/JourneyReplayTests.cs); [artifact-specific runs and unresolved gates](beta-completion.md). Player acceptance is pending.

## Implementation evidence

The first foundation provides immutable activity definitions/state/intents/outcomes, session-owned Explore/Survey services, v2 companion persistence, ordered clue and mystery validation, bounded diary metadata and explicit residency ownership. `ActivityTests.cs` and the activity ownership regression in `CoreArchitectureTests.cs` passed in `Builds/validation/20260907T015153143278Z/summary.json`. At this historical checkpoint, signal tuning had only its deterministic evaluator; the later interaction checkpoint below adds its runtime interface. These are partial foundations; player acceptance for all twelve features remains pending.

## Original sound assets

Soft Bell, Warm Pluck and Airy Glass are original, procedurally authored two-second mono PCM recordings at 24 kHz. They are bundled through the local visual Addressables catalogue; unavailable clips produce silent visual feedback. Musical pitch derives from stable IDs and a fixed pentatonic palette, not source activity or sentiment. Eight reusable sources bound normal playback; reduced audio uses two sources at reduced volume.

## Foundation and first interaction checkpoint

`Builds/validation/20260907T020408361539Z/summary.json` passes 127 EditMode and 12 PlayMode tests with no project compiler diagnostics. F01 now has a runtime reticle, optional automatic tuning, Explore practice and atomic Survey clean-signal distinctions. Changing scan target or world mode cancels the charge. Journal reconciliation can retry without duplicating clean distinctions. F05 has local instrument assets, an eight-source audio pool, stable notes, ambient and constellation playback, audition, tempo/volume/preset, mute and reduced-audio controls. Full player and performance acceptance is still pending; this is not phase-two completion (F06/F11 remain pending).

## Feature-completion execution checkpoint

The accepted feature-only plan is [2026-09-07-feature-catalogue-completion.md](superpowers/plans/2026-09-07-feature-catalogue-completion.md). [Custom art source and inventory](exploration-art.md) documents the first original cozy retro-space mesh pass. Mystery-board persistence and compatibility plus anomaly animation checks pass in `Builds/validation/20260907T231749648730Z/summary.json` (182 EditMode, 15 PlayMode). Evidence-board campaign creation/restart passed at 720p in `Builds/evidence/20260907T230950633689Z-demo-journey/summary.json`. These checkpoints do not establish catalogue-wide player acceptance.


Current runtime audit: the earlier journey harness missed Unity callback-signature errors. Historical fixture assertions remain useful behavioral evidence but do not establish zero-error acceptance. See the ledger's runtime diagnostic audit and newer strict-harness runs. Feature status always distinguishes code existence, automated behavior, runtime diagnostics and human acceptance.


## Flight-first integration update (2026-09-08)

F03/F04 now place their presentation courses outside current body exclusion envelopes. A bounded search reports unavailable space instead of placing a course through a star or planet. Long scenic segments use distance to the corridor rather than distance to the next gate when deciding whether guidance was abandoned. Course version 2 separates these records from historical competitive ghosts; old recordings remain viewable. Runtime course hazards end guidance without an award.

F09 visits use clearance beyond the destination's exclusion boundary, preserving safe access to enlarged stars. F06/F08/F10/F12 editing and viewing surfaces share a projected cockpit interface and nested pause ownership; the navigation chart suppresses pilot input while local simulation continues. These integrations have core/Unity automated coverage and initial Linux visual evidence; final complete player acceptance and release qualification remain separate, open statuses in the beta ledger.
