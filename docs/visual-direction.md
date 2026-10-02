# Flight deck and solar-system presentation — 2026-09-08 UTC

Status: historical first visual iteration; automated and player evidence below. The subsequent [orbital-flight work](superpowers/plans/2026-09-08-orbital-flight.md) changes scale, gravity and cockpit projection again. Artistic acceptance and release qualification remain separate.

The player accepted the current flight controls for now and requested that development move to world scale and presentation: smooth orbits, a visible sun, a proper flight HUD, a cockpit, larger planets and improved menus. This iteration preserves the accepted bindings and flight-v3 acceleration/braking tuning.

## Implemented

- Orbit guides use 512 samples spaced geometrically around the ellipse instead of 96 equal-time samples. Actual deterministic Kepler motion is unchanged.
- Demo planets have ten times their previous radius; the existing lane allocator accounts for the enlarged bodies. Live post-planet presentation receives the same radius multiplier.
- The main demo sun has radius 140 (previously 22), warm plasma shading and a corona. Demo planets have deterministic procedural surfaces, day/night shading, atmospheric limbs and shared higher-resolution sphere geometry.
- A camera-mounted 3D canopy and instrument deck frame the flight view. Its colliders are disabled, and it hides during captured UI/map contexts.
- The retained flight HUD presents heading, speed, selected-target range, scan progress, contextual objective and source provenance. The detailed status paragraph moves into the command console.
- The command console separates station navigation, scrollable actions and a flight brief. Buttons share focus/highlight styling; controller navigation retains access to existing actions.
- The demo opens with a nearby planet and the sun in view. Distant fog is removed; the instanced starfield follows floating-origin shifts using cached world matrices.

The procedural planet surfaces and main solar treatment are currently demonstrated in authored demo content. This record does not claim equivalent live-provider art, public-provider validation, complete modal art direction or final bespoke cockpit assets. Source metrics and relationships are unchanged; surface patterns are decorative.

## Verification

Base source `775f826f51ee70d8f25e3e421887eaa5a6f10801`, working tree on `codex/linux-beta-qualification`. Release artifact tree SHA-256: `b98ad9b74f08f76a92b188425df7ddd93615e37f81d383fc16dfb98985f2496d`.

- [Unity validation](../Builds/validation/20260908T043709564729Z/summary.json): 218 EditMode and 16 PlayMode passed; no project compiler diagnostics.
- [Release build](../Builds/build-logs/20260908T043919Z/BuildLinuxRelease.json): successful; per-file artifact checksums recorded.
- [720p visual smoke](../Builds/evidence/20260908T044048855031Z-visual-smoke/summary.json) and [1440p visual smoke](../Builds/evidence/20260908T044109013678Z-visual-smoke/summary.json): passed. Actual flight, console, close planet and origin-shift captures inspected. Eighteen guides, at least 512 vertices per guide, approximately 199-unit first planet diameter and 280-unit sun diameter. Relative body/ship physics position preserved across origin shift. No runtime error markers.
- [720p create and restart journey](../Builds/evidence/20260908T044131484002Z-demo-journey/summary.json): passed using virtual controller traversal; saved profile, postcard/replay and v1 sentinels preserved.
- Nine Python harness tests passed; whitespace diff check passed.

Earlier failed runs remain available. The first origin assertion incorrectly read the interpolated ship display transform immediately after moving its Rigidbody; the corrected assertion compares authoritative Rigidbody positions. Visual captures are rendered Linux-player evidence, not player approval. The current artifact has not repeated every feature journey at every resolution. Full 600-second performance qualification must be repeated for the changed artifact.


The [30-second active-feature hierarchy stress report](../Builds/evidence/20260908T044540618093Z-hierarchy/summary.json) completed with 14,053 resident nodes and 57,000 routes, CPU p95 11.18 ms, GPU p95 0.87 ms, longest ordinary frame 18.77 ms, zero ordinary managed allocation, and 3,440/3,440 valid GPU samples. Player memory peaked at 1.35 GB and GPU memory at 435 MB. Numerical preflight requirements were met; the process returned 2 with `Build provenance unavailable`, so the report is not a passing qualification. This fixture stress run does not measure the demo-specific procedural surface scene or replace the 600-second release gate. The interrupted preceding run is preserved with an explicit interruption record.
