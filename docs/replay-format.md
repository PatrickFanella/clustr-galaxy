# Local journey replay format

Status: versioned codec, recording, local playback, import selection and storage management are implemented. Full F12 acceptance remains pending.

Versions 1, 2 and 3 use a fixed little-endian declarative binary record, extension `.clustr-replay`. It contains no serialized object types, code, external assets, network references or progression operations. The codec cannot access a session or provider. Unknown versions and trailing fields are rejected. Version 3 adds bounded declarative annotations.

| Field | Encoding |
|---|---|
| Magic | UInt32 `0x52544743` (`CGTR`) |
| Format version | Int32, readers accept 1, 2 and 3; writers emit 3 |
| Snapshot revision, catalogue | Two positive Int64 values |
| Storage namespace | UInt16 UTF-8 byte length followed by at most 128 bytes |
| World mode, authored content version | Two Int32 values |
| Flight tuning ID | Bounded UTF-8 string |
| Flight mode | Int32 enum |
| Course ID, course version | Bounded UTF-8 string and Int32; empty/zero for free flight |
| Course anchor (versions 2+) | One byte 0/1; when 1, three Float64 values containing the original course anchor |
| Pose count | Int32, 1–9,000 |
| Each pose | Four Float64 values (seconds, world x/y/z), four Float32 values (quaternion x/y/z/w); 48 bytes |
| Annotation count (version 3) | Int32, 0–256 |
| Each annotation | Float64 seconds, Int32 event kind, bounded UTF-8 display label |

Identity strings accept letters, digits, hyphens and underscores only. The format has no arbitrary URL, file-path, executable or asset field. Seekable input is bounded to 64 MiB before parsing. Pose payload length must match its declared count exactly. NaN/infinity, excessive coordinates, invalid quaternion lengths, decreasing/too-dense timestamps and durations over 900 seconds are rejected before a replay is returned.

The first pose is time zero. Recording resamples ordinary input frames at 10 Hz into a preallocated 9,000-slot ring. Long input gaps remain gaps: playback holds the previous pose rather than inventing motion through loading or paused editing. Logical double coordinates are reconstructed from floating-origin offset plus local position. Capture copies the buffer only on explicit save; later recording cannot mutate the saved interval.

A competitive ghost requires a nonempty matching course ID/version, snapshot, authored content version, flight tuning, world mode and Assist/Mastery mode. Autopilot is never competitive. Other valid files are presented as labelled standalone playback; the codec itself performs no gameplay loading or awards.

Permanent files are written through a unique temporary sibling, flushed to disk, then renamed without overwriting existing files. Failed saves remove only their own temporary file and leave the recorder available for retry. Explicit saving occurs under `ClustrGalaxy/replays` in the application's local storage directory. The local library supports browsing, explicit deletion, import selection and storage reporting.

Completed manual activities can now explicitly save recordings under `ClustrGalaxy/ghosts`. The local viewer reads the latest saved file on user request, shows a top-down path projection, and supports play/pause, restart, timeline seeking, 0.5×/1×/2× speed and back. Flight input is suspended while viewing and restored on close. This explicit file action provides standalone historical playback. Separately, the off-by-default Save personal-best ghosts preference embeds a complete competitive recording and its double-coordinate course anchor in the best-score companion entry. Compatible recordings restore during practice, translated to the new course anchor. No separate replay file is automatically created. Failed companion persistence retains both the previous score and its recording; older records without a ghost still load. The run retains its original activity/snapshot identity even after a world-mode change. Course recording tuning identities hash effective serialized translation/rotation controls, the physics timestep and the initial gravity-mode setting, with an explicit flight-model version.

Version-1 packages remain readable but have no explicit frame, so import offers standalone playback. Version-2 complete playground exports carry their original double-coordinate course anchor. The three current playgrounds can use an explicitly selected imported ghost when course/content versions, snapshot, world mode, flight mode and tuning match. Selection is transient, attaches on the next matching start/restart, clears on world-mode change/session teardown and never writes progression. Truncated rolling intervals omit competitive frame metadata. Scenic exports remain standalone until their complete route geometry identity is represented. Version 3 annotations support personal bookmarks, committed discoveries and completed activities. Labels are plain display text bounded to 64 characters and 128 UTF-8 bytes. Controls, markup, slash/colon path or URL syntax, unsupported kinds and events outside the recorded interval are rejected. The preallocated 256-event ring drops its oldest event at capacity; capture normalizes event timestamps to the saved pose window. Imported annotations are playback only and never dispatch progression. Older packages load with an empty annotation list.
