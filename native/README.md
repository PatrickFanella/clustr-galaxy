# Linux qualification telemetry

`python3 scripts/native_build.py` compiles `telemetry.c` with the host C compiler,
C11, optimization and warnings-as-errors. Validation and player build entrypoints
invoke it automatically. The generated shared library is ignored by Git; its
Unity plugin metadata is retained. Ubuntu builds require `build-essential`.

The plugin is preloaded but allocation instrumentation is opt-in for
`--clustr-beta-profile` or `--clustr-ui-performance`. It tries to enable Mono's allocation profiler before
managed execution. Late or unavailable instrumentation remains unavailable, and
the player checks a known allocation before using the counter. It does not infer
zero allocations from heap growth. References: [Mono profiler API](https://raw.githubusercontent.com/mono/mono/main/mono/metadata/profiler.h)
and [allocation initialization requirements](https://raw.githubusercontent.com/mono/mono/main/mono/metadata/profiler.c).

GPU memory uses Linux `/proc/self/fdinfo` `drm-total-<region>` requested-buffer
bytes, deduplicated by device and client ID. It excludes engine-cycle fields and
does not add resident aliases to total buffers. Missing data returns unavailable.
Shared buffers across distinct clients may be counted conservatively. Reference:
[Linux DRM usage statistics](https://docs.kernel.org/gpu/drm-usage-stats.html).

Neither telemetry mechanism grants qualification. The release player must prove
measurement availability, full sample coverage and all acceptance thresholds.
The plugin performs no network requests and does not read player save files.

Player memory uses the Linux process high-water RSS, including native allocations
and startup/loading peaks. The native callback reads `getrusage(RUSAGE_SELF)` and
converts `ru_maxrss` from KiB to bytes. Failure or nonpositive data is unavailable,
not zero usage. Reference: https://man7.org/linux/man-pages/man2/getrusage.2.html


For the currently observed complete GPU diagnostic path, use native Wayland/Vulkan
with the profiling scripts' explicit `--single-threaded-rendering` option. It adds
Unity's documented `-force-gfx-direct`; the player log must show
`kGfxThreadingModeDirect`. See the artifact-specific runs in
[the completion ledger](../docs/beta-completion.md). Default threaded rendering
has produced incomplete frame timings on this host; a successful direct-mode run
does not certify another mode. This option does not change normal launches or
relax any sample, duration or performance requirement.
