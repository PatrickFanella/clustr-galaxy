# Free presentation assets

Budget: zero purchases. Preserve physics, controls, stable IDs, source provenance and gameplay.

1. Curate six Solar System Scope maps (CC BY 4.0), six Kenney ships, a dish satellite, one console model and its atlas, three sounds and two controller prompts (CC0). Keep source URLs, original archive paths, hashes and license files. Preserve unrelated imported assets.
2. Integrate shared deterministic celestial materials through the existing Addressables catalog, with procedural fallback. Add normalized presentation-only model prefabs and cockpit consoles without colliders. Retain the Resources demo ship fallback.
3. Add bounded engine/scanner/interface sound sources, respecting mute, volume, reduced audio and paused contexts. Add controller confirmation/back glyphs beside text; retain keyboard and non-Xbox text.
4. Verify catalog content, fallback and lifecycle in Unity tests; run EditMode/PlayMode, build an isolated Linux player, inspect all three resolutions and run simulation/demo journeys. Record exact evidence and remaining qualification requirements.

Status: implementation in progress. No player acceptance or updated release qualification is claimed. The previous ecba4ba artifact and its evidence remain historical and intact.

## Evidence and remaining gate

- Original file SHA-256 and license checks: passed, with 23 total Python harness tests.
- Standalone C# compile (Core, Unity adapter, Editor and EditMode assemblies): passed with no diagnostics against installed 6000.4.10f1 libraries. Evidence: `Builds/static-compile/`. This does not compile shaders or import assets.
- Six new EditMode contracts cover fallback, stable material assignment, texture import settings, normalized collider-free models, demo fallback parity and audio/glyph availability. Authored and statically compiled; not executed.
- Unity suites: blocked before import by Editor license exit 198, evidence `Builds/validation/20260919T164438242790Z/summary.json`.
- Outstanding: refresh Personal license, run `scripts/validate.py`, inspect generated imports/materials, build both configurations, run `scripts/visual_smoke.py` at all three resolutions, simulation/demo/restart and audio/accessibility journeys, and updated performance qualification. Human acceptance remains separate.

## License recovery and integration fixes

Unity Personal activation was restored on September 19. Import validation caught and repaired cubemap defaults on 2D textures, missing glyph references, and FBX authoring offsets. The builder now repairs stale import shapes and generated models are centered with a unit maximum extent.

Fresh validation `20260919T174433348728Z` passes 298 EditMode and 434 PlayMode tests. Both Linux configurations built successfully (`20260919T173503Z`), with the initial 1080p visual/flight journey passing. Screenshots exposed an existing low-quality LOD bias selecting coarse planets at close range: celestial detail now has shared 128/64, 32/16 and 16/8 spheres with screen-coverage thresholds normalized for quality bias. The visual runner checks near-detail visibility, Addressables content, engine/interface/scanner playback, mute and reduced audio.

Headless asset workers required X even with `-nographics`. `scripts/build.py` now supplies the existing private display harness when no display exists; set `WESTON_ROOT` to the isolated Weston installation if it is not system-installed. No visible desktop window is used. The interrupted run at `20260919T173235Z` is preserved.

Third-party diagnostics are kept in build logs: Unity Serialization obsolete EntityId conversions and unsupported unused XR shader variants. No project-code compiler diagnostics were reported. Updated source/artifact performance and complete player matrix remain separate gates until their reports pass.

Updated 1080p release-player run `20260919T174800451285Z-visual-smoke` passes all recorded boolean checks, including near-detail rendering and engine/interface/scanner/mute/reduced-audio assertions, approach, planet/moon orbit, floating origin, recovery and progress preservation. This is automated evidence, not human audio or visual acceptance.

The first complete matrix (`20260919T175015267896Z-full-pass`) exposed shader stripping in the observatory: removing old ship material dependencies made its runtime `Shader.Find(Lit)` return null. That interrupted constellation display and controller focus. The failed run is preserved. Activity rendering now has an explicit catalog material dependency (also retained in the small demo fallback), and the observatory can use an unlit fallback without constructing a material from null. A dedicated asset-contract test verifies the retained activity shader.

The repaired 1440p campaign (`20260919T180350070086Z-demo-journey`) passes create, resume and restart, including observatory/diary controller focus, all authored activities, saved creations and byte-preserved v1 sentinels. The activity material now explicitly sets the URP legacy color alias to avoid a source mutation during builds. Final qualification runs from a new clean checkout with no Library cache.
