# Live Interaction and Qualification Wave Implementation Plan

> **For agentic workers:** Execute this plan task-by-task. Recommended path:
> dispatch a fresh subagent per task, review each result with `review-quality`,
> then continue. For complex multi-agent splits, use
> `parallel-feature-development`, `team-composition-patterns`, and
> `team-communication-protocols`. Steps use checkbox (`- [ ]`) syntax for
> tracking.

**Goal:** Turn the revision-pinned live hierarchy into an understandable controller-first beta and produce repeatable Linux performance evidence without inventing data the server has not published.

**Architecture:** Keep `SpatialSceneLoader` as the session-pinning orchestrator, but move continuation limits and page aggregation into pure policy code that can be tested without networking. Expose map-hover and scope-completeness state through small public models consumed by the existing navigation controller and HUD. Extend the existing performance monitor with an opt-in command-line qualification runner that records a machine-readable report from the same player build.

**Tech Stack:** Unity 6.4, C# 9, Unity Input System, NUnit EditMode/PlayMode tests, URP 17.4, Unity ProfilerRecorder and FrameTimingManager, Linux standalone player.

---

## File map

- Create `Assets/ClustrGalaxy/Runtime/Data/SpatialScopePaging.cs`: immutable scope limits, cursor URI construction, deterministic page aggregation, and honest completion labels.
- Modify `Assets/ClustrGalaxy/Runtime/Data/SpatialSceneLoader.cs`: cancel stale selections with a generation token, follow published cursors within caps, cache only validated aggregate scopes, and publish aggregate scenes once.
- Modify `Assets/ClustrGalaxy/Runtime/Flight/GalaxyMapSelection.cs`: return the nearest candidate as data, retaining the existing ID convenience API.
- Modify `Assets/ClustrGalaxy/Runtime/Flight/GalaxyNavigationController.cs`: continuously expose the controller map cursor's candidate and a provenance-safe label.
- Modify `Assets/ClustrGalaxy/Runtime/UI/ProbeHud.cs`: display map candidate, scope composition, continuation limitations, and profile state without requiring world-space labels.
- Create `Assets/ClustrGalaxy/Runtime/UI/GalaxyBetaProfile.cs`: pure acceptance thresholds and JSON report contract.
- Create `Assets/ClustrGalaxy/Runtime/UI/GalaxyBetaProfileRunner.cs`: opt-in warm-up/sample/report lifecycle for Linux beta runs.
- Modify `Assets/ClustrGalaxy/Runtime/UI/GalaxyPerformanceMonitor.cs`: capture maximum stall, peak per-frame GC allocation, CPU/GPU frame timing, and stable samples.
- Modify `Assets/ClustrGalaxy/Runtime/Data/GalaxyLaunchOptions.cs`: parse `--clustr-beta-profile` and a bounded duration.
- Modify `Assets/ClustrGalaxy/Runtime/Bootstrap/GalaxyPrototypeBootstrap.cs`: attach the runner only for explicit profile launches.
- Modify `Assets/ClustrGalaxy/Tests/EditMode/SpatialSceneContractsTests.cs`: verify cursor binding, cumulative caps, aggregation, status wording, selection preview, launch parsing, and qualification verdicts.
- Modify `Assets/ClustrGalaxy/Tests/PlayMode/GalaxyNavigationPlayModeTests.cs`: verify a map cursor exposes a selectable live entity without selecting it.
- Modify `README.md`: document live traversal, continuation semantics, controls, profile command, report location, and evidence boundary.

### Task 1: Continuation-safe bounded loading

- [x] **Step 1: Write the failing paging-policy tests**

Add tests that assert a cursor is URL encoded and appended to an already pinned URI; community pages stop at 5,000 nodes/15,000 links; discussion pages stop at 2,048 comments; two matching pages aggregate stable IDs and links; and contradictory revision/catalog/scope/root metadata is rejected.

```csharp
var next = SpatialScopePaging.WithCursor(pinnedUri, "catalog:3 offset:500");
Assert.That(next, Does.EndWith("&cursor=catalog%3A3%20offset%3A500"));
Assert.That(SpatialScopePaging.For("community").MaximumNodes, Is.EqualTo(5000));
Assert.Throws<InvalidOperationException>(() =>
    SpatialScopePaging.Aggregate(manifest, "community", "region-a", pages));
```

- [x] **Step 2: Run EditMode and confirm RED**

Run:

```bash
/home/onnwee/Videos/6000.4.10f1/Editor/Unity -batchmode -nographics \
  -projectPath /home/onnwee/Projects/subcult/clustr-galaxy \
  -runTests -testPlatform EditMode -testResults /tmp/clustr-wave-edit-red.xml \
  -logFile /tmp/clustr-wave-edit-red.log
```

Expected: compilation fails because `SpatialScopePaging` does not exist.

- [x] **Step 3: Implement the pure policy and loader loop**

Implement `WithCursor`, `For`, and `Aggregate` as deterministic public functions. In `LoadScope`, increment a request generation, collect validated pages, stop when the response is complete, the cursor is absent, or the cumulative cap is reached, and publish one aggregate `SpatialScene`. After every network yield, abandon work whose generation is no longer current. Cache the aggregate JSON only after all accepted pages validate.

- [x] **Step 4: Run EditMode and confirm GREEN**

Run the Step 2 command with `clustr-wave-edit-green` paths. Expected: all EditMode tests pass with zero failures.

- [x] **Step 5: Commit**

```bash
git add Assets/ClustrGalaxy/Runtime/Data Assets/ClustrGalaxy/Tests/EditMode/SpatialSceneContractsTests.cs docs/superpowers/plans/2026-08-14-live-interaction-and-qualification-wave.md
git commit -m "feat: page bounded live galaxy scopes"
```

### Task 2: Controller-readable map and scope feedback

- [x] **Step 1: Write the failing selection-preview test**

Add an EditMode test for `GalaxyMapSelection.Nearest` and a PlayMode test that opens the map, positions a selectable community at the cursor, waits one frame, and asserts `MapHoveredStableId` and `MapHoverStatus` are populated without changing the targeting selection.

```csharp
var nearest = GalaxyMapSelection.Nearest(GalaxyNavigationLevel.Galaxy,
    new Vector2(0.5f, 0.5f), candidates, 0.12f);
Assert.That(nearest.HasValue, Is.True);
Assert.That(nearest.Value.StableId, Is.EqualTo("community-a"));
```

- [x] **Step 2: Run the focused tests and confirm RED**

Run EditMode and PlayMode using the Unity commands from Task 1, changing `-testPlatform` and result paths. Expected: compile failure for the new public hover interface.

- [x] **Step 3: Implement preview and HUD state**

Return `GalaxyMapCandidate?` from the pure selector, reuse it for selection, and have the controller refresh a hover candidate whenever the map is open. Resolve labels only through `GalaxySurveyVisibilityController.CanLabel`; otherwise show `UNIDENTIFIED SIGNAL`. In the HUD render `MAP CURSOR`, the current bounded scope's object composition, and honest statuses such as `SERVER DID NOT PUBLISH A CONTINUATION CURSOR`.

- [x] **Step 4: Run EditMode and PlayMode and confirm GREEN**

Expected: both suites pass, including the existing Survey discovery journey and the new hover test.

- [x] **Step 5: Commit**

```bash
git add Assets/ClustrGalaxy/Runtime/Flight Assets/ClustrGalaxy/Runtime/UI/ProbeHud.cs Assets/ClustrGalaxy/Tests
git commit -m "feat: expose controller map selection feedback"
```

### Task 3: Repeatable Linux beta qualification

- [x] **Step 1: Write the failing report-verdict tests**

Add tests for command-line duration parsing and pure verdict thresholds: 60 FPS requires CPU and GPU p95 at or below 16.7 ms, no non-loading stall above 50 ms, zero steady-state GC bytes per frame, RAM below 4 GiB, and VRAM below 6 GiB when GPU memory is measurable.

```csharp
var verdict = GalaxyBetaProfile.Evaluate(report);
Assert.That(verdict.Qualified, Is.True);
Assert.That(GalaxyLaunchOptions.ProfileDurationSeconds(
    new[] { "--clustr-beta-profile=600" }), Is.EqualTo(600));
```

- [x] **Step 2: Confirm the new report boundary did not exist before implementation**

The failing tests were authored against APIs absent from the runtime. This
boundary was confirmed from the compile surface; implementation followed
before the next Unity import cycle, then the complete suite was run green.

- [x] **Step 3: Implement sampling and reporting**

Extend `GalaxyPerformanceMonitor` with a resettable sampling window, CPU/GPU frame timing, maximum stall, and peak GC. The opt-in runner waits for live smoke completion or a bounded warm-up, samples for the requested duration, writes `ClustrGalaxy/reports/latest-beta-profile.json` beneath `Application.persistentDataPath`, and logs `QUALIFIED` or exact failed thresholds. Normal launches do not create reports.

- [x] **Step 4: Run full tests, build, live smoke, and profile**

Run EditMode, PlayMode, and `GalaxyLinuxBetaBuilder.BuildLinuxBeta`. First run
a 30-second profile gate at 2560x1440. Run the full ten-minute command only if
the short gate has no structural qualification failures:

```bash
Builds/Linux/ClustrGalaxy.x86_64 --clustr-live-smoke --clustr-beta-profile=600 \
  -screen-fullscreen 0 -screen-width 2560 -screen-height 1440 \
  -logFile /tmp/clustr-galaxy-beta-profile.log
```

Observed: the live hierarchy reached `COMPLETE` at revision 1/catalog 3, the
report was valid JSON, and no unhandled exception occurred. The short gate
failed on unavailable GPU timing and recurring IMGUI HUD allocations, so the
ten-minute run was deliberately not spent reconfirming known failures. The
failure remains recorded as evidence and defines the next optimization wave.

- [x] **Step 5: Document and commit**

Update `README.md` with controls, profile command, report path, and the distinction between a fresh live traversal and backend fields the public API still omits. Commit only source, tests, settings, and documentation; exclude player binaries, logs, caches, and reports.

```bash
git add Assets/ClustrGalaxy README.md docs/superpowers/plans/2026-08-14-live-interaction-and-qualification-wave.md
git commit -m "test: qualify the live galaxy beta player"
```

## Final self-review checklist

- [x] Every accepted page belongs to the session's immutable revision and catalog.
- [x] Changing selection cannot merge a stale response.
- [x] No label reveals an unidentified Survey entity.
- [x] Missing public users, audit counts, or continuation cursors are shown as unavailable rather than synthesized.
- [x] EditMode, PlayMode, Linux build, live hierarchy smoke, and JSON profile evidence come from the final source state.
- [x] `git status --short` contains no generated player, cache, report, `Library`, `Temp`, or log artifacts.
