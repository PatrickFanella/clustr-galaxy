# ADR 0007: Curated local presentation assets

Decision implemented September 19, 2026. Player acceptance and beta qualification remain separate gates.

## Context

Clustr needs clearer celestial surfaces, recognizable spacecraft and cockpit geometry while the asset budget is zero. Imported art must not own gameplay, change source relationships, introduce remote asset loading, or remove primitive fallbacks. Replacing the old ships exposed an accidental shader dependency in the observatory, demonstrating that editor-only asset checks are insufficient.

## Decision

Use a curated local subset of Kenney CC0 packs and attributed Solar System Scope CC BY 4.0 maps. Retain original bytes, SHA-256, source URLs, archive paths and license text under `Assets/ClustrGalaxy/Presentation/FreeAssets`. Copy notices and the manifest beside each Linux player.

The existing Addressables visual catalog owns the large surfaces, sounds and UI sprites. The small Resources demo fallback retains normalized ship/satellite prefabs and an explicit activity material. The activity material keeps its lit shader and emission variant available independently of imported ships; the observatory clones it with a guarded unlit fallback.

The editor builder produces centered unit-extent model prefabs with no colliders or gameplay components. Unity adapters apply them only as presentation. Celestial materials use a stable identity hash; mapped surfaces are artistic treatments rather than source facts. Shared near/middle/far sphere meshes select detail by screen coverage, accounting for Unity quality bias without changing global quality settings or physical radii.

Three bounded cockpit audio sources supplement the existing eight musical voices. Mute/volume settings apply to both; reduced audio and paused local simulation stop ambient engine/scanner loops. Interface confirmation remains quiet and bounded. Controller glyphs accompany text equivalents.

## Verification and consequences

Asset tests check import shape, mipmaps, deterministic material selection, prefab bounds, physics separation, fallback parity, shader retention, sounds and glyphs. The Linux visual journey checks real Addressables loading, near-detail selection and sound-source behavior. The campaign/restart journey must additionally pass because shader stripping and modal focus can fail only in a built player.

Build workers may require X even with `-nographics`; unattended builds use the existing isolated display harness. Historical failures remain in timestamped reports. No paid package, engine upgrade, source/API field, account or network submission is introduced by this decision.

See [asset notices](../free-asset-notices.md), [implementation plan](../superpowers/plans/2026-09-19-free-presentation-assets.md), and [completion ledger](../beta-completion.md).
