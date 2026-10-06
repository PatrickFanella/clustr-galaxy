# ADR-0003: One resident galaxy index

- Status: accepted

One resident galaxy module owns loaded and active entity/route indexes, projection scope, pooling, and Survey visibility. Static entity registries and steady-state scene scans are forbidden. Loaded and active are explicit, distinct queries.

Scope membership is tracked independently for shared entities/routes. New
projections apply the current Survey visibility before exposure. Unity registry
views are cached and invalidated on changes. Selection/navigation pin ownership
and rendering from core resident change sets are still incomplete migration work.
