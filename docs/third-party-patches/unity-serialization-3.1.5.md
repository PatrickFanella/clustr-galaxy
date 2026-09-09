# Serialization 3.1.5 compatibility with Unity 6000.4.10f1

The project embeds the locked 3.1.5 package, including its original license and
upstream tests. No package version is upgraded. Only the two editor-only GUID
adapters listed in the adjacent JSON manifest differ from the verified upstream
archive: `UnityEditor.GUID` becomes `UnityEngine.GUID` (eight references per file).
This is the same replacement Unity's API updater performed during the first
clean import. The registry still reports 3.1.5 as its latest release.

Without the patch, a new Library first emits 14 CS0234 errors, then the API
updater changes these two files in PackageCache and recompiles successfully.
That produces a failed strict validation result even though its tests run.
Embedding the already required API correction makes the first compile usable
without suppressing diagnostics or depending on an earlier failed run.

The original package contents were verified against the registry SHA-1 and the
SHA-256 recorded in the JSON manifest. All other 337 files are unchanged.
Keep this patch limited to the pinned editor. To roll it back, remove the
embedded package and restore its registry entry in packages-lock.json; the
original cold-import API-update failure will return. A future official package
fix should replace this embedding only with fresh clean-import evidence.

Validation status: implemented; fresh-import validation pending. This patch does
not establish Linux release qualification or hosted CI acceptance.
