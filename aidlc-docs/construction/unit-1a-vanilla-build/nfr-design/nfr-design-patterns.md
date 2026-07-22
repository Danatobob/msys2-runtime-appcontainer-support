# NFR Design Patterns — Unit 1a (Vanilla Build Enablement)

## Resilience Pattern: Clean-Slate Retry
- **Pattern**: On any failure during toolchain setup (archive download, extraction, or any `pacman` package install), the entire `.build-toolchain/` directory is deleted and setup restarts from scratch — no attempt to resume or patch a partially-installed environment.
- **Rationale**: A partially-installed MSYS2/mingw-w64-cross toolchain is unreliable to reason about (unclear which packages/dependencies landed vs. didn't); given this is a one-time manual setup with no automation to carefully track partial state, clean-slate retry is simpler and safer than incremental repair.
- **Applies to**: The manual Build Environment Setup procedure (per `services.md`).

## Security Pattern: Two-Layer Integrity Verification
- **Pattern**: Two independent integrity checks, not one:
  1. **Archive-level**: The downloaded portable MSYS2 base archive's published checksum (SHA256, as published alongside the `msys2/msys2-installer` GitHub release) is verified before extraction.
  2. **Package-level**: `pacman`'s own signature verification (`SigLevel`) remains enabled (untouched) for every subsequent package install, verifying each package against MSYS2's official signed package database.
- **Rationale**: The archive-level check guards against a corrupted/tampered download of the bootstrap environment itself; the package-level check (already required by NFR-5) guards every package pulled in afterward. Neither substitutes for the other — the archive check happens once before `pacman` even exists in the isolated environment.
- **Maps to**: NFR-5, Security Baseline SECURITY-10, SECURITY-13.

## Security Pattern: No-Elevation Install
- **Pattern**: The entire toolchain setup and build (archive extraction, `pacman` installs, `configure`/`make`) runs entirely within the project-local `.build-toolchain/` directory tree, requiring no administrator/elevated privileges and no writes outside the project folder.
- **Rationale**: Directly enforces the "isolated, no disk clutter" requirement — an elevated/system-wide install couldn't be cleanly undone by simply deleting a folder, and would risk touching shared system state.

## Isolation Pattern: Gitignored Toolchain Directory
- **Pattern**: `.build-toolchain/` is added to `.gitignore` at the project root, ensuring the (large, binary-heavy) toolchain is never accidentally staged or committed.
- **Rationale**: Keeps the toolchain purely local/disposable from version control's perspective while still being deliberately kept on disk for reuse during Unit 1b (per Question 2 decision).
