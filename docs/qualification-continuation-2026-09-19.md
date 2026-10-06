# Remaining qualification gates — 2026-09-19

Status at 22:51 UTC: **not release qualified**. The next build step requires a
valid entitlement in the isolated Ubuntu environment. Human acceptance and
licensed Gitea execution also remain open.

## Verified source and retained candidate

The continuation checkout started clean at
`aec467716c36bc3d5adc3744ffd7aea3938efe3d`. The remote branch
`codex/free-presentation-assets-20260919` still resolved to that exact commit.
The tested game source remains `6f2546b0a7192e2f3695ab11d048109033bfb2d4`;
the later commit changes documentation only.

The existing package is under
`/home/onnwee/Work/subcult/clustr-galaxy/Builds/handoffs/6f2546b-free-assets`.
This continuation does not replace its manifest, checksum inventory, reports or
archive. The failed original full matrix and passing same-artifact stress repeat
retain their separate verdicts, as described in [the completion ledger](beta-completion.md).

## Fresh Ubuntu attempt

A separate clone of the exact tested commit started clean without a Library.
`python3 scripts/validate.py --mode EditMode` ran in the existing image
`sha256:7f22082132a3ff4f007bff06d663deb0d643ea9e041f4d772ad0cac7e696d246`,
which reports Ubuntu 24.04.4 LTS. The exact Unity installation and existing host
entitlement directory were mounted read-only. No entitlement activation, return,
machine-identity override or host license write was performed.

Unity exited **198** after 0.502 seconds, before test discovery. It reported
zero matching entitlements, missing `com.unity.editor.headless`, and
`No valid Unity Editor license found`. The wrapper exited 2 and retained its
failed result with missing XML. No Ubuntu build or Ubuntu-built runtime result
was produced. The container shares the host kernel; this is Ubuntu userland
evidence, not a separate Ubuntu-kernel machine.

Evidence root:
`/home/onnwee/Work/subcult/clustr-galaxy/Builds/evidence/20260919T225110Z-remaining-gates`.
See `preparation.json`, `ubuntu-result.json`, `ubuntu-console.log` and
`source/Builds/validation/20260919T225111336308Z/summary.json`.

## Live Gitea state

[Run 8602](https://git.subcult.tv/subculture-collective/clustr-galaxy/actions/runs/8602)
exists for the pushed documentation commit. Job 15841 is **queued**, requests
`unity-6000.4.10f1-linux`, has runner ID 0 and has executed no steps.
The repository runner endpoint returned no repository runners. The inspected
Kvant runner advertises Ubuntu and Kvant labels, but no Unity label. These
observations do not inventory every organization or instance runner.

Readbacks are retained in `actions-runners.json`,
`actions-runs-8602-jobs.json` and `runner-labels.json` under the evidence root.
No runner configuration or workflow was changed.

## Resume requirements

1. Provide an isolated Ubuntu 24.04 environment with Unity 6000.4.10f1, the
   Linux Standalone module and a working editor entitlement. Repeat validation
   from clean `6f2546b`, build both configurations, and verify those exact players.
   Preserve build-userland and runtime-host identities separately.
2. Provision the required Unity runner label only on a suitably isolated,
   licensed build machine. Record the hosted job and source identity when it
   executes; a queued workflow is not CI verification.
3. Run the [human acceptance checklist](manual-controller-acceptance.md) on the
   packaged candidate. No physical gamepad was identifiable in the current
   `/proc/bus/input/devices` inventory. Connect one and confirm its event path
   before launching the existing recorder:

   ```sh
   python3 scripts/manual_acceptance.py \
     --player /home/onnwee/Work/subcult/clustr-galaxy/Builds/handoffs/6f2546b-free-assets/release/ClustrGalaxy.x86_64 \
     --controller /dev/input/eventN \
     --window-system wayland \
     --resolution 1280x720
   ```

   Replace `eventN` with the confirmed gamepad device. The recorder creates fresh
   evidence-owned progress and records artifact identity. It leaves human
   acceptance pending until actual observations are supplied. Its result is a
   manual functional session, not another performance qualification.
