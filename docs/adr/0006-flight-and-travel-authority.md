# ADR 0006: Local propulsion and fictional travel have separate execution paths

Status: implementation in progress; see the [simulation execution ledger](../superpowers/plans/2026-09-08-flight-first-simulation.md).

The session owns the reference ship, flight identity, travel state and resident references. Normal local flight uses bounded force and torque with explicit mass and inertia. Assist and guidance spend the same directional propulsion budget as manual piloting; Mastery retains momentum. Gravity remains active in local flight. Collision recovery preserves committed progress and requires explicit resume.

System cruise is explicitly labelled a fictional drive. Its core computes logical positions and bounded approach speeds independently of Unity Rigidbody propulsion. The Unity adapter checks moving exclusion volumes with swept segments and performs explicit transitions to/from local physics. Arrival requires positive safety clearance and relative closing speed below 20 m/s. A nearby demo flight is qualified separately from broad performance or human acceptance.

Travel commands identify session, frozen snapshot and monotonically increasing operation. State changes are published once and listeners cannot perform nested travel mutations. Validated destination loads can be staged without changing active scenes or residency; only the exact current receipt may be committed. Cancellation, superseding loads, caller cancellation ignored by a provider, wrong namespaces and cross-session receipts cannot commit staged destinations. The Unity fast-travel action now starts the same charged jump transaction. The session issues a preparation bound to its active operation; only that preparation can authorize arrival after safe-clearance validation. Raw arrival commands cannot bypass destination loading. The legacy core intent remains for compatibility and is not the ordinary Unity travel path.

Jump choices use validated resident system identities and published route direction, independently of rendered stars. Survey requires known destinations and known routes; unidentified destinations retain anonymous labels. The command list is paginated and filtered on presentation changes. Loading failure or cancellation retains the current usable scene; committing destination data precedes Unity materialization. Rollback from an unexpected materialization exception remains an unresolved boundary.

Inspecting another system, discussion or thread does not move the ship or replace its current physical system. The current chart is retained while its physical system is active; a fully separate, continuously updated map representation remains cockpit integration work. Live arrivals use the shared central stellar envelope and logical-to-render frame conversion. Cache-only fixture restart uses the fixture namespace with networking prohibited, and is labelled offline deterministic fixture rather than live data.

Celestial organizational frames stay at the render origin. Ephemerides and static gravity anchors retain logical coordinates; newly loaded ephemerides initialize at the current simulation clock, and rebasing reapplies the last simulated positions without advancing the clock or altering ship velocity. Physical Rigidbody positions decide rebasing; interpolated render history is reset and synchronized during the shift to prevent an old-frame pose from causing another origin shift. No mutual celestial N-body simulation is introduced.

The cockpit command console suppresses pilot input while physics continues. Observatory/editor/replay modal capture pauses local simulation and restores its previous rate and component state. Full station integration, context ownership and accessibility remain tracked acceptance gates.

Public spatial-scene-v1 and source relationships are unchanged. Physics, authored geometry and fictional travel are internal presentation/simulation policy. Existing v2 progress remains intact; no v1 storage is read or modified by these changes. Imported replays do not award progression. Human acceptance, Linux platform verification and complete hardware measurements remain separate from automated correctness.


## Cockpit station input and chart presentation

The session owns `CockpitInputContext`. Command and chart contexts suppress pilot actions without pausing celestial motion or active guidance. Editing, observatory, diary and replay surfaces acquire independent modal leases; local simulation resumes only after every owner releases its lease. The compatibility modal flag cannot override an outstanding lease. This prevents nested view teardown from leaking flight input or unpausing a parent editor.

`CockpitStationSurface` projects retained feature content into a shared camera-attached canvas with consistent typography and focus restoration. `CockpitChartStation` renders its navigation camera to a bounded local render texture; the primary camera remains attached to the cockpit. Inspecting the chart no longer makes the ship kinematic. The chart camera excludes UI and diorama layers. This is a presentation adapter over existing residency, not a separate live scene model. Continuous live chart population remains an open implementation item.

Implementation and automated suite verification are distinct from player acceptance. Core checks exercise nested leases, stale releases and chart suppression without pause. Linux station journeys and resolution inspection are recorded in the completion ledger as they run.
