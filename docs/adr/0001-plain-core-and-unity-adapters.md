# ADR-0001: Plain Core and Unity adapters

- Status: accepted

Gameplay state machines live in `ClustrGalaxy.Core`. They do not depend on `MonoBehaviour`, scene search, input actions, or Unity lifecycle. `ClustrGalaxy.Unity` contains adapters for input, physics, rendering, persistence serialization, and scene lifetime. Assembly definitions enforce this direction.

Implementation note (2026-09-06): GalaxySessionBehaviour owns runtime initialization
and teardown. The runtime assembly still contains Unity adapters alongside a
separate plain Core assembly. The HUD is retained uGUI. Full resident-change-set
rendering integration remains pending; this ADR records the intended boundary,
not proof that every migration gate is complete.

Expansion foundation: GalaxySession owns separate Explore and Survey ActivitySessions, keyed by profile and immutable snapshot. Immutable activity intents/outcomes persist candidate companion state before committing. Authored definitions never enter source projections; Unity adapters own input, effects and audio. Activity residency references have explicit owners and release on mode changes and teardown.
