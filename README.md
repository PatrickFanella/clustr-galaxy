# Clustr Galaxy

Clustr Galaxy is a Unity client for flying through [Clustr](https://github.com/subculture-collective/clustr)'s
map of Reddit communities. Subreddits become star systems, and the users,
posts and comments around them become the bodies you scan, inspect and travel
between. It is built for a controller first, with full keyboard and mouse
support.

It is in development. The target is a Linux beta, and that beta has not been
released or qualified yet. There are no published builds; you build it from
source with Unity.

## Features

- **Live data:** reads the published Clustr graph from the public API at
  `https://clustr.subcult.tv/api`, pinned to one graph revision and spatial
  catalog per session. It only reads; it never connects to a database.
- **Offline and demo modes:** a verified local cache is used when the API is
  unreachable, and a deterministic demo galaxy is available when neither is.
- **Explore and Survey:** Explore shows everything loaded. Survey starts from one
  subreddit and reveals routes and destinations as you scan them. Survey
  progress is saved per profile and per graph revision.
- **Flight:** Newtonian flight with gravity in three pilot modes (Autopilot,
  Pilot Assist and Pilot Mastery), system cruise to a selected planet, charged
  jumps between connected systems, and a galaxy and Survey map.
- **Inspection:** source details for subreddits, users and posts; comment
  replies appear as a tree in the HUD, up to three levels deep.
- **Constellations:** group 3 to 12 known entities into a named personal
  constellation, saved on your machine. The submission queue is local only;
  nothing is sent anywhere.
- **Flight activities:** ring run, figure-eight and stable-orbit courses,
  scenic route profiles, local personal bests and optional ghost replays.
- **Replays and captures:** a rolling 15-minute flight recorder, a local replay
  library with import and export (`.clustr-replay`, see
  [docs/replay-format.md](docs/replay-format.md)), screenshots and 1920×1080
  postcards.
- **Demo campaign:** authored clues near demo bodies, a campaign notebook and
  an evidence board for each mystery. Live mode offers personal notes instead.
- **Observatory and diary:** display slots for keepsakes, constellations and
  recorded flight paths, plus an automatic journey diary with bookmarks.
- **Local profiles:** separate save data per profile with
  `--clustr-profile=<name>`.

The full feature catalogue is in
[docs/exploration-features.md](docs/exploration-features.md) and the world
model in [docs/galaxy-world-model.md](docs/galaxy-world-model.md).

## Quick start

You need Unity `6000.4.10f1` with the Linux Build Support module and a valid
editor license, Python 3, and a C compiler (`build-essential` on Ubuntu) for
the native telemetry plugin.

To play in the editor, open Unity Hub, choose **Add > Add project from disk**,
select this directory, open it with `6000.4.10f1`, and press **Play** in any
scene.

To test and build from the command line:

```bash
export UNITY_EDITOR=/path/to/6000.4.10f1/Editor/Unity
python3 scripts/validate.py                                   # EditMode and PlayMode tests
python3 scripts/build.py --configuration development          # Builds/Linux/
python3 scripts/build.py --configuration release              # Builds/LinuxRelease/ClustrGalaxy.x86_64
python3 -m unittest discover -s scripts -p 'test_*.py'        # harness unit tests, no Unity needed
```

Logs and results are written under `Builds/`. `python3 scripts/full_pass.py`
runs the complete automated pass: both builds plus functional, controller,
persistence, visual and performance checks.
[docs/linux-qualification.md](docs/linux-qualification.md) covers runner setup
and the performance harness.

### Launch options

| Option | Effect |
| --- | --- |
| `--clustr-demo` | Start in the deterministic demo galaxy |
| `--clustr-offline` | Send no network requests; use the verified local cache only |
| `--clustr-profile=<name>` | Use a separate local profile (1–64 letters, digits, `-` or `_`) |
| `--clustr-live-smoke` | Walk the live hierarchy once, for testing |
| `--clustr-beta-profile=<seconds>` | Record a performance report (30–3600 seconds) and exit |

## Controls

| Action | Keyboard and mouse | Controller |
| --- | --- | --- |
| Forward/back thrust | W / S | Right / left trigger |
| Sideways thrust | A / D | Bumpers |
| Pitch / yaw | Mouse | Right stick vertical / left stick horizontal |
| Rise/fall | Space / Left Ctrl | Left stick vertical |
| Roll | Q / E | Right stick horizontal |
| Boost | Left Shift | Left-stick press |
| Brake | X | East button (B/Circle) |
| Target reticle candidate | F | South button (A/Cross) |
| Change pilot mode | `[` / `]` | D-pad left / right |
| Scan known target (hold) | R | West button (X/Square) |
| Galaxy map / Survey map | M / V | Select / D-pad up |
| Confirm Survey starting system | Enter | South button (A/Cross) |
| Charge route jump / plot autopilot | T / P | D-pad down / Right-stick press |

Open the command console with **Tab** or **Start**. Text fields use keyboard
and mouse; controller text entry is not supported.

## Project layout

```text
Assets/ClustrGalaxy/   Game code, scenes, presentation assets and tests
native/                Linux telemetry plugin used by performance runs
scripts/               Validation, build and automated test harness (Python)
docs/                  World model, features, replay format and ADRs
Packages/, ProjectSettings/   Unity package lock and project settings
```

## Contributing

- Report bugs and request features in
  [GitHub issues](https://github.com/subculture-collective/clustr-galaxy/issues).
- Ask questions and share ideas in
  [GitHub Discussions](https://github.com/subculture-collective/clustr-galaxy/discussions).
- Send code as a pull request on GitHub against `main`.

CI runs the Python harness unit tests on every pull request, and the `CI gate`
check must pass before a pull request can merge. Unity tests and builds need a
licensed editor, so run `python3 scripts/validate.py` and the builds locally
before opening a pull request that changes the game.

## License

Clustr Galaxy is licensed under the GNU General Public License v3.0. See
[LICENSE](LICENSE). Third-party art, models and sounds keep their own licenses;
see [docs/free-asset-notices.md](docs/free-asset-notices.md).
