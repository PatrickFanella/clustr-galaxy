# ADR 0005: Session-owned exploration activities and declarative replays

Status: accepted architecture; implementation and qualification are partial. See the [feature catalogue](../exploration-features.md) and [completion ledger](../beta-completion.md).

The expansion keeps progression in the plain core. `GalaxySession` opens separate Explore and Survey activity services keyed by profile, frozen snapshot and world mode. Immutable definitions use stable authored IDs and content versions. Activity commands build and validate candidate state, persist it durably, then publish a committed outcome. Persistence failures leave authoritative state and processed-intent identities unchanged so callers can retry. Outcome listeners cannot mutate the service recursively.

Expansion state lives in versioned companion documents inside the v2 family. Existing Survey and membership documents retain their meaning. Missing preferences or layouts use deterministic defaults. Corrupt companions are preserved and reported; no v1 file is consulted. Clean-signal distinctions are part of the existing atomic Survey discovery commit; stable derived diary identities reconcile companion write failures without duplicating awards.

Unity adapters own the probe reticle, pooled eight-voice audio, three-slot celebration rendering and retained constellation editor. Source routes and metrics remain separate from player-drawn edges, artistic music and authored fiction. The session supplies activity residency ownership. Input scopes reserve the editing stick while restoring previous UI navigation on close, and editing suppresses local flight physics/actions.

Recording uses a session-owned fixed-size ring of logical double-coordinate poses, resampled at 10 Hz. Capture and disk writes occur only on explicit saving. World/flight mode changes start a new transient recording. Stored recordings remain historical files. Playback mathematics and the strict [replay codec](../replay-format.md) contain no provider, resident-graph or progression service reference; imported data cannot dispatch gameplay outcomes. Competitive ghosts require matching course/content/snapshot/tuning and Assist/Mastery identities. Unknown schema versions and unsupported trailing content fail closed.

The PNG exporter and replay writer use unique temporary files, durable flush and non-overwriting rename. The companion repository retains its atomic replacement contract. Exports never upload data. Replay viewing, storage management, course/campaign integration and the observatory now have implementation and deterministic journey coverage. Final player/performance qualification remains open; this ADR does not establish feature acceptance.

The flight tranche adds a transient core `FlightActivityRun` and immutable `FlightCourse`/`FlightRecord` values. The session validates scenic route eligibility and retains endpoints, clears old navigation guidance at start, releases activity references when the run ends, and persists completed manual records through the activity commit boundary. A failed record save is retryable; previews cannot create records. Run recordings retain their original snapshot/world-mode identity. The Unity flight adapter applies only guidance and visuals. The standalone top-down viewer suppresses probe input and never dispatches progression commands. Best-ghost recording is explicitly opt-in through a persisted preference, off by default.
A best record embeds its full recording in the same atomic companion document;
failed replacement preserves the previous score and recording together. Existing
records without recordings remain valid. Runs longer than the rolling buffer
retain a score without a competitive ghost. Practice attaches a compatible ghost
once, then samples translated course-anchor-relative double coordinates without
per-frame identity construction. One pooled visual displays playback; hiding it,
reduced motion, previewing or abandoning a run never changes progression. Gravity
or fixed-step changes cancel the comparable run. Authored training bodies are
presentation objects with no source identity or gravitational effect.

Campaign observations use stable authored IDs with text and anchor references kept
separate from source projections. The authoritative session owns transient
observation timers; the Unity adapter supplies range, aim and visibility. Three
seconds of continuous nearby observation submits one persistent intent. Failed
persistence retains the intent for explicit retry and does not reveal a diary
entry. Switching the activity owner resets transient observation timing. Demo
fiction is unavailable in live snapshots. Survey observation requires identified
anchors; optional pinned trails retain only currently eligible resident anchors.
Completed trail records derive keepsake labels, avoiding a second award write.
The notebook suspends local input and exposes all supported interpretations;
Evidence boards and original anomaly assets are implemented; normal-play pacing and art/player acceptance remain open in the ledger.

## Observation evidence boards

Mystery boards reuse the normalized constellation layout type but remain separate `EvidenceBoards` companions. Their stable node IDs name authored observations, never resident entities or Survey membership. New case completion validates five discovered observations and at least one player connection before persisting the board, interpretation and explanation in the same activity replacement. Missing companions in existing completed cases preserve historical completion. Draft saves are explicit, and failures publish no authoritative changes.

Replay child views retain the originating library/observatory modal context and selected item. Closing the child invokes its return callback while flight stays suspended; only closing the outer view releases flight input. Ordinary replay metadata uses the observed flight tuning hash, and changing gravity/momentum or pilot/world context clears the transient recording. Timeline seeking and playback speed affect presentation only.


The observatory renders companion records as local presentation assets: six slots refer to durable originals; clearing a display removes only the selection. Flight displays use recorded poses and omit recording gaps. Child diary/replay views return focus to the originating observatory control and retain flight suppression. The diary browser adds explicit personal bookmarks through the same persist-before-publication activity path. Diary/replay postcards are local 1920×1080 PNG exports with snapshot/mode provenance; they are separate files and never dispatch progression. Local file selection follows its path across child exports that change sort order.

Unity cloud services and automatic Analytics/Ads startup are disabled in project settings. Installed package versions remain locked. Qualification instrumentation continues to write local reports and does not require cloud analytics.


## Retention, annotation and profile boundaries

Automatic diary metadata retains its 200-entry cap. Explicit bookmarks and retained moments survive automatic eviction until the user removes them. The optional companion `retained` field preserves old v2 documents; legacy explicit `bookmark:` identities receive retained defaults. Retain/remove commands use the same durable candidate replacement and intent replay rules as other activities.

Replay version 3 adds a separate 256-event bounded annotation buffer with validated kinds, ordered in-window timestamps and plain permitted labels. The codec accepts versions 1/2/3 and rejects unsupported trailing data. Recorders derive generic annotations only from committed outcomes; imports have no path to activity or Survey dispatch. The optional `--clustr-profile` argument selects a validated local storage identity at session creation, with the old default unchanged.

Active-feature profiling is opt-in and creates a synthetic matching course recording before sampling. It records ghost-in-view, active audio, guidance and three-slot celebration observations, and applies the existing full-frame/ordinary-flight thresholds without relabelling event frames. Initializing a scenario is distinct from proving that its required workload ran.
