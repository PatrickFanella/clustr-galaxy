# ADR-0001: Plain Core and Unity adapters

- Status: accepted

Gameplay state machines live in `ClustrGalaxy.Core`. They do not depend on `MonoBehaviour`, scene search, input actions, or Unity lifecycle. `ClustrGalaxy.Unity` contains adapters for input, physics, rendering, persistence serialization, and scene lifetime. Assembly definitions enforce this direction.
