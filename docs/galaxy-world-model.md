# Clustr Galaxy world model

- Status: accepted direction; numeric tuning remains experimental
- Target: Unity desktop client
- Scene contract: `spatial-scene-v1`

## Product idea

Clustr Galaxy turns a published Clustr graph revision into a persistent,
flyable universe. Its geography remains faithful to relationships in the
source graph while astronomical visuals make the data legible and enjoyable to
explore.

The simulation is physically inspired rather than literally astronomical.
Distances and body sizes are perceptually compressed, while orbital motion and
local ship gravity follow coherent physical rules.

## Spatial language

| Clustr concept | Galaxy representation | Primary meaning |
| --- | --- | --- |
| Detected community cluster | Galactic region, constellation, or nebula | A stable family of related systems |
| Subreddit | Star and local system | A persistent community landmark |
| Post | Planet or other large system body | A place where one discussion gathers |
| Comment | Moon, satellite, or aggregated ring/swarm | One contribution within a discussion |
| Individual user | Ship | A selected historical participation path |
| Aggregate user activity | Shipping traffic and travel routes | Movement and overlap between communities |
| Relationship strength | Route brightness, thickness, and traffic | Strength and direction of connection |

Communities and subreddits are stable geography. Posts, comments, and users are
activity that resolves progressively as the observer approaches or selects it.

## Exploration layers

### Galaxy view

- Galactic regions show detected community clusters.
- Subreddit systems appear as stars at their published graph coordinates.
- Strong cross-community relationships appear as bundled travel routes.
- Aggregate participation appears as traffic rather than millions of ships.
- Search and targeting can identify any published entity without making every
  entity resident at once.

### System view

- A subreddit star anchors the local coordinate space.
- Significant or recent posts become planets and other large bodies.
- Orbital bands communicate recency, topic, or lifecycle using one consistent
  rule selected during tuning.
- Less significant posts contribute to belts, distant points, and summary
  density instead of individual high-detail meshes.

### Scan view

- Significant comments become named moons or satellites around their post.
- Reply structure appears as branching signal arcs or communication pulses.
- Large comment populations become rings, swarms, and density statistics.
- Selecting an entity opens its label, provenance, metrics, relationships, and
  a deep link to the corresponding browser view when available.

## Visual grammar

Each visual property has one stable family of meanings:

- Position expresses graph relationship.
- Size expresses sustained significance within an entity type.
- Brightness expresses activity or recency.
- Color expresses entity type and stable galactic-region identity.
- Pulsing expresses recent change or unusual activity.
- Route thickness expresses relationship strength.
- Route animation and particle volume express traffic.
- Transparency expresses age, low importance, or uncertainty.

Meaning is normalized within entity types. A star and a moon are never directly
compared by raw radius.

## Metric mapping

Raw Reddit metrics are long-tailed. All size and activity mappings use bounded
logarithmic normalization, robust percentiles, or another explicitly versioned
normalizer. Raw counts never become literal Unity scale or gravitational mass.

### Subreddit stars

| Metric | Presentation |
| --- | --- |
| Unique participating users | Radius and bounded gravitational class |
| Sustained activity | Luminosity and surface motion |
| Post volume | Corona and stellar activity |
| Cross-community participation | Route count and traffic |
| Growth or change | Flares or pulsing |
| Community assignment | Stable spectral color family |

### Post planets

| Metric | Presentation |
| --- | --- |
| Comment count | Radius within the planet class |
| Score and engagement | Atmosphere and illumination |
| Reply velocity | Storms, lights, or aurora |
| Age | Surface state and orbital lifecycle |
| Exceptional activity | Rings or other rare morphology |
| Discussion structure | Moon count, rings, and local signal complexity |

### Comment satellites

- Top-level, highly replied-to, or otherwise significant comments become
  discussion satellites.
- Other resident top-level comments become individual particles in a real
  orbital ring; one inspect-level particle always represents one comment.
- Replies remain collapsed until their parent comment is selected, then appear
  as a bounded branching thread constellation several levels deep.
- At far LOD the particles may merge into an aggregate glow for performance,
  but counts and inspect queries preserve the individual-comment meaning.

### Automated activity filtering

Bot records remain in the source corpus for provenance but are suppressed from
the published galaxy read model. Suppressed activity contributes nothing to
star, planet, comment, ship, or route metrics. Explicit automation flags and
`AutoModerator` are authoritative. Other bot-like account names are suppressed
only when combined with broad cross-community behavior; activity volume alone
never classifies a person as automated. The publication manifest records the
filter-policy version and excluded counts.

### User ships

| Metric | Presentation |
| --- | --- |
| Activity volume | Ship class and bounded visual scale |
| Breadth of communities | Hull or navigation complexity |
| Recency | Engine and signal brightness |
| Participation history | Traveled route |
| Selected relationship | Destination or docking context |

At normal scale, users are aggregate traffic. Nearby, searched, or selected
users may materialize as identifiable ships. A user's strongest affiliation can
be a home port, but does not permanently bind that user to one system.

User ships represent historical public participation records. They must not
imply that a real person is currently present, traveling, or endorsing a
community.

## Physics model

Clustr Galaxy uses hierarchical two-body orbital simulation rather than a
galaxy-wide N-body simulation.

- A post planet follows deterministic Keplerian elements around its subreddit
  star.
- A comment moon follows deterministic Keplerian elements around its post.
- Orbital periods derive from bounded simulation mass and semi-major axis.
- Celestial bodies are analytically placed and do not accumulate numerical
  integration error.
- Celestial bodies do not apply uncontrolled forces to every other body.
- The observer probe experiences local gravitational acceleration.
- The active gravity source changes through explicit spheres of influence.
- Optional barycentric wobble can make a star respond visually to unusually
  large planets without destabilizing the system.

This provides elliptical motion, changing orbital velocity, capture, escape,
and gravity assists while keeping published systems deterministic.

## Stability invariants

- The subreddit star is the local system anchor.
- Data importance, visual radius, and simulation mass are separate values.
- Simulation mass is bounded by an entity-class range.
- Generated bodies never begin inside another body's collision radius.
- Adjacent planetary orbits receive a minimum separation.
- Comment moons remain inside the stable portion of their planet's Hill sphere.
- Gravity uses a small softening radius near body centers.
- Player gravitational acceleration has a safety cap for extreme proximity.
- Aggregate traffic, routes, rings, and signal effects have no gravitational
  mass.
- Orbital elements are deterministic for a spatial catalog and generator
  version.
- Restarting or streaming a system preserves its orbital configuration.
- A newly published data revision cannot silently change the world pinned by an
  active client session.

Black holes or catastrophic orbital events may exist only as explicitly
authored features. They are never an accidental result of raw database counts.

## Scale and coordinates

- Published Clustr coordinates determine stable galactic geography.
- Unity uses nested spaces: galaxy, region, system, and local body.
- A floating origin keeps the observer probe near the physics origin.
- Inter-system distances, local orbital distances, and body radii have distinct
  perceptual scale functions.
- Simulation time can be accelerated for observation without changing the
  deterministic orbit definition.
- Streaming and origin shifts preserve probe-to-target relative positions and
  velocities.

## Rendering and residency

- Galactic regions use density fields, nebulae, instanced stars, and bundled
  routes.
- Systems use pooled meshes, material variants, orbit lines, and selective
  textures.
- Comments and users obey explicit per-system residency budgets.
- Labels are attention-driven: target, search result, nearby significant body,
  or pinned relationship.
- Selected entities and their important relationships outrank generic regional
  caps.

## Next prototype

Replace the current fixed prototype system with a deterministic generated
subreddit system containing:

- one metrically scaled subreddit star;
- several post planets with stable orbital elements;
- significant comment moons and an aggregate comment ring;
- analytic Keplerian orbital motion;
- local gravity acting on the observer probe;
- assisted flight plus a gravity-assist toggle;
- targeting and an information panel showing source metrics;
- a visible distinction between data importance, visual radius, and simulation
  mass.

### Acceptance criteria

- The same input data and generator version produce the same system layout.
- Planets and moons complete stable orbits at accelerated simulation time.
- No generated orbit intersects its parent body or a neighboring orbital lane.
- Comment-heavy posts remain legible within the configured render budget.
- Releasing flight input produces predictable assisted motion.
- The probe can enter orbit, escape, and perform a visible gravity assist.
- Restarting Play Mode preserves all generated orbital elements.
- The prototype makes no direct database connection.
- Unity compiles without project-code errors or warnings.

## Deferred decisions

These should be settled through the generated-system prototype rather than by
guessing:

- exact normalization curves and percentile windows;
- which metric controls orbital band after usability testing;
- entity budgets for planets, moons, ships, routes, and labels;
- gravity strength, distance compression, time scale, and flight-assist tuning;
- visual taxonomy for planet, moon, star, and ship classes;
- when archived posts fade, move outward, or become belts;
- whether black holes or other authored astronomical events belong in the
  product at all.
