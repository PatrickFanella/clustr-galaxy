# ADR-0002: Atomic Survey transitions

- Status: accepted

Every Survey intent is validated and reduced into one immutable outcome, then persisted exactly once. Successful outcomes may drive presentation or cosmetic reactions, but observers cannot perform nested Survey mutations. The synchronous mutation event bus is retired.

The reducer constructs a candidate state, saves it, then publishes authoritative
state. IO or permission failure returns a retryable rejection and does not consume
the intent ID. Outcome listeners cannot re-enter the Unity mutation adapter.
Authored discoveries apply only to the demo namespace. Snapshot identity is frozen
for a session; provider refresh requires creating another session.
