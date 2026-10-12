# Native CI availability

Unity qualification requires a trusted runner exposing `unity-6000.4.10f1-linux` with the intended Unity installation and licensing. Until that exists, the workflow reports an unavailable native gate on a Linux runner.

After runner/toolchain verification, set the repository variable `UNITY_RUNNER_ENABLED=true`. The native job retains harness tests, exact-checkout compilation/tests and Linux player builds. The availability flag alone is not Unity qualification; the native job must also pass.
