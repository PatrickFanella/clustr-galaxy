# ADR-0002: Atomic Survey transitions

- Status: accepted

Every Survey intent is validated and reduced into one immutable outcome, then persisted exactly once. Successful outcomes may drive presentation or cosmetic reactions, but observers cannot perform nested Survey mutations. The synchronous mutation event bus is retired.
