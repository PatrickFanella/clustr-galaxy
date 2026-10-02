# Expanded Linux beta implementation roadmap

Accepted September 6, 2026. All twelve feature specifications in [the catalogue](../../exploration-features.md) are mandatory beta scope. The existing technical milestone is not replaced or weakened.

## Architecture and compatibility

GalaxySession owns an immutable plain-core activity service. Definitions have stable authored IDs and content versions. Intents/outcomes identify profile, snapshot and world mode. Unity owns input, retained UI, physics, audio and rendering. Activity references explicitly retain resident entities and release on cancellation, completion, mode change and teardown. Coordinates use doubles or stable anchors. Public spatial-scene-v1 is unchanged.

Expansion saves are v2 companion documents keyed by profile/snapshot/mode. Existing v2 Survey and constellation membership remains compatible; absent companion data uses defaults. Never read/migrate/overwrite/delete v1. Persist validated candidate state before publishing success. Failed persistence is retryable without deduplication changes. Reconcile committed Survey events idempotently after restart. Explore and imports cannot award Survey progression.

Replay packages contain only versioned declarative poses/events/permitted labels. Imports cap at 64 MiB and 9,000 samples, reject unsupported versions/nonfinite numbers/external paths/network references. Competitive ghosts require matching snapshot/course/tuning; otherwise standalone playback. No provider request or progression on import. Automatic diary metadata caps at 200; screenshots/permanent replays are explicit and user-deletable.

## Ordered implementation phases

1. Document all features and extend the completion ledger. Stabilize session, persistence, residency and modal input; establish activity types, authored-content validation and v2 companion persistence. Existing provider, GPU, controller, Ubuntu and CI failures remain open.
2. Discovery/creation: F01 tuning, F05 music, F06 styling/postcards, F11 celebrations. The existing six-member demo constellation is the vertical slice: scan, create, play, save, export, restart.
3. Flight/recording: F03 profiles, F04 challenges, F12 recording core. Verify origin shifts, separate Assist/Mastery records, save/restart/ghost playback.
4. Authored campaign: F02 three trails, F07 six anomalies, F10 two mysteries. Supply all authored text, placements, triggers, interpretations and keepsakes; validate reachability and complete player campaign.
5. Personal experience: F08 observatory, F09 three expeditions, remaining F12 diary/import/export. Integrate eligible live activities; verify incomplete data and restart.
6. Qualify one clean source revision and both player configurations. Complete original and expanded journeys, Arch/Ubuntu builds, real controller, live/offline, CI, notices and local packaging. No public publication.

A phase is complete only with passing automated checks and its Linux player journey. Development switches cannot hide required features in the final artifact.

## Test and release contract

- Core: deterministic tuning/triggers/courses/music/expeditions; failed-save retry; replayed intents; wrong identity/mode; candidate immutability; bounded diary; existing-v2 compatibility; v1 sentinels.
- Integration: visibility and resident ownership; cancellation and teardown; modal input and focus; course origin shifts; ghosts; pooled audio/effects; missing assets; reduced audio/motion.
- Exports: membership and edges, explicit save/queue isolation, exact postcard dimensions; replay caps, malformed packages and incompatible ghosts; no import progression/network.
- Players: tune/trail/anomaly/mystery/musical constellation/observatory/restart; all courses and saved ghost; all expedition moods and paused restart; postcard/replay export and separate-profile import; live/incomplete/cache/unavailable behavior.
- Verify 1280×720, 1920×1080 and 2560×1440 with virtual and real controllers. Record enjoyment observations separately: discovery, comfort, retry appeal, expressiveness, unguided enjoyment.
- Retain 600 measured seconds and all CPU/GPU/allocation/memory/cache thresholds. Include music, visible ghost, guidance and pooled celebrations. Report editing/observatory intervals separately without hiding stalls. Missing counters, Ubuntu evidence, hardware journeys or any mandatory gate means NOT QUALIFIED.

## Implementation checkpoint

The catalogue and ledger were written before feature code. Activity persistence, scan tuning, musical feedback, constellation studio/postcards and committed celebration adapters are implemented to differing acceptance levels. Scripted scan/create/play/save/export/restart runs passed at all three resolutions; real-controller, manual comfort and active-feature performance gates remain open. A fixed-size recorder and strict declarative replay codec now pass core tests and a 720p explicit-save/restart journey. See [the ledger](../../beta-completion.md) for immutable run paths, including failures.

Next implementation work remains phase three: session-owned scenic courses and playground evaluation, separate durable Assist/Mastery records, guidance/preview adapters and actual saved-ghost playback. The current free-flight recorder supplies no competitive course identity. Course tuning identity must include the effective flight configuration before competitive ghosts are enabled. Then complete the authored campaign and personal experience phases. Existing provider/GPU/Ubuntu/controller/CI dependencies are still mandatory release gates, and local feature work remains independently unfinished.

The flight tranche now supplies session-owned course evaluation/guidance, separate durable records and explicit saved-flight viewing. The 720p expanded player integration has passed (see ledger for fixture limitations). Opt-in personal-best ghosts aligned to a new practice course and pooled decorative training bodies are now implemented; atomic replacement, legacy-file compatibility and origin-shift/restart behavior have automated evidence. Remaining phase-three work includes controller/manual flight evaluation, multi-resolution acceptance and active-course allocation/performance qualification. These remain independent of the later authored campaign and observatory work.
