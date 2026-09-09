# Orbital flight and cockpit simulation

Direction approved by the player: Newtonian physics, assisted orbital capture, compressed distances; cockpit-integrated menus inspired by space-simulation interaction conventions. Existing controls remain the baseline. This is a simulation foundation, not a claim that every system of another commercial game exists.

1. Establish scale: a 2 km solar radius, 120–300 m planet radii, first semimajor axis 9 km and wider lanes. Verify actual rendered sizes, safe planetary periapses and readable cockpit proportions.
2. Couple gravity and ephemerides: use the same gravitational parameters for prescribed Kepler body motion and ship attraction. Enable ship gravity by default; maintain separate thrust/field velocity in Assist and preserve inertia in Mastery. Version replay physics identity. Test attractive falloff, sustained pull, braking and uninstructed stable orbit.
3. Implement session-owned orbital guidance: eligible selected gravity body, safe capture radius, orbital plane and tangential speed derived from gravity, moving-center velocity, cancellation and resident references. Expose capture/release in controller-accessible cockpit actions. Test capture convergence, released coasting, invalid sources and origin shifts.
4. Project command UI inside the cockpit, preserving focus and click behavior. Keep canopy visible, use transparent illuminated panels and give orbital status a dedicated instrument.
5. Validate Unity suites; build one release player; run visual captures and scan/save/restart regression plus an orbital player scenario. Defer visible launch and human review until the player resumes testing. Preserve failed reports and record any mandatory gate left open.

Compatibility: preserve all save files; no v1 access or migration. Old ghosts remain watchable and cannot qualify under a changed physics identity. Keep source API projections unchanged. Prescribed celestial orbits and compressed authored distances are explicit simulation approximations; no claims of relativistic or full mutual N-body simulation.

Status: first foundation implemented and automatically verified in part; player acceptance pending. Prior visual and performance reports do not certify this physics change.

## 2026-09-08 automated checkpoint

- Unity validation `Builds/validation/20260908T051222242554Z/summary.json`: 224 EditMode and 17 PlayMode tests pass, no compiler diagnostics. Includes gravity/braking, capture/release mathematics, inverse-square falloff, origin-relative guidance and controller regressions.
- Release-configured build `Builds/build-logs/20260908T051723Z/BuildLinuxRelease.json` succeeds. Artifact tree SHA-256: `2df4654dea970b11a92bfbe987a9499081be85a568d06762458962d00bfa6238`. Source is the working tree rooted at `775f826f51ee70d8f25e3e421887eaa5a6f10801`; this is not a clean-revision release.
- Isolated 720p Vulkan player `Builds/evidence/20260908T051915404095Z-visual-smoke/summary.json` passes with no runtime exceptions. Eighteen orbit guides have at least 512 vertices; the sun diameter is 4,000 units and inspected planet diameter approximately 597 units. Floating-origin preservation, orbit entry, capture and released coasting all pass. Radius remains approximately 603–656 units during the 120 simulated seconds after release. Accelerated simulation is functional evidence, not a performance measurement or indefinite stability proof.
- Console screenshot inspected: projected controls remain readable and no canopy rib crosses their text. This is one 720p screen, not complete multi-resolution UI acceptance.
- Scan/create/export/restart regression `Builds/evidence/20260908T052019939073Z-demo-journey/summary.json` passes both processes at 720p on the same artifact. Progress, postcards and replay files survive restart and v1 sentinel bytes remain unchanged. The harness places the probe for scanning; it does not establish safe free-flight approaches to the newly enlarged surfaces or full campaign pacing.
- Failed map-fast-travel validation and first rejected Explore orbit smoke remain preserved in their original evidence directories. They prompted fixes to map navigation processing and Survey-only eligibility gating.

Outstanding: all-resolution cockpit interaction; full live-scale consistency and lane separation; safe surface-approach journeys; tidal orbit safety; body collision/impact behavior; physical propulsion configuration; complete cockpit station migration; system-cruise/jump implementation; new performance qualification; deferred human acceptance. See the [technical research](../../space-simulation-research.md) for the proposed engineering contracts and primary sources.
