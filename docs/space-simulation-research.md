# Space-simulation technical research

Research date: 2026-09-08. Approved direction: Newtonian close-range flight, assisted orbit capture, compressed distances, and cockpit-integrated interaction. Elite Dangerous and Star Citizen are design and engineering references. Their proprietary source is unavailable; this document distinguishes published mechanisms from Clustr implementation choices. Historical developer accounts are dated and do not certify present releases.

## Primary-source findings

### Elite Dangerous: separate travel regimes

Frontier's player guide describes normal thruster flight, fast in-system supercruise, and inter-system hyperspace. Flight Assist supplies counterthrust; disabling it permits continued translation and rotation. Supercruise limits control and responds to nearby bodies, with approach/drop conditions. Hyperspace requires a destination and a charge/alignment sequence. Cockpit focus exposes contextual panels. Planetary orbital cruise and glide are powered travel transitions, not proof of a ballistic, engine-off orbit. These distinctions matter more than copying numerical speed caps. [Frontier's Horizons player guide, flight, focus and travel sections](https://hosting.zaonce.net/elite/website/assets/English-PlayersGuide_v2.00-Horizons.pdf).

This is a public behavior specification. I did not find a reliable published source for the exact current supercruise acceleration equation or Frontier's current internal controller gains. No guessed curve should be labelled their algorithm.

### Star Citizen: control systems above rigid-body physics

CIG's June 2014 engineering article includes a technical explanation by physics programmer John Pritchett. It separates pilot commands, feedback control, requested forces/torques, and propulsion allocation. Thruster location relative to center of mass affects torque. It describes velocity/attitude feedback, gravity compensation and allocation priorities. Assistance is a control policy above physics. The account specifically avoids direct state rewriting for ordinary ship control. This is a historical design account, not evidence that every described subsystem survives unchanged today. [Flight Model and Input Controls](https://robertsspaceindustries.com/en/comm-link/engineering/13951-Flight-Model-And-Input-Controls).

A technical caution: the article's prose swaps the conventional descriptions of underdamped and overdamped responses. We will use measured response and standard control mathematics rather than reproduce that wording.

### Star Citizen: precision, spatial frames and bounded geometry

CIG's December 2015 report describes 64-bit positioning, camera-relative rendering, inverted depth, local geometry placement, a zone system and memory-budgeted terrain LOD. Its May 2015 report describes moving rendering to camera-relative coordinates during the large-world conversion. The useful principle is hierarchical spatial frames: precision near the player without requiring every render vertex to carry galaxy-wide coordinates. [December 2015 studio report](https://robertsspaceindustries.com/en/comm-link/transmission/15155-Monthly-Studio-Report-December-2015), [May 2015 studio report](https://robertsspaceindustries.com/en/comm-link/transmission/14758-Monthly-Studio-Report-May-2015).

### Star Citizen: coupled control and transit preparation

The official landing guide distinguishes coupled stopping assistance from decoupled momentum retention. The official quantum-travel guide, updated June 2025, describes destination selection, NAV/QT selection, alignment/calibration and explicit engagement. Those sources provide useful interaction contracts without exposing internal integration or transit equations. [Landing and coupled flight](https://support.robertsspaceindustries.com/hc/en-us/articles/360020925254-How-to-Land-Your-Ship), [Quantum travel](https://support.robertsspaceindustries.com/hc/en-us/articles/360019449994-How-to-Quantum-Travel).

### Star Citizen: reusable interfaces on physical displays

The Alpha 3.12 postmortem discusses reusable Building Blocks UI, styles on differently shaped canvases and the practical difficulty of fitting instruments into different vehicles. It explicitly identifies early interactive art testing as a lesson. We can reuse the underlying separation of presentation data, components, style and physical placement with retained Unity UI; adopting their engine or UI implementation is unnecessary. [Alpha 3.12 postmortem](https://robertsspaceindustries.com/en/comm-link/transmission/17991-Alpha-312-Postmortem).

### Later thruster implementation evidence

CIG's October 2017 Shipyard explanation describes pooled available thrust with per-thruster caps and distinguishes main, retro, VTOL, fixed and gimballed thrusters. This is a useful correction to any assumption that the 2014 description is timeless: implementation details evolved. Clustr should expose actual directional authority and braking distance, then validate the response, rather than copy a historic collection of labels or promise component fidelity it does not simulate. [Ship Thrusters and You](https://robertsspaceindustries.com/en/comm-link/engineering/16179-The-Shipyard-Ship-Thrusters-And-You).

### Frontier's deterministic system generation

Frontier's February 2021 developer recap with lead render programmer Dr Kay Ross describes Stellar Forge as deterministic procedural generation constrained by astronomical data to produce plausible systems. This supports separating a repeatable system definition from the rendering needed at the current location. Clustr already has a different input: published social graph snapshots. It must retain that input's provenance and keep artistic celestial parameters separate, rather than pretend source activity is astronomical mass. [Frontier developer recap](https://store.steampowered.com/news/posts/?appids=359320%2C475180%2C441340&enddate=1617185871&feed=steam_community_announcements).

The same Frontier recap gives a more concrete rendering breakdown: cube patches become a sphere; terrain offsets are refined from large regions toward smaller detail; Odyssey blends prepared terrain resources; materials and lighting use physically based response. It also records a deliberate lighting tradeoff: that version did not use multiple stellar light sources. These are dated implementation details, not universal requirements. The relevant future Clustr work is bounded patch LOD, stable surface coordinates and consistent material response, rather than increasing the resolution of every distant sphere indefinitely.

## Clustr technical design derived from the research

The following is our design, not a claim about either game's private implementation.

### One physical state and one actuator path

The authoritative session owns flight intent, orbital guidance, transit state and eligibility. Unity owns the Rigidbody execution adapter. Human input and autopilot must both request acceleration/torque through the same limits. Normal piloting must not teleport, overwrite position or bypass actuator limits.

A fuller ship definition should provide mass, center of mass, inertia tensor, engine force envelopes, reverse/lateral/vertical authority and angular torque limits. For each thruster, torque is the cross product of its center-of-mass offset and applied force. Do not simulate individual thruster damage or resource plumbing until those systems exist as gameplay.

Assist requests a bounded velocity/rate change. Inertial flight supplies thrust and torque and retains uncommanded momentum. Emergency braking is explicit counterthrust. Gravity is an external field; disabling assistance is not disabling gravity. A debug gravity switch may be exposed during development but must not become the normal definition of Flight Assist Off.

### Coherent compressed orbits

Use one gravitational parameter per body. Outside a safety interior, attraction follows inverse-square distance. Circular speed is sqrt(mu/r), escape speed sqrt(2mu/r), and period 2*pi*sqrt(r^3/mu). A compressed solar system must choose distance, gravity and time together. Multiplying radii without revisiting these quantities produced the previous scale problem.

Prescribed Kepler ephemerides are an acceptable deterministic approximation for authored celestial bodies, provided their clock and gravitational parameters agree with ship gravity. Ship motion should respond to the combined field. This is not a fully interacting N-body solar-system simulation.

Orbit capture selects a safe radius and orbital plane, matches the center body's velocity, builds tangential speed and removes radial/plane error using bounded thrust. Releasing guidance preserves velocity. The orbit should survive release; a perpetual positional animation is not acceptance evidence. A future trajectory instrument should show prograde/retrograde, radial velocity, periapsis, apoapsis and escape/impact status. Capture near a moon also needs a sphere-of-influence or tidal-stability check.

### Distinct travel state machine

Proposed states: Local Flight, Charging, System Cruise, Approach, Orbit Capture, Orbit Hold, Jump Charging, Jump Transit, Arrival and Abort/Cooldown. This is outstanding implementation beyond initial orbital guidance.

System cruise should be a separately labelled fictional drive model, not enormous impulses applied to local collision physics. Its speed envelope depends on clearance, available braking distance and target approach. Use swept segment/sphere tests to prevent crossing exclusion volumes between frames. Approach requires low closing speed and safe range before handing back to ordinary physics.

Inter-system travel must still obey the existing published-route and Survey visibility rules. Load and validate the destination against the frozen snapshot before completing arrival. Cancellation, failed loading and disappearing eligibility preserve the current usable scene. No fabricated connections or provider requests caused by replay playback.

### Frames and precision

Keep double-precision logical locations and local physics/render coordinates near the player. A rebase must translate positions without inventing velocity. Moving-body frames need both position and velocity; rotating frames additionally need angular velocity and explicit velocity conversion. Start with nonrotating body-centered guidance. Nested walkable-ship physics grids are unnecessary for a seated cockpit and are not part of the current delivered functionality.

### Cockpit interface

Use an actual camera-relative or cockpit-mounted world-space canvas for command panels, consistent event cameras and explicit controller focus. Keep the canopy visible and the external world legible behind the projection. Navigation, ship settings, discovery tools and journal views should become stations within that interface. Avoid decorative instruments that imply unimplemented fuel, heat, hull damage or power management.

The flight display needs a velocity vector, mode/drive state, gravity/altitude and approach information. A task-specific instrument presentation can swap scanning and orbital detail without rebuilding every frame. Input capture should suppress pilot controls while retaining active guidance and simulation; a deliberately paused observatory remains a separately labelled context.

## Acceptance and remaining work

### Concrete engineering contracts for the next implementation

These equations specify proposed Clustr behavior; they are not reverse-engineered proprietary algorithms.

| Subsystem | Contract | Verification |
| --- | --- | --- |
| Directional propulsion | For thruster i at body-local offset r_i with direction d_i and bounded output u_i, F = sum(u_i d_i), torque = sum(r_i cross u_i d_i). A bounded allocator minimizes requested-versus-achievable force/torque error. | Saturating yaw must not manufacture translation authority; unavailable reverse thrust must lengthen stopping distance. |
| Assisted translation | Desired acceleration is a bounded velocity-error response. Optional gravity compensation consumes the same thrust budget. Integral control, if introduced, needs anti-windup. | Step response, overshoot, release-to-stop, gravity exceeding engine authority, and mode-switch momentum continuity. |
| Rotation | Requested torque respects the inertia tensor and angular-rate limits. In inertial mode, releasing the stick applies no automatic countertorque. | Equal torque with different inertia produces different angular acceleration; explicit braking reduces angular momentum. |
| Approach instrument | For constant available braking acceleration a and closing speed v, braking distance starts with v²/(2a). Add control latency and moving-target margin. Treat nonpositive braking authority as unavailable. | Moving destinations, reverse approach, near-zero authority, and a target crossing the flight path. |
| Cruise envelope | Proposed clearance bound v <= sqrt(2 a_effective d_clear), further constrained by the fictional drive curve. Evaluate swept clearance before committing a step. | Large frame steps cannot cross a star or planet; interrupted arrival retains a usable view. |
| Orbit telemetry | Specific energy E = v²/2 - mu/r and angular momentum h = r cross v use velocity relative to the moving center. Negative E alone is insufficient: periapsis must clear the surface and nearby fields must permit stability. | Elliptic, escape and impact trajectories; moving-body frame conversion; floating-origin invariance. |
| Cockpit focus | One active input context owns confirm/back and pointer events. Command panels suppress pilot intent while simulation and explicitly active guidance continue. | Every panel exits to a predictable focus target without an accidental thrust, scan or selection. |

Implement propulsion limits and trajectory instruments before long-distance cruise, then cruise exclusion/approach handling before jump transitions. This ordering makes the travel UI report quantities the simulation actually enforces. Heat, fuel, power distribution, damage and component failure remain design candidates until separately specified; decorative readouts must not imply they already exist.

The [implementation plan](superpowers/plans/2026-09-08-orbital-flight.md) tracks the first delivered unit. Required automated evidence includes control impulse and braking response, sustained gravity, orbit energy/radius stability, capture/release, moving centers, origin shifts, controller focus and save/restart regression.

Remaining simulation work must be recorded explicitly: physical ship/thruster configuration, removal of legacy speed-policy shortcuts, tidal-stability limits, body collision/impact handling, integrated multi-station cockpit screens, instrument-driven local/system/jump travel, exclusion volumes, travel resource gameplay if adopted, and complete qualification of the changed artifact. Do not call a larger sun or a mathematical orbit helper a finished space simulation.

No further hands-on testing or visible player launch is requested from the user tonight. Automated isolated checks may continue; human acceptance remains pending.
