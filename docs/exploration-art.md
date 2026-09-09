# Original exploration asset collection

Art direction: cozy retro-space. The first mesh pass uses enamel housings, machined brass collars, dark instrument fins and warm glass elements. All geometry is original project-authored mesh data; there are no purchased packs or external texture dependencies.

Editable source: `Assets/ClustrGalaxy/Editor/ExplorationAssetBuilder.cs`. Run Unity menu **Clustr Galaxy → Author Exploration Assets** to regenerate the ten mesh/prefab assets. Material records and mesh identities are retained on regeneration. The lathe profiles, radial segment counts, torus cross-sections and assembly transforms are the editable source geometry. Unity `.asset`, `.mat` and `.prefab` files are the runtime outputs. Authorship: Clustr Galaxy project, generated with Codex assistance. Preview and player visual acceptance remain required; this initial pass is not final artistic acceptance.

Inventory: EchoReceiver, ShySatellite, PocketOrrery, WindowStation, LanternCluster, InvertedBeacon; keepsakes PocketResonator, WanderingLantern, WindowPrism; ObservatoryPlatform. The local visual catalogue carries these references through Addressables. Missing anomaly assets keep primitive fallbacks. `AuthoredAnomalyVisual` owns bounded presentation only; the campaign service owns discovery and durable progress.

An instrument dish sweeps, the shy satellite folds its panels while watched, orrery rails rotate independently, station windows illuminate in sequence, lanterns bob individually and the inverted beacon rotates below its housing. Reduced motion freezes all animation. No physical source entities or progression outcomes are created by these visual objects.

Preview and observatory stages use the isolated presentation layer near the origin. The earlier far-away stage quantized small mesh details; isolation is provided by camera layers and opaque UI rather than million-unit coordinates.
