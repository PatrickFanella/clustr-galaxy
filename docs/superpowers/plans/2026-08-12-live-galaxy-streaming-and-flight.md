# Clustr Galaxy: Live Data, Streaming, and Flight

## Outcome

Replace fixture systems with a pinned immutable Clustr snapshot while retaining the deterministic prototype as an explicitly labelled offline demo. Deliver in vertical, test-first slices: publication contract, bounded Unity loading and cache, real hierarchy materialization, floating origin and navigation, then rendering/traffic/assets/optimization and Linux qualification.

## Locked product model

- Detected community cluster -> galactic region.
- Subreddit -> star and local system.
- Post -> planet or other large system body.
- Top-level comment with replies -> satellite, capped at 64 per post.
- Other top-level comment -> individually selectable ring particle.
- Replies -> temporary three-level constellation loaded only while the parent is inspected.
- User -> ship; activity -> historical route; weight -> route brightness, width, and traffic density.
- Catalog bodies use deterministic analytic Kepler orbits. Only the player receives softened, bounded local gravity.

## Delivery sequence

1. Extend immutable spatial catalogs with bot-safe typed metrics, hierarchy fields, normalization metadata, policy versions, exclusion counts, and a catalog checksum. Preserve raw source rows. Apply manual overrides first, then known-bot names including AutoModerator, then the conservative bot-name plus 20-community heuristic. Exclude classified activity before all metrics and routes.
2. Extend the graph API additively. Community `lod=near` pages at 500 nodes/1,500 links with client totals capped at 5,000/15,000. Entity `expand=system` returns at most 512 posts and participating users; `expand=discussion` returns only top-level comments in 512-item pages up to 2,048; `expand=thread&depth=3` returns 250 descendants per page. Responses carry revision, catalog, scope, root, truncation, and a cursor bound to the complete request identity.
3. Split Unity data work into `SpatialSceneClient`, `SpatialSceneLoader`, `SpatialSceneStore`, and `SnapshotCache`. Pin one manifest per session, reject mixed revisions, cancel stale selection work, retry only transient failures, and repeat bot suppression before store merge.
4. Cache immutable JSON plus SHA-256 under `Application.persistentDataPath/ClustrGalaxy/cache/v1`. Use atomic replacement and LRU eviction with a 10 GB ceiling. Always retain the latest validated manifest and overview; replace the prior snapshot only after the new base validates. Legacy unpinned data is demo-only.
5. Materialize live region, system, discussion, and thread views from stable IDs and published normalization quantiles. Cap satellite radius at 25% of the planet radius. Clear temporary reply renderers when inspection ends while retaining their cache entries.
6. Add double-precision logical coordinates and fixed-step rebasing at 2,000 Unity units. Shift all world roots, physics objects, routes, trails, particles, targeting state, and cached render offsets without altering velocity or analytic orbit state.
7. Add one-world seamless galaxy/region/system/inspection navigation with controller-only map and flight actions. Prefetch before transitions and keep loading/offline/truncated/error state visible without freezing flight.
8. After data/navigation is stable, migrate once to URP 17.4; add Addressables 2.7.6 for presentation assets; preserve incompatible imported assets. Animate snapshot activity without implying live presence.
9. Keep logical entities in the store; instantiate only near or interactive objects. Pool interactive bodies and UI markers. Instance landmarks, distant bodies, ring particles, traffic, and orbit decoration with shared materials and per-instance properties.
10. Qualify the Linux player at 2560x1440 and 60 FPS on the Ryzen 9 9900X/Radeon RX 7900-class reference machine, then validate an Ubuntu 24.04 build environment in addition to the current Arch workstation.

## Verification gates

- Backend: public-handler integration tests cover filtering, overrides, raw-data preservation, scoped entity results, tree depth, deterministic ordering, and cursor conflicts; run migrations, spatial-catalog integration tests, and `go test ./...`.
- Unity EditMode: URI pinning, paging, merge/deduplication, bot defense, checksum/cache/LRU/offline behavior, visual mapping, lazy replies, orbital bounds, and floating-origin math.
- Unity PlayMode: overview -> region -> subreddit -> post -> thread, controller-only navigation, cached startup, request failures, rebasing, physics/targeting/trails/routes, URP, and Addressables in a Linux player.
- Live: validate first on a database clone, then protected staging/Kvant behind revision-read flags. Compare sampled counts with source SQL. Production/public enablement requires separate approval.
- Performance: 10-minute bounded worst-case traversal; p95 CPU and GPU frame time below 16.7 ms, no steady-state managed allocation during ordinary flight, no non-loading main-thread stall above 50 ms, RAM below 4 GB, VRAM below 6 GB, warm offline overview within three seconds, cache at or below 10 GB.

## Safety and compatibility

- Public read-only data only; never embed database credentials in Unity.
- Keep `spatial-scene-v1` backward compatible through optional fields.
- Never mix snapshot revisions in one session.
- Preserve existing imported assets and unrelated worktree changes.
- Keep fixture content only as a clearly labelled demo/test path.
