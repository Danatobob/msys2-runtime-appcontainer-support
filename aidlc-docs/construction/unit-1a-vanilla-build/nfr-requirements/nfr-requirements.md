# NFR Requirements — Unit 1a (Vanilla Build Enablement)

## Applicability Summary
- **Scalability**: N/A — one-time local build-environment setup, not a running service.
- **Performance**: N/A — build wall-clock time is not a tracked requirement; only correctness of the resulting binary matters.
- **Availability**: N/A — no uptime/failover concept for a local build.
- **Usability**: N/A — no end-user interface.
- **Security**: Applicable — see below.
- **Reliability**: Applicable — see below.
- **Maintainability**: Applicable — see below.

## Security Requirements
- **Isolated install location**: The MSYS2 toolchain MUST be installed entirely under `.build-toolchain/` inside the project folder (`D:\trd\Programming\Git\msys2-runtime\.build-toolchain\`) — never system-wide (no `C:\msys64`, no `Program Files`), per the original requirement and NFR-5.
- **Trusted sources only**: The portable MSYS2 base archive MUST come from the official `msys2/msys2-installer` GitHub releases (or msys2.org's documented mirror of the same). All subsequent package installs MUST go through that MSYS2 instance's own `pacman` against its default (official) package repositories — no third-party/unofficial repos added.
- **Signature verification**: `pacman`'s default package signature verification (`SigLevel`) MUST remain enabled for all installs. Unattended-prompt flags (e.g. `--noconfirm`) are acceptable for skipping interactive Y/N prompts, but MUST NOT be used to disable or weaken signature checking itself.
- **Maps to**: NFR-5, Security Baseline SECURITY-10 (trusted sources, no unvetted third-party sources) and SECURITY-13 (artifact integrity via signature verification).

## Reliability Requirements
- **Reproducibility record**: The exact set of installed toolchain package versions MUST be captured to a manifest file after setup completes (e.g. via `pacman -Q`), even though the setup procedure itself remains manual (per Application Design decision — not a committed script). This lets a future rebuild identify exactly what toolchain produced a given build output.
- **Verification gate**: The vanilla build MUST be confirmed to behave normally in an ordinary (non-sandboxed) shell before it is trusted as a baseline for Unit 1b's patch or Unit 2's repro testing (per FR-1).

## Maintainability Requirements
- **Documented procedure**: The exact commands used to set up the isolated toolchain and run the build MUST be recorded in the Build and Test instructions (per `services.md`'s Build Environment Setup workflow), so the manual procedure is reproducible by inspection even without a committed script.
- **Manifest location**: The package-version manifest is stored as a documentation artifact (`toolchain-versions.md`, alongside this file) rather than committed inside `.build-toolchain/` itself, since that directory is build output/toolchain, not source.

## Tech Stack Decision Summary
See `tech-stack-decisions.md` for the concrete choices (acquisition method, install path, package set).
