# Clustr Galaxy context

## Domain language

- **Published graph**: the factual, read-only Clustr graph exposed as `spatial-scene-v1`.
- **Snapshot**: one immutable revision and spatial-catalog pair. A running galaxy and its Survey progress never mix snapshots.
- **Galaxy session**: the lifetime that owns one snapshot, its resident galaxy, navigation, and optional Survey session.
- **Resident galaxy**: the loaded factual entities and routes plus their current residency and visibility. Loaded and active are distinct states.
- **Explore**: the world mode that exposes all currently loaded entities without changing Survey progress.
- **Survey**: the snapshot-bound discovery mode that starts from one subreddit system and reveals knowledge through scans.
- **Known entity**: an entity whose silhouette may be resident, rendered, targeted, or travelled to according to its factual kind.
- **Identified entity**: a known entity whose factual name and metrics may be presented.
- **Authored overlay**: presentation content, including authored constellations and discoveries, that never rewrites published graph facts.
- **Personal constellation**: a profile-local authored overlay containing 3–12 identified entities and a player-supplied name and reason.
- **Local outbox**: durable, explicitly confirmed intent to submit a personal constellation. It has no network or publication behaviour.
- **Navigation level**: galaxy, region, system, or inspection view within the navigation history.
- **Deterministic demo**: the local offline adapter that supplies stable prototype content without claiming published provenance.

## Product invariants

- The active galaxy is pinned to one snapshot or explicitly labelled legacy/demo.
- Survey progress belongs to exactly one profile and snapshot.
- Explore never mutates Survey progress.
- Replies remain HUD-only inspection data, not physical galaxy entities.
- Authored overlays are always labelled as authored and remain separate from published graph facts.
- Saving a personal constellation and confirming its local queue are separate actions.
- The local outbox never implies that a submission was transmitted or published.
