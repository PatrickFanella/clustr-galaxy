# Free presentation asset notices

This asset pass costs $0 and adds no paid packages. Original downloaded files and their hashes are listed in `Assets/ClustrGalaxy/Presentation/FreeAssets/provenance.json`. Only a small curated subset is imported; archive item counts are not claims about implemented content.

| Creator / pack | Included content | License | Source |
|---|---|---|---|
| Kenney Space Kit | Six craft models and one satellite dish | CC0 1.0 | https://kenney.nl/assets/space-kit |
| Kenney Space Station Kit | Wide console model and color atlas | CC0 1.0 | https://kenney.nl/assets/space-station-kit |
| Kenney Sci-fi Sounds | Engine and scanner recordings | CC0 1.0 | https://kenney.nl/assets/sci-fi-sounds |
| Kenney Interface Sounds | Interface click | CC0 1.0 | https://kenney.nl/assets/interface-sounds |
| Kenney Input Prompts | A and B reference glyphs | CC0 1.0 | https://kenney.nl/assets/input-prompts |
| Solar System Scope / INOVE | 2K Sun, Mars, Jupiter, Moon, Mercury and Neptune maps | CC BY 4.0 | https://edu.solarsystemscope.com/textures/ |

Solar System Scope textures are licensed under Creative Commons Attribution 4.0 International: https://creativecommons.org/licenses/by/4.0/. The downloaded image files are unmodified. Unity compresses textures; Clustr maps them onto artistic celestial bodies and supplies its own illumination. Their use does not claim a correspondence between real planets and Clustr entities or source metrics.

Kenney license: https://creativecommons.org/publicdomain/zero/1.0/. Models are normalized, assigned project-compatible materials and assembled into the cockpit. Audio is imported as mono and mixed quietly. Glyphs accompany text equivalents. Each selected pack's original license file is retained alongside its content.

The build copies this document, the source/hash manifest, and all six license files into `AssetNotices/` beside the player. Retain these files when redistributing. These notices cover this new asset pass; existing Unity/package and previously imported asset notices still apply.

## Runtime behavior

The catalog supplies five shared planet materials and one stellar material. Selection is deterministic by stable entity identity and changes no scale, orbit, gravity, relationship, visibility or source data. Missing maps/catalogs preserve procedural surfaces. Generated models have no physics colliders or gameplay scripts.

Three bounded cockpit sound sources are separate from the existing eight musical voices. Engine/scanner loops stop under mute, reduced audio or paused local simulation. Interface feedback respects mute and volume; reduced audio keeps its quiet confirmation cue. Audio volume controls both music and cockpit feedback. No sound is evidence of a committed discovery; existing visual feedback remains authoritative.

## Verification record

Current verification results and unresolved release gates belong to `docs/beta-completion.md` and `docs/superpowers/plans/2026-09-19-free-presentation-assets.md`. Asset licensing does not establish player acceptance or release qualification.
