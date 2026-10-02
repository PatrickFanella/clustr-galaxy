# Physical-controller acceptance

This checklist records a real player's observations separately from virtual-controller tests and performance qualification. Passing automated journeys does not fill these boxes.

Run `python3 scripts/manual_acceptance.py --controller /dev/input/eventN` with the actual gamepad event path confirmed from `/proc/bus/input/devices`. This opens a visible 1280×720 demo with fresh evidence-owned storage. Only that gamepad device's button/axis event counts are recorded; keyboard input is not captured. Quit the player normally or close its window to finish the session report. The artifact manifest and player log accompany the report. Detection of physical input is evidence of input use, not automatic human acceptance.

- [ ] Start opens Actions; D-pad focus, A confirm and B back reach and leave essential screens without mouse rescue.
- [ ] Left-stick vertical thrust/yaw, right-stick pitch/roll, trigger forward/reverse thrust, bumper sideways thrust, boost and braking feel controllable.
- [ ] Switch Assist / Mastery / Autopilot with D-pad left/right and return to the preferred mode.
- [ ] Enter Survey, choose the technology system, target with A and hold X for the uninterrupted scan. Missing optional tuning does not block completion.
- [ ] Read a discovered clue in Campaign notebook, move between pages and close; local flight stays suspended while reading.
- [ ] Preview a playground and try a manual run. Record whether guidance, missed-gate recovery and retry are comfortable.
- [ ] Open the observatory, change selection, place a discovered item and close. Verify focus and flight resume predictably.
- [ ] Start/pause/resume or abandon an expedition; no next journey starts automatically.
- [ ] Open Local creations and leave it; import text entry may use the keyboard. Do not delete a file unless intentionally testing that control.
- [ ] Describe one enjoyable unguided moment and any confusing, uncomfortable or tedious interaction.

Acceptance remains pending until player feedback is recorded with its artifact identity. Campaign pacing, all-feature completion, 1080p/1440p legibility, source fidelity and full performance qualification remain separate checks.


## Expanded feature pass

Record which artifact was used and keep observations separate from fixture results. Text entry can use a keyboard; note any other mouse rescue.

1. In Discoveries, try ordinary held scanning and optional tuning, then automatic tuning. Record whether the reticle is comfortable with left yaw/lift and right pitch/roll. Try mute and reduced audio; visual feedback should remain understandable.
2. Discover the first harmonic or beacon clue without a fixture or saved completion. Follow/unpin it, read each observation and time the ordinary journey including reading and searching. Harmonic and beacon target 10–15 minutes; pause the archival trail and finish it after restarting. Record confusing hints rather than counting fixture reachability as pacing evidence.
3. Fly each scenic profile and all three playgrounds using normal controls. Try a missed gate, abandon, retry, preview, and a compatible ghost. Compare Assist and Mastery records. Note whether retrying feels appealing.
4. Create constellations at both membership boundaries (3 and 12 identified entities), move/link nodes with the controller, and inspect all six palettes and three musical presets. Save and restore; try queue request/cancel and request/confirm. View the exported postcard in Local Creations.
5. Visit each anomaly, then assemble and submit an interpretation in both evidence boards. No hidden correct answer is required. Review the original models and keepsakes for legibility and the intended cozy retro-space style.
6. Place each item kind in the six-slot observatory. Play a displayed constellation, open a replay and diary, return to the same focus, clear one display and verify its original remains saved.
7. Complete each expedition mood; pause one across restart. Check shorter/unavailable wording with sparse eligible data. Close the journal card intentionally; no new trip should begin by itself.
8. Save and remove a permanent bookmark, keep an automatic moment, capture a screenshot with Save/Discard, and view a transferred replay under a different local profile. Playback must not grant discoveries. Confirm missing/corrupt media leaves a usable library.
9. In an unguided session, describe one enjoyable moment and anything that felt tedious. Record discoverability, tuning comfort, course retry appeal, constellation expressiveness and freedom to wander separately from functional success.

The automated resolution journeys provide screenshots and controller traversal at 720p/1080p/1440p; they do not fill this human checklist. Any unfinished item remains open.


## Focused feature inspection with a copied fixture

The launcher accepts `--resolution 1280x720|1920x1080|2560x1440` and optional `--profile-from PATH`, where PATH is a completed deterministic journey's `ClustrGalaxy/profiles/v2` directory. It copies only saved JSON progress into new evidence-owned storage; the source is hashed before and after the session. It rejects symlinks and never copies or reads sibling v1 files. Original screenshots/replay files are not copied; record new media in the manual session. The report explicitly records `seeded_fixture_progress`.

Use this option for convenient inspection of unlocked creations, clues and displays. **A seeded inspection cannot establish fresh-start discoverability, campaign pacing or ordinary acquisition of rewards.** Run without the option for those journeys. Human acceptance remains pending until observations are recorded; input event counts alone cannot approve the features.
