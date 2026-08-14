# Clustr Galaxy Survey Gameplay Wave Implementation Plan

> **For agentic workers:** Execute this plan task-by-task. Recommended path:
> dispatch a fresh subagent per task, review each result with `review-quality`,
> then continue. For complex multi-agent splits, use
> `parallel-feature-development`, `team-composition-patterns`, and
> `team-communication-protocols`. Steps use checkbox (`- [ ]`) syntax for
> tracking.

**Goal:** Turn the flyable data prototype into a coherent beta loop where players choose Explore or Survey, discover the graph by scanning, complete constellations and contracts, save personal discoveries, and fly at a legible astronomical scale.

**Architecture:** Keep the immutable `SpatialSceneStore` as factual truth and add a separate snapshot-bound `SurveyDiscoveryStore` that controls what Survey mode may render, target, or describe. World mode (Explore/Survey) and flight mode (Autopilot/Assist/Mastery) are orthogonal state machines; gameplay systems communicate through typed discovery events rather than scene searches. Authored constellations, contracts, achievements, and easter eggs are local versioned content; personal constellations persist locally and enter an explicit disabled-by-default submission outbox until a reviewed backend contract exists.

**Tech Stack:** Unity 6000.4.10f1, C#/.NET, Unity Input System, NUnit EditMode/PlayMode tests, URP 17.4, Addressables 2.7.6, JSON persistence under `Application.persistentDataPath`, Linux standalone build.

---

## Scope, dependencies, and locked decisions

This wave is Unity-only. Do not touch the Clustr database, backend repository, Kvant/Almaz jobs, migrations, API deployment, or production services. The live-data agent owns those systems. Development and all automated tests must work with the deterministic demo snapshot; the same gameplay services consume live stable IDs later without branching the rules.

This is intentionally two releasable slices:

1. **Survey Core:** scale and flight tuning, mode state, discovery persistence, signal scanning, route reveal, HUD-only threads, and one end-to-end Survey journey.
2. **Discovery Content:** authored and personal constellations, local submission outbox, contracts, achievements, one clearly authored easter egg, and beta qualification.

Locked product behavior:

- Explore exposes the loaded universe, search, and fast travel. It never advances Survey progress.
- Survey starts from one chosen subreddit/system. Only that system is known; scans reveal connected entities and routes.
- Survey progress is keyed to the pinned revision and spatial catalog. A new snapshot creates a new progress namespace and never silently transfers discoveries.
- Scanning a member of an authored constellation displays progress such as `Example Constellation 1/6`.
- Personal constellations contain stable entity IDs plus a local name and note/reason. Saving is local. Submission is a separate, explicit action and is never automatic.
- Thread replies appear only in the HUD/tree. They do not create world bodies or reply constellations.
- Autopilot, Pilot Assist, and Pilot Mastery can be changed freely in either world mode. Mode changes do not invalidate achievements.
- Historical time layers remain outside this wave because one immutable snapshot cannot support honest historical playback.
- Easter eggs are authored overlays labelled as such; data anomalies remain factual scan findings and are never rewritten as fictional facts.

## File map

Create focused folders instead of expanding `GalaxyPrototypeBootstrap.cs` into a gameplay monolith:

- `Runtime/Gameplay/GalaxyModeState.cs` — orthogonal world and flight modes plus change events.
- `Runtime/Gameplay/SurveySnapshotKey.cs` — revision/catalog identity used by all progress.
- `Runtime/Gameplay/SurveyDiscoveryState.cs` — serializable discovered entities, routes, scans, constellation progress, and rewards.
- `Runtime/Gameplay/SurveyDiscoveryStore.cs` — atomic profile load/save and discovery mutations.
- `Runtime/Gameplay/SurveyEvent.cs` — typed event payload consumed by HUD, contracts, achievements, and easter eggs.
- `Runtime/Gameplay/SurveyEventBus.cs` — in-process event stream with no Unity scene lookup.
- `Runtime/Scanning/SignalScanner.cs` — scan state machine and controller binding.
- `Runtime/Scanning/ScanRules.cs` — pure reveal rules for entities and connected routes.
- `Runtime/Scanning/ScanResult.cs` — immutable result shown in the HUD.
- `Runtime/Constellations/ConstellationDefinition.cs` — authored stable-ID pattern.
- `Runtime/Constellations/ConstellationTracker.cs` — snapshot-safe progress calculation.
- `Runtime/Constellations/PersonalConstellation.cs` — local player-authored pattern and note.
- `Runtime/Constellations/PersonalConstellationStore.cs` — atomic local persistence.
- `Runtime/Constellations/ConstellationSubmissionOutbox.cs` — explicit queued drafts; no network adapter.
- `Runtime/Progression/SurveyContractDefinition.cs` — data-driven contract requirements.
- `Runtime/Progression/SurveyProgression.cs` — contracts, achievements, and scanner upgrade levels.
- `Runtime/Discovery/EasterEggDefinition.cs` — authored trigger/reward with visible provenance.
- `Runtime/Discovery/EasterEggTracker.cs` — evaluates authored triggers from Survey events.
- `Runtime/UI/ThreadInspectionModel.cs` — converts loaded comment descendants into a bounded HUD tree.
- `Runtime/UI/SurveyHudModel.cs` — presentation-only mode, scan, discovery, constellation, contract, and message state.
- `Runtime/UI/ProbeHud.cs` — renders the new model and controls; no rules or persistence.
- `Runtime/Generation/GalaxyTuning.cs` — one testable source for spatial and flight defaults.
- `Runtime/Flight/ProbeFlightModel.cs` — pure flight-mode velocity calculation.
- `Runtime/Flight/ObserverProbeController.cs` — input/physics adapter using `GalaxyModeState` and `GalaxyTuning`.
- `Runtime/World/GalaxyDiscoveryVisibility.cs` — decides whether a logical entity/route is renderable and targetable.
- `Runtime/Bootstrap/GalaxyGameplayBootstrap.cs` — wires services; prototype bootstrap only builds demo content.
- `Tests/EditMode/SurveyGameplayTests.cs` — pure rules, persistence, tuning, and content tests.
- `Tests/PlayMode/SurveyGameplayPlayModeTests.cs` — controller/runtime discovery and visibility journey.

Existing files modified by the wave:

- `Runtime/Generation/GalaxyScale.cs`
- `Runtime/Bootstrap/GalaxyPrototypeBootstrap.cs`
- `Runtime/Flight/GalaxyNavigationController.cs`
- `Runtime/Interaction/CommentThreadController.cs`
- `Runtime/Interaction/ProbeTargeting.cs`
- `Runtime/Interaction/LiveGalaxySelectionController.cs`
- `Runtime/World/LiveGalaxyMaterializer.cs`
- `Runtime/World/GalacticOverviewRenderer.cs`
- `Runtime/UI/ProbeHud.cs`
- `Tests/EditMode/SpatialSceneContractsTests.cs`
- `Tests/PlayMode/GalaxyNavigationPlayModeTests.cs`
- `README.md`

Use these verification commands throughout. If the open editor owns the project lock, use the menu items and inspect the Console instead of launching a second editor:

```bash
# Open-editor shortcuts
# Ctrl+R: refresh
# Shift+Ctrl+G: Clustr Galaxy/Run Edit Mode Tests
# Shift+Ctrl+J: Clustr Galaxy/Run Play Mode Tests
# Shift+Ctrl+Alt+L: Clustr Galaxy/Build Linux Beta

git diff --check
git status --short
```

Expected suite baselines before Task 1: 36 EditMode tests and 2 PlayMode tests pass; the Linux beta builds with zero errors. Record the actual counts in the task log if Unity reports a different non-failing count.

### Task 1: Lock the wave baseline and test fixtures

**Files:**
- Create: `Assets/ClustrGalaxy/Tests/EditMode/SurveyGameplayTests.cs`
- Create: `Assets/ClustrGalaxy/Tests/EditMode/SurveyGameplayTests.cs.meta`
- Modify: `Assets/ClustrGalaxy/Tests/PlayMode/GalaxyNavigationPlayModeTests.cs`

- [ ] **Step 1: Record a clean baseline**

Run both existing suites from the open editor and build the Linux beta. Save Console lines containing pass counts and `[ClustrGalaxy.Build] Linux beta succeeded` in the execution log. Expected: no failed tests and no build error.

- [ ] **Step 2: Add a shared deterministic survey fixture**

Create `SurveyFixture` inside the new EditMode test file with snapshot key `(revision: 9, catalog: 3)`, starting system `subreddit:technology`, six constellation IDs, two routes, and a three-level comment thread. Use only stable IDs already generated by `GalaxyPrototypeBootstrap` so PlayMode and EditMode tests describe the same universe.

```csharp
private static readonly string[] ConstellationMembers =
{
    "subreddit:technology",
    "post:open-source-ai",
    "post:linux-handhelds",
    "post:right-to-repair",
    "post:privacy-tools",
    "post:retro-computing"
};
```

- [ ] **Step 3: Add ignored journey names before implementations exist**

Add PlayMode tests marked `[Ignore("Enabled by the implementing task")]` named `SurveyStart_HidesEverythingOutsideTheStartingSystem`, `Scan_RevealsConnectedRouteAndDestination`, and `ModeSwitch_PreservesIndependentSurveyProgress`. This makes the intended vertical slice visible without making baseline red.

- [ ] **Step 4: Re-run both suites**

Expected: all pre-existing tests pass; exactly three new PlayMode tests are skipped.

- [ ] **Step 5: Commit the fixture checkpoint**

```bash
git add Assets/ClustrGalaxy/Tests/EditMode/SurveyGameplayTests.cs* \
  Assets/ClustrGalaxy/Tests/PlayMode/GalaxyNavigationPlayModeTests.cs
git commit -m "test: lock survey gameplay fixture"
```

### Task 2: Correct astronomical scale and ship pacing

**Files:**
- Create: `Assets/ClustrGalaxy/Runtime/Generation/GalaxyTuning.cs`
- Create: `Assets/ClustrGalaxy/Runtime/Generation/GalaxyTuning.cs.meta`
- Modify: `Assets/ClustrGalaxy/Runtime/Generation/GalaxyScale.cs`
- Modify: `Assets/ClustrGalaxy/Runtime/Flight/ProbeFlightModel.cs`
- Modify: `Assets/ClustrGalaxy/Runtime/Flight/ObserverProbeController.cs`
- Modify: `Assets/ClustrGalaxy/Runtime/Bootstrap/GalaxyPrototypeBootstrap.cs`
- Test: `Assets/ClustrGalaxy/Tests/EditMode/SurveyGameplayTests.cs`

- [ ] **Step 1: Write failing scale and travel-time tests**

```csharp
[Test]
public void BetaTuning_ExpandsSpaceAndSlowsTheProbe()
{
    Assert.That(GalaxyTuning.RegionDistanceScale, Is.EqualTo(5f));
    Assert.That(GalaxyTuning.SystemDistanceScale, Is.EqualTo(4f));
    Assert.That(GalaxyTuning.PlanetOrbitScale, Is.EqualTo(3f));
    Assert.That(GalaxyTuning.MoonOrbitScale, Is.InRange(2f, 2.5f));
    Assert.That(GalaxyTuning.CruiseSpeed, Is.InRange(78f, 104f));
    Assert.That(GalaxyTuning.Acceleration, Is.InRange(38f, 57f));
}

[Test]
public void BetaTuning_KeepsNeighborPlanetTravelReadable()
{
    var seconds = GalaxyTuning.EstimatedTravelSeconds(3000f);
    Assert.That(seconds, Is.InRange(20f, 45f));
}
```

- [ ] **Step 2: Run EditMode tests and verify compile failure**

Expected: `GalaxyTuning` does not exist.

- [ ] **Step 3: Implement immutable tuning defaults**

```csharp
namespace ClustrGalaxy.Generation
{
    public static class GalaxyTuning
    {
        public const float RegionDistanceScale = 5f;
        public const float SystemDistanceScale = 4f;
        public const float PlanetOrbitScale = 3f;
        public const float MoonOrbitScale = 2.25f;
        public const float OrbitPeriodScale = 10f;
        public const float OrbitSimulationTimeScale = 1f / OrbitPeriodScale;
        public const float CruiseSpeed = 92f;
        public const float BoostMultiplier = 1.75f;
        public const float Acceleration = 48f;
        public const float BrakeAcceleration = 120f;

        public static float EstimatedTravelSeconds(float distance) =>
            distance / CruiseSpeed;
    }
}
```

Update `GalaxyScale` to derive its axes, lane gaps, and time scales from these constants. `KeplerOrbit` multiplies elapsed time by the supplied simulation time scale, so pass `OrbitSimulationTimeScale` (`0.1`), not `OrbitPeriodScale` (`10`); this makes periods ten times longer instead of ten times faster. Update the probe serialized defaults to match. Preserve the existing floating-origin threshold; the expanded logical coordinates must continue rebasing rather than increasing float-world limits.

- [ ] **Step 4: Apply tuning to demo layout and map camera distances**

Scale region/system/planet/moon logical placement, not body radii. Pass the reciprocal time scale to `KeplerOrbit` so orbit periods are multiplied by `OrbitPeriodScale`; keep gravity parameters independent from visual size. Set normal boost to `1.75x`, not the current `5x`.

- [ ] **Step 5: Verify tests and rendered pacing**

Run EditMode tests, enter Play mode for two minutes, and verify: bodies no longer resemble a marble stack; no body overlaps its parent; a normal-speed flight between the two nearest planet lanes falls inside 20–45 seconds; no Console exceptions occur.

- [ ] **Step 6: Commit**

```bash
git add Assets/ClustrGalaxy/Runtime/Generation/GalaxyTuning.cs* \
  Assets/ClustrGalaxy/Runtime/Generation/GalaxyScale.cs \
  Assets/ClustrGalaxy/Runtime/Flight/ProbeFlightModel.cs \
  Assets/ClustrGalaxy/Runtime/Flight/ObserverProbeController.cs \
  Assets/ClustrGalaxy/Runtime/Bootstrap/GalaxyPrototypeBootstrap.cs \
  Assets/ClustrGalaxy/Tests/EditMode/SurveyGameplayTests.cs
git commit -m "feat: establish readable galaxy scale and flight pacing"
```

### Task 3: Add orthogonal world and flight modes

**Files:**
- Create: `Assets/ClustrGalaxy/Runtime/Gameplay/GalaxyModeState.cs`
- Create: `Assets/ClustrGalaxy/Runtime/Gameplay/GalaxyModeState.cs.meta`
- Modify: `Assets/ClustrGalaxy/Runtime/Flight/ProbeFlightModel.cs`
- Modify: `Assets/ClustrGalaxy/Runtime/Flight/ObserverProbeController.cs`
- Modify: `Assets/ClustrGalaxy/Runtime/UI/ProbeHud.cs`
- Test: `Assets/ClustrGalaxy/Tests/EditMode/SurveyGameplayTests.cs`

- [ ] **Step 1: Write failing state tests**

```csharp
[Test]
public void Modes_AreOrthogonalAndFreelyChangeable()
{
    var modes = new GalaxyModeState();
    modes.SetWorldMode(GalaxyWorldMode.Survey);
    modes.SetFlightMode(FlightControlMode.PilotMastery);
    Assert.That(modes.WorldMode, Is.EqualTo(GalaxyWorldMode.Survey));
    Assert.That(modes.FlightMode, Is.EqualTo(FlightControlMode.PilotMastery));
    modes.SetFlightMode(FlightControlMode.Autopilot);
    Assert.That(modes.WorldMode, Is.EqualTo(GalaxyWorldMode.Survey));
}
```

- [ ] **Step 2: Implement the pure mode state**

```csharp
public enum GalaxyWorldMode { Explore, Survey }
public enum FlightControlMode { Autopilot, PilotAssist, PilotMastery }

public sealed class GalaxyModeState
{
    public GalaxyWorldMode WorldMode { get; private set; } = GalaxyWorldMode.Explore;
    public FlightControlMode FlightMode { get; private set; } = FlightControlMode.PilotAssist;
    public event Action Changed = delegate { };
    public void SetWorldMode(GalaxyWorldMode value) { WorldMode = value; Changed(); }
    public void SetFlightMode(FlightControlMode value) { FlightMode = value; Changed(); }
}
```

Guard duplicate assignments so they do not emit duplicate events.

- [ ] **Step 3: Define flight behavior in pure math**

Add `ProbeFlightModel.DesiredVelocity(mode, current, intent, forwardSpeed, deltaTime)`. Autopilot damps and aligns toward the selected destination, Pilot Assist uses capped velocity targeting, and Pilot Mastery applies acceleration without velocity-target overwrite. Gravity assist remains a momentary modifier within all three modes.

- [ ] **Step 4: Bind controller actions and HUD state**

Use D-pad left/right (and keyboard `[`/`]`) to cycle flight modes. Change world mode only from the map/HUD mode selector, not a single accidental flight button. Display both `WORLD EXPLORE|SURVEY` and `PILOT AUTO|ASSIST|MASTERY` continuously.

- [ ] **Step 5: Run EditMode and manual controller checks**

Expected: mode state tests pass; switching flight mode never changes world mode; all three modes remain controllable; Survey/Explore selection requires an explicit map action.

- [ ] **Step 6: Commit**

```bash
git add Assets/ClustrGalaxy/Runtime/Gameplay/GalaxyModeState.cs* \
  Assets/ClustrGalaxy/Runtime/Flight/ProbeFlightModel.cs \
  Assets/ClustrGalaxy/Runtime/Flight/ObserverProbeController.cs \
  Assets/ClustrGalaxy/Runtime/UI/ProbeHud.cs \
  Assets/ClustrGalaxy/Tests/EditMode/SurveyGameplayTests.cs
git commit -m "feat: add explore survey and pilot modes"
```

### Task 4: Persist snapshot-bound Survey discovery

**Files:**
- Create: `Assets/ClustrGalaxy/Runtime/Gameplay/SurveySnapshotKey.cs`
- Create: `Assets/ClustrGalaxy/Runtime/Gameplay/SurveyDiscoveryState.cs`
- Create: `Assets/ClustrGalaxy/Runtime/Gameplay/SurveyDiscoveryStore.cs`
- Create: matching `.meta` files
- Modify: `Assets/ClustrGalaxy/Runtime/Data/SpatialSceneLoader.cs`
- Test: `Assets/ClustrGalaxy/Tests/EditMode/SurveyGameplayTests.cs`

- [ ] **Step 1: Write failing persistence/isolation tests**

```csharp
[Test]
public void Discovery_IsIsolatedBySnapshotAndExploreDoesNotMutateIt()
{
    using var fixture = SurveyStoreFixture.Create();
    var first = fixture.Open(new SurveySnapshotKey(9, 3), "subreddit:technology");
    first.DiscoverEntity("post:open-source-ai", DiscoveryReason.Scan);
    first.Save();

    Assert.That(fixture.Open(new SurveySnapshotKey(9, 3), "subreddit:technology")
        .IsEntityKnown("post:open-source-ai"), Is.True);
    Assert.That(fixture.Open(new SurveySnapshotKey(10, 4), "subreddit:technology")
        .IsEntityKnown("post:open-source-ai"), Is.False);
}
```

- [ ] **Step 2: Implement serializable state and atomic storage**

Persist under `Application.persistentDataPath/ClustrGalaxy/profiles/v1/<profile>/<revision>-<catalog>.json`. State contains schema version, revision, catalog, starting system, known entity IDs, known route IDs, completed scan IDs, constellation member IDs, completed contracts, achievements, scanner level, and last saved UTC.

Write `<path>.tmp`, flush and close it, validate it with `JsonUtility.FromJson`, then atomically replace the destination. Sort every ID array ordinally before writing so identical state produces stable JSON.

- [ ] **Step 3: Add explicit session initialization**

`SpatialSceneLoader` exposes its pinned `SurveySnapshotKey` after manifest validation. Demo mode uses the fixed key `(9, 3)` plus a `demo` namespace so it cannot be confused with authoritative cached progress.

- [ ] **Step 4: Verify recovery cases**

Add tests for duplicate discovery, truncated/corrupt JSON fallback to a fresh profile, no cross-revision merge, and no file write caused by Explore-mode visibility.

- [ ] **Step 5: Commit**

```bash
git add Assets/ClustrGalaxy/Runtime/Gameplay/SurveySnapshotKey.cs* \
  Assets/ClustrGalaxy/Runtime/Gameplay/SurveyDiscoveryState.cs* \
  Assets/ClustrGalaxy/Runtime/Gameplay/SurveyDiscoveryStore.cs* \
  Assets/ClustrGalaxy/Runtime/Data/SpatialSceneLoader.cs \
  Assets/ClustrGalaxy/Tests/EditMode/SurveyGameplayTests.cs
git commit -m "feat: persist snapshot-bound survey discovery"
```

### Task 5: Implement discovery visibility and the starting-system choice

**Files:**
- Create: `Assets/ClustrGalaxy/Runtime/World/GalaxyDiscoveryVisibility.cs`
- Create: `Assets/ClustrGalaxy/Runtime/World/GalaxyDiscoveryVisibility.cs.meta`
- Modify: `Assets/ClustrGalaxy/Runtime/World/GalacticOverviewRenderer.cs`
- Modify: `Assets/ClustrGalaxy/Runtime/World/LiveGalaxyMaterializer.cs`
- Modify: `Assets/ClustrGalaxy/Runtime/Interaction/ProbeTargeting.cs`
- Modify: `Assets/ClustrGalaxy/Runtime/Flight/GalaxyMapSelection.cs`
- Modify: `Assets/ClustrGalaxy/Runtime/UI/ProbeHud.cs`
- Test: EditMode and PlayMode survey files

- [ ] **Step 1: Write failing visibility tests**

```csharp
[TestCase(GalaxyWorldMode.Explore, false, true)]
[TestCase(GalaxyWorldMode.Survey, false, false)]
[TestCase(GalaxyWorldMode.Survey, true, true)]
public void Visibility_UsesWorldModeAndDiscovery(
    GalaxyWorldMode mode, bool known, bool expected)
{
    Assert.That(GalaxyDiscoveryVisibility.CanRender(mode, known), Is.EqualTo(expected));
    Assert.That(GalaxyDiscoveryVisibility.CanTarget(mode, known), Is.EqualTo(expected));
}
```

- [ ] **Step 2: Implement one visibility gate used everywhere**

`CanRender`, `CanTarget`, `CanLabel`, and `CanFastTravel` all take mode plus known state. In Survey, an unknown route may render only as a weak directional signal after its source is scanned; its destination name remains hidden until revealed.

- [ ] **Step 3: Add Survey start selection**

On first Survey entry, the map asks the player to choose one loaded subreddit. Confirming it calls `StartAt(stableId)`, marks the star, its currently loaded post bodies, and local navigational anchors known, then saves. Do not auto-select a global “best” system in the client.

- [ ] **Step 4: Route rendering, targeting, labels, and map selection through the gate**

Unknown objects must not remain in `GalaxyEntity.ActiveEntities` as hidden colliders. Either do not acquire them from `GalaxyEntityPool`, or release them when entering Survey. Logical nodes stay in `SpatialSceneStore` so scans can reveal them without another network fetch.

- [ ] **Step 5: Enable the first ignored PlayMode test**

Remove its `Ignore` attribute. Expected: entering Survey with `subreddit:technology` makes that system targetable and a disconnected fixture system neither rendered nor targetable; Explore restores visibility without changing the Survey save.

- [ ] **Step 6: Commit**

```bash
git add Assets/ClustrGalaxy/Runtime/World/GalaxyDiscoveryVisibility.cs* \
  Assets/ClustrGalaxy/Runtime/World/GalacticOverviewRenderer.cs \
  Assets/ClustrGalaxy/Runtime/World/LiveGalaxyMaterializer.cs \
  Assets/ClustrGalaxy/Runtime/Interaction/ProbeTargeting.cs \
  Assets/ClustrGalaxy/Runtime/Flight/GalaxyMapSelection.cs \
  Assets/ClustrGalaxy/Runtime/UI/ProbeHud.cs \
  Assets/ClustrGalaxy/Tests/EditMode/SurveyGameplayTests.cs \
  Assets/ClustrGalaxy/Tests/PlayMode/GalaxyNavigationPlayModeTests.cs
git commit -m "feat: gate survey universe by discovered knowledge"
```

### Task 6: Add signal scanning and route-driven reveal

**Files:**
- Create: `Assets/ClustrGalaxy/Runtime/Scanning/ScanResult.cs`
- Create: `Assets/ClustrGalaxy/Runtime/Scanning/ScanRules.cs`
- Create: `Assets/ClustrGalaxy/Runtime/Scanning/SignalScanner.cs`
- Create: `Assets/ClustrGalaxy/Runtime/Gameplay/SurveyEvent.cs`
- Create: `Assets/ClustrGalaxy/Runtime/Gameplay/SurveyEventBus.cs`
- Create: matching `.meta` files
- Modify: `Assets/ClustrGalaxy/Runtime/Interaction/ProbeTargeting.cs`
- Modify: `Assets/ClustrGalaxy/Runtime/Data/SpatialSceneStore.cs`
- Modify: `Assets/ClustrGalaxy/Runtime/UI/ProbeHud.cs`
- Test: EditMode and PlayMode survey files

- [ ] **Step 1: Write failing reveal-rule tests**

```csharp
[Test]
public void Scan_RevealsStrongestUnknownRouteBeforeWeakerRoutes()
{
    var routes = new[]
    {
        new ScanRoute("weak", "known", "far", 2f),
        new ScanRoute("strong", "known", "near", 8f)
    };
    var result = ScanRules.Reveal("known", routes, scannerLevel: 1);
    Assert.That(result.RevealedRouteIds, Is.EqualTo(new[] { "strong" }));
    Assert.That(result.RevealedEntityIds, Is.EqualTo(new[] { "near" }));
}
```

- [ ] **Step 2: Implement deterministic scan rules**

Define `ScanRoute` in `ScanResult.cs` as an immutable value containing `Id`, `SourceId`, `TargetId`, and `Weight`. Sort candidate routes by descending published weight, then ordinal route ID. Level 1 reveals one destination; levels 2 and 3 reveal two and four. Scanning a star reveals its loaded planets. Scanning a route reveals the destination silhouette and stable ID but delays its name/metadata until the destination scan. A completed system reveals remaining weak outbound signals. Repeat scans in the same snapshot return `AlreadyScanned` and never award progress twice.

- [ ] **Step 3: Implement the scanner state machine**

Bind scan to keyboard `R` and gamepad west button. Require a valid target, range within the target type’s configured range, and a 1.5-second uninterrupted hold. States are `Idle`, `Charging`, `Complete`, `OutOfRange`, and `NoSignal`. Cancellation must not mutate discovery.

- [ ] **Step 4: Publish typed events and HUD feedback**

On completion publish one `SurveyEvent.ScanCompleted` containing snapshot key, scanned ID, newly revealed entity IDs, newly revealed route IDs, UTC, and scanner level. HUD shows charge percent, signal strength, reveal summary, and `ALREADY SURVEYED` when appropriate. Do not create physical “signal” objects.

- [ ] **Step 5: Enable the route-reveal PlayMode test**

Expected: an unknown destination is absent before scanning; after the scan event it becomes a targetable silhouette and the connecting route appears; a second scan produces no duplicate progress.

- [ ] **Step 6: Commit**

```bash
git add Assets/ClustrGalaxy/Runtime/Scanning Assets/ClustrGalaxy/Runtime/Gameplay/SurveyEvent* \
  Assets/ClustrGalaxy/Runtime/Data/SpatialSceneStore.cs \
  Assets/ClustrGalaxy/Runtime/Interaction/ProbeTargeting.cs \
  Assets/ClustrGalaxy/Runtime/UI/ProbeHud.cs \
  Assets/ClustrGalaxy/Tests/EditMode/SurveyGameplayTests.cs \
  Assets/ClustrGalaxy/Tests/PlayMode/GalaxyNavigationPlayModeTests.cs
git commit -m "feat: reveal the galaxy through signal scanning"
```

### Task 7: Move reply threads entirely into the HUD

**Files:**
- Create: `Assets/ClustrGalaxy/Runtime/UI/ThreadInspectionModel.cs`
- Create: `Assets/ClustrGalaxy/Runtime/UI/ThreadInspectionModel.cs.meta`
- Modify: `Assets/ClustrGalaxy/Runtime/Interaction/CommentThreadController.cs`
- Modify: `Assets/ClustrGalaxy/Runtime/Interaction/CommentThreadNode.cs`
- Modify: `Assets/ClustrGalaxy/Runtime/Bootstrap/GalaxyPrototypeBootstrap.cs`
- Modify: `Assets/ClustrGalaxy/Runtime/World/LiveGalaxyMaterializer.cs`
- Modify: `Assets/ClustrGalaxy/Runtime/UI/ProbeHud.cs`
- Test: EditMode and PlayMode survey files

- [ ] **Step 1: Write failing HUD-tree tests**

Create a fixture with more than three levels and assert that `ThreadInspectionModel.Build(root, maxDepth: 3, maxNodes: 250)` returns only descendants, preserves deterministic score/stable-ID ordering, exposes reply counts for truncated branches, and creates no `GalaxyEntity`.

- [ ] **Step 2: Implement the bounded presentation tree**

Use data-only nodes containing stable ID, author, score, body excerpt, depth, reply count, and `HasMore`. No `Transform`, collider, orbit, or mesh belongs in this model.

- [ ] **Step 3: Remove prototype reply bodies**

Replace `AttachPrototypeThread` with demo `SpatialNode` descendants consumed by the inspection model. `CommentThreadController` requests/caches a thread and supplies the model to the HUD; it no longer toggles a world-space reply root. Top-level parent comments remain satellites or ring particles.

- [ ] **Step 4: Verify no reply world objects exist**

PlayMode assertion: inspecting a parent changes the HUD model node count but leaves `GalaxyEntity.ActiveEntities` count unchanged. Back/close clears the displayed tree while leaving the immutable response in `SnapshotCache`.

- [ ] **Step 5: Commit**

```bash
git add Assets/ClustrGalaxy/Runtime/UI/ThreadInspectionModel.cs* \
  Assets/ClustrGalaxy/Runtime/Interaction/CommentThreadController.cs \
  Assets/ClustrGalaxy/Runtime/Interaction/CommentThreadNode.cs \
  Assets/ClustrGalaxy/Runtime/Bootstrap/GalaxyPrototypeBootstrap.cs \
  Assets/ClustrGalaxy/Runtime/World/LiveGalaxyMaterializer.cs \
  Assets/ClustrGalaxy/Runtime/UI/ProbeHud.cs \
  Assets/ClustrGalaxy/Tests/EditMode/SurveyGameplayTests.cs \
  Assets/ClustrGalaxy/Tests/PlayMode/GalaxyNavigationPlayModeTests.cs
git commit -m "feat: present comment threads in the survey HUD"
```

### Task 8: Add authored and personal constellations

**Files:**
- Create: `Assets/ClustrGalaxy/Runtime/Constellations/ConstellationDefinition.cs`
- Create: `Assets/ClustrGalaxy/Runtime/Constellations/ConstellationTracker.cs`
- Create: `Assets/ClustrGalaxy/Runtime/Constellations/PersonalConstellation.cs`
- Create: `Assets/ClustrGalaxy/Runtime/Constellations/PersonalConstellationStore.cs`
- Create: `Assets/ClustrGalaxy/Runtime/Constellations/ConstellationSubmissionOutbox.cs`
- Create: matching `.meta` files
- Modify: `Assets/ClustrGalaxy/Runtime/UI/ProbeHud.cs`
- Test: `Assets/ClustrGalaxy/Tests/EditMode/SurveyGameplayTests.cs`

- [ ] **Step 1: Write failing constellation tests**

```csharp
[Test]
public void AuthoredConstellation_ReportsOneOfSixAndCompletesExactlyOnce()
{
    var tracker = SurveyFixture.CreateConstellationTracker();
    var first = tracker.RecordScan("post:open-source-ai");
    Assert.That(first.Message, Is.EqualTo("Example Constellation 1/6"));
    foreach (var id in ConstellationMembers) tracker.RecordScan(id);
    Assert.That(tracker.IsComplete("example-constellation"), Is.True);
    Assert.That(tracker.CompletionAwards, Is.EqualTo(1));
}
```

- [ ] **Step 2: Implement authored definitions and tracking**

Definitions contain ID, display name, description, stable member IDs, optional snapshot/catalog compatibility, reward ID, and provenance `AuthoredOverlay`. Unresolved members make a definition unavailable rather than reducing its denominator. Scan progress is stored as member stable IDs so input order cannot change it.

- [ ] **Step 3: Add the six-member demo constellation**

Use the six stable IDs from Task 1 and display `Example Constellation N/6`. Keep it a code fixture until the Addressables content-catalog pass; do not create an unreviewed public factual claim.

- [ ] **Step 4: Implement personal constellations**

Require 3–12 unique known stable IDs, a 1–60 character local name, and a 1–500 character note/reason. Persist atomically under `profiles/v1/<profile>/constellations.json`. Reject duplicates, unknown IDs in Survey, control characters, and blank reasons.

- [ ] **Step 5: Implement an explicit submission outbox**

`QueueForSubmission(personalId, confirmed)` throws unless `confirmed` is true. The queued payload contains a generated submission ID, snapshot key, stable IDs, name, note, created UTC, and state `AwaitingBackend`. It contains no username, credentials, or implicit network request. HUD text must say `SAVED LOCALLY` before submission and `QUEUED LOCALLY — SUBMISSION SERVICE UNAVAILABLE` afterward.

- [ ] **Step 6: Verify local-only behavior and commit**

Run tests with networking disabled and assert save, reload, and queued status work with zero HTTP request. Then commit.

```bash
git add Assets/ClustrGalaxy/Runtime/Constellations \
  Assets/ClustrGalaxy/Runtime/UI/ProbeHud.cs \
  Assets/ClustrGalaxy/Tests/EditMode/SurveyGameplayTests.cs
git commit -m "feat: add discoverable and personal constellations"
```

### Task 9: Add survey contracts, achievements, and scanner upgrades

**Files:**
- Create: `Assets/ClustrGalaxy/Runtime/Progression/SurveyContractDefinition.cs`
- Create: `Assets/ClustrGalaxy/Runtime/Progression/SurveyProgression.cs`
- Create: matching `.meta` files
- Modify: `Assets/ClustrGalaxy/Runtime/Gameplay/SurveyDiscoveryState.cs`
- Modify: `Assets/ClustrGalaxy/Runtime/UI/ProbeHud.cs`
- Test: `Assets/ClustrGalaxy/Tests/EditMode/SurveyGameplayTests.cs`

- [ ] **Step 1: Write failing progression tests**

Cover these exact contracts: scan three posts, reveal two routes, complete one system, complete one authored constellation. Cover achievements for first scan, first new system, first constellation, first personal constellation, and first Pilot Mastery docking/arrival. Assert replaying an event cannot award twice and changing flight mode never invalidates progress.

- [ ] **Step 2: Implement event-driven progression**

`SurveyProgression` subscribes to `SurveyEventBus`, evaluates pure counters, writes completed IDs to `SurveyDiscoveryStore`, and emits one HUD notification. It never queries the scene. The first two completed contracts raise scanner level from 1 to 2; four completed contracts raise it to 3. No currency or inventory is added in this wave.

- [ ] **Step 3: Add a compact HUD objective panel**

Show one pinned contract, current/required count, scanner level, last achievement, and constellation progress. The panel must not cover the reticle or thread inspection panel at 2560×1440 and 1920×1080.

- [ ] **Step 4: Verify persistence and commit**

Reload the profile and assert completions and scanner level survive. Switch Explore/Survey and all flight modes; assert no duplicate or lost reward.

```bash
git add Assets/ClustrGalaxy/Runtime/Progression \
  Assets/ClustrGalaxy/Runtime/Gameplay/SurveyDiscoveryState.cs \
  Assets/ClustrGalaxy/Runtime/UI/ProbeHud.cs \
  Assets/ClustrGalaxy/Tests/EditMode/SurveyGameplayTests.cs
git commit -m "feat: add survey contracts and achievements"
```

### Task 10: Add fast travel, assisted arrival, and seamless mode switching

**Files:**
- Modify: `Assets/ClustrGalaxy/Runtime/Flight/GalaxyNavigationController.cs`
- Modify: `Assets/ClustrGalaxy/Runtime/Flight/ObserverProbeController.cs`
- Modify: `Assets/ClustrGalaxy/Runtime/Interaction/LiveGalaxySelectionController.cs`
- Modify: `Assets/ClustrGalaxy/Runtime/UI/ProbeHud.cs`
- Test: EditMode and PlayMode survey files

- [ ] **Step 1: Write eligibility tests**

Explore may fast travel to any loaded navigable system. Survey may fast travel only to known systems. Inspect targets, comments, and unknown silhouettes are never fast-travel destinations. Autopilot may plot to the same eligible destinations without teleporting.

- [ ] **Step 2: Implement deterministic arrival envelopes**

Fast travel moves the probe to a safe arrival anchor outside the star’s collision/gravity softening radius, zeros angular velocity, preserves no pre-jump linear velocity, and updates floating-origin logical coordinates in the same fixed-step transaction. Autopilot instead uses a capped approach speed and brakes inside the arrival envelope.

- [ ] **Step 3: Enable the mode-switch PlayMode test**

Expected: Survey discovery survives Explore round-trip, fast travel cannot select an unknown destination in Survey, Pilot Mastery can be enabled immediately after autopilot arrival, and exact navigation back still restores hierarchy.

- [ ] **Step 4: Run PlayMode regression suite and commit**

```bash
git add Assets/ClustrGalaxy/Runtime/Flight/GalaxyNavigationController.cs \
  Assets/ClustrGalaxy/Runtime/Flight/ObserverProbeController.cs \
  Assets/ClustrGalaxy/Runtime/Interaction/LiveGalaxySelectionController.cs \
  Assets/ClustrGalaxy/Runtime/UI/ProbeHud.cs \
  Assets/ClustrGalaxy/Tests/EditMode/SurveyGameplayTests.cs \
  Assets/ClustrGalaxy/Tests/PlayMode/GalaxyNavigationPlayModeTests.cs
git commit -m "feat: add mode-aware galaxy travel"
```

### Task 11: Add a safe authored easter-egg framework

**Files:**
- Create: `Assets/ClustrGalaxy/Runtime/Discovery/EasterEggDefinition.cs`
- Create: `Assets/ClustrGalaxy/Runtime/Discovery/EasterEggTracker.cs`
- Create: matching `.meta` files
- Modify: `Assets/ClustrGalaxy/Runtime/UI/ProbeHud.cs`
- Test: `Assets/ClustrGalaxy/Tests/EditMode/SurveyGameplayTests.cs`

- [ ] **Step 1: Write failing trigger/provenance tests**

Assert an egg triggers only after its exact ordered or unordered discovery predicate is satisfied, awards once per Survey snapshot, and always exposes `Authored discovery — not source data` to the HUD. Explore inspection must not trigger Survey eggs.

- [ ] **Step 2: Implement definitions and tracker**

Definitions contain ID, trigger kind, required stable IDs/event sequence, title, short message, optional local cosmetic reward, and provenance. The tracker consumes Survey events and stores discovered egg IDs in the Survey profile.

- [ ] **Step 3: Add one demo egg**

Use a hidden scan sequence among demo stable IDs, a brief HUD transmission, and a harmless local starfield color pulse. Do not create fake posts, users, comments, metrics, or routes.

- [ ] **Step 4: Verify and commit**

```bash
git add Assets/ClustrGalaxy/Runtime/Discovery \
  Assets/ClustrGalaxy/Runtime/UI/ProbeHud.cs \
  Assets/ClustrGalaxy/Tests/EditMode/SurveyGameplayTests.cs
git commit -m "feat: add provenance-safe galaxy easter eggs"
```

### Task 12: Wire the vertical slice and qualify the beta

**Files:**
- Create: `Assets/ClustrGalaxy/Runtime/Bootstrap/GalaxyGameplayBootstrap.cs`
- Create: `Assets/ClustrGalaxy/Runtime/Bootstrap/GalaxyGameplayBootstrap.cs.meta`
- Modify: `Assets/ClustrGalaxy/Runtime/Bootstrap/GalaxyPrototypeBootstrap.cs`
- Modify: `Assets/ClustrGalaxy/Runtime/UI/GalaxyPerformanceMonitor.cs`
- Modify: `Assets/ClustrGalaxy/Tests/PlayMode/SurveyGameplayPlayModeTests.cs`
- Modify: `README.md`

- [ ] **Step 1: Centralize service wiring**

`GalaxyGameplayBootstrap` creates one mode state, event bus, discovery store, scanner, constellation tracker, personal store, submission outbox, progression service, easter-egg tracker, and HUD model. Inject them into MonoBehaviours. No gameplay service may call `FindAnyObjectByType`, `FindObjectsByType`, or use a static mutable singleton.

- [ ] **Step 2: Add the full offline PlayMode journey**

The test performs: start offline demo → choose Survey → choose `subreddit:technology` → verify only its known scope → target and scan → reveal a route/destination → receive `Example Constellation 1/6` → inspect a parent comment in HUD with no new world entity → switch through all flight modes → save/reload profile → enter Explore and see loaded content → return to Survey with identical progress.

- [ ] **Step 3: Add performance counters for the wave**

Track active/pool entity counts, scan event count, visible/known node counts, steady-state GC allocation, p95 frame time, and memory. Display details behind a developer HUD toggle; retain the compact p95/memory line for normal play.

- [ ] **Step 4: Run all automated gates**

Run EditMode and PlayMode from the open editor. Expected: zero failed and zero skipped tests. Refresh once and rerun to catch serialization/domain-reload faults. Run `git diff --check`.

- [ ] **Step 5: Run the Linux offline smoke**

Build Linux Beta, then launch with `--clustr-offline`. At 2560×1440 verify the complete journey using only a controller. Run for ten minutes and record: no Console/runtime exception, no Clustr API request, p95 CPU frame time, allocated memory, active entities, and largest observed non-loading stall. Acceptance for this wave: p95 CPU below 16.7 ms after warm-up, no steady-state per-frame managed allocation during ordinary flight, RAM below 4 GB, and no non-loading stall above 50 ms.

- [ ] **Step 6: Run a pinned-fixture smoke without production dependency**

Point `SpatialSceneClient` at the existing local fixture response or cached immutable snapshot. Verify the same stable-ID Survey rules run without code changes and mixed revision data remains rejected. Do not access the database or remote hosts.

- [ ] **Step 7: Update player documentation**

Document Explore vs Survey, scanning, constellation progress, personal constellation privacy/submission state, three flight modes, fast travel limits, controller bindings, offline behavior, and the statement that authored easter eggs are not source data. Remove the outdated claim that real API streaming “comes next.”

- [ ] **Step 8: Commit the qualified wave**

```bash
git add Assets/ClustrGalaxy/Runtime/Bootstrap/GalaxyGameplayBootstrap.cs* \
  Assets/ClustrGalaxy/Runtime/Bootstrap/GalaxyPrototypeBootstrap.cs \
  Assets/ClustrGalaxy/Runtime/UI/GalaxyPerformanceMonitor.cs \
  Assets/ClustrGalaxy/Tests/PlayMode/SurveyGameplayPlayModeTests.cs* \
  README.md
git commit -m "feat: deliver offline survey gameplay beta"
```

## Beta acceptance demonstration

The wave is complete only when one uninterrupted Linux run demonstrates all of the following:

1. The player chooses Explore or Survey; Survey asks for a starting subreddit.
2. Only the starting system is known in Survey, while Explore can display all loaded content.
3. A 1.5-second scan reveals the strongest connected route and a destination silhouette.
4. Scanning a constellation member displays `Example Constellation 1/6`; completing all six awards once.
5. A 3–12 member personal constellation with a note survives restart and is not submitted automatically.
6. Explicit submission produces a local `AwaitingBackend` outbox record and honest unavailable-service HUD text.
7. Thread inspection renders a bounded HUD tree and creates no reply body, collider, orbit, or target.
8. Contracts and achievements persist, never double-award, and are unaffected by free flight-mode switching.
9. Autopilot, Pilot Assist, and Pilot Mastery are usable in both Explore and Survey.
10. Distances and orbit periods read as a galaxy rather than a marble simulation; flight pacing meets the travel-time targets.
11. One authored easter egg is clearly marked as authored and never alters factual graph data.
12. Offline, cache, floating-origin, hierarchy-back, pooling, URP, controller, test, and Linux build regressions remain green.

## Deferred integration contract

When the backend owner is ready, add a separate plan for public constellation moderation. That plan must define authentication, consent, rate limits, abuse reporting, moderation states, deletion, stable-ID/snapshot validation, and publication provenance before replacing `ConstellationSubmissionOutbox` with a network adapter. No endpoint name or payload should be invented in this Unity wave.
