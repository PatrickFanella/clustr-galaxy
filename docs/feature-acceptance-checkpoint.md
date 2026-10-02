# Feature acceptance checkpoint — 2026-09-08 UTC

**The current automated feature subset passes. The feature catalogue is not yet player accepted, and the Linux beta is not release qualified.** This checkpoint covers the feature-completion scope; original release gates remain mandatory and separate. Older artifact reports remain historical evidence.

This checkpoint is **historical for the visual runtime**: the subsequent [flight-deck and world-scale iteration](visual-direction.md) changes the player artifact. Its prior performance and journey reports apply only to the artifact identified here. The player has accepted the controls for now and requested a substantial presentation improvement; feature and artistic acceptance remain open.

## Source and player

- Branch: `codex/linux-beta-qualification`.
- Base source SHA: `775f826f51ee70d8f25e3e421887eaa5a6f10801`; **working tree**, not clean-checkout evidence.
- Release-configured artifact tree SHA-256: `c92f1c09967d0225d0124d2dcb463bdc1f8d218a8b4e5966f89208fc8d610af8`.
- [Build manifest and per-file checksums](../Builds/build-logs/20260908T032713Z/BuildLinuxRelease.json).
- [Machine-readable checkpoint](../Builds/evidence/20260908T035331718651Z-feature-checkpoint/summary.json).

The approved flight-v3 controls and acceleration/braking tuning are retained. Public publishing and provider/backend changes are outside this work.

## Implemented continuation

- Permanent explicit bookmarks survive automatic diary eviction. Keep/remove, failure/retry, legacy v2 restoration and intent replay are covered.
- Replay format 3 adds bounded declarative annotations while reading versions 1/2. Separate-profile imports remain display-only and cannot award progression.
- All three expedition moods now have a three-process pause/resume/completion/restoration fixture journey, including honest unavailable Survey connections and separate Explore records.
- Live-mode personal notes support durable saving, paging and diary return under a labelled provider fixture. This does not establish public-provider compatibility.
- Actions are grouped; the observatory uses displayed constellation presets; anomaly audio has matching visual cues and bounded voices; celebrations track stable entities, remain pooled and retain zero-allocation measured flight. A camera-relative stroke minimum repairs distant constellation-trace legibility.
- The manual launcher supports all three resolutions and an optional independent copy of fixture v2 progress for convenient feature inspection. It explicitly labels seeded runs and preserves source progress; fresh-start pacing still needs a fresh run.

## Evidence for this artifact

| Check | Result and evidence |
|---|---|
| Unity validation | [215 EditMode / 16 PlayMode passed](../Builds/validation/20260908T032506862861Z/summary.json), no compiler diagnostics. |
| Harness | Nine Python tests passed; `git diff --check` passed. |
| 1280×720 | [Create, resume and restart passed](../Builds/evidence/20260908T033521962275Z-demo-journey/summary.json). |
| 1920×1080 | [Create, resume and restart passed](../Builds/evidence/20260908T034611911491Z-demo-journey/summary.json). |
| 2560×1440 | [Create, resume and restart passed](../Builds/evidence/20260908T032848462106Z-demo-journey/summary.json). |
| Separate-profile replay/media journey | [Passed](../Builds/evidence/20260908T035237793255Z-recipient-import/summary.json): no progression files, source replay preserved, malformed/missing replay and corrupt-image recovery, v1 sentinel unchanged. |
| Editor/observatory UI measurements | [Passed in the recorded direct-rendering configuration](../Builds/evidence/20260908T034252464766Z-ui-performance/summary.json). Complete GPU samples: 3,742/3,742 and 3,653/3,653. Entry/interaction/exit are UI intervals; their allocations are explicitly reported. |
| Active-feature 30-second stress | [All numerical preflight requirements met](../Builds/evidence/20260908T034451278359Z-hierarchy/summary.json); qualification still fails working-tree provenance. 14,053 nodes, 57,000 routes, 3,325/3,325 GPU samples, CPU p95 12.48 ms, GPU p95 0.86 ms, longest frame 19.98 ms, ordinary-flight allocation zero. |

All nine resolution launches exercise deterministic fixture placement and virtual-controller traversal. They preserve v1 sentinels, saved creations/media and restart state and reject runtime exception/script-error markers. They do not measure ordinary discovery pacing, physical-controller usability or enjoyment.

The complete measurement path observed here is **native Wayland / Vulkan / `-force-gfx-direct`**, requested with `--window-system wayland --single-threaded-rendering` on the profiling scripts. Both logs confirm `kGfxThreadingModeDirect`. Default-threaded runs remain preserved as failures with incomplete GPU capture. These results apply to the recorded configuration; they do not certify another rendering mode or replace 600-second release qualification.

## Required acceptance still open

1. **Hands-on feature and accessibility acceptance:** physical-controller menus/editing, tuning comfort, muted/reduced-motion feedback, full 3–12 member boundaries and queue flow. The controller was absent at the last input inventory. Earlier approval of flight controls does not approve the new feature catalogue.
2. **Ordinary-play pacing and enjoyment:** discover trails without fixture placement, measure the two 10–15 minute trails, resume the longer trail, fly scenic/playground courses normally, assess retries and expedition lengths, and record an enjoyable unguided session.
3. **Art and presentation acceptance:** review the six anomalies, keepsakes, observatory exhibits and all four celebrations. The distant trace has a verified legibility correction; artistic quality is not automatically accepted.
4. **Public-provider journey:** the [latest bounded manifest probe](../Builds/evidence/20260908T023056227038Z-provider-probe/summary.json) returned HTTP 403. Provider fixtures do not close this gate.

Use the [physical-controller checklist](manual-controller-acceptance.md) to record observations with the artifact identity. The [catalogue](exploration-features.md) and [completion ledger](beta-completion.md) retain per-feature contracts and historical results.

Original release work remains open: clean-revision development/release artifacts, full 600-second stress and pinned-live reports, exact-editor Ubuntu build/runtime verification, licensed CI activation/result, packaging/notices/checksums and the complete release checklist. No public release was published.
