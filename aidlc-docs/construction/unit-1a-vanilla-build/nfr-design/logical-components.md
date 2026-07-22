# Logical Components — Unit 1a (Vanilla Build Enablement)

## `.build-toolchain/` — Isolated Toolchain Directory
- **Purpose**: Self-contained, project-local MSYS2 + mingw-w64-cross toolchain used to build `winsup/cygwin`.
- **Structure**: `.build-toolchain/msys64/` (the portable archive's own top-level folder name after extraction).
- **Lifecycle**: Created once during Unit 1a's Build and Test execution; **kept in place** for the remainder of the engagement (per Question 2 decision) so Unit 1b can rebuild the patched source from the same known-good toolchain, and Unit 2's repro/validation runs have a stable, repeatedly-rebuildable target. Not committed to git (`.gitignore` entry).
- **Failure behavior**: Deleted and recreated from scratch on any setup failure (Clean-Slate Retry pattern, see `nfr-design-patterns.md`) — never left in a partially-installed state.

## `toolchain-versions.md` — Reproducibility Manifest
- **Purpose**: Records the exact `pacman -Q` package-version output captured immediately after a successful toolchain setup.
- **Location**: `aidlc-docs/construction/unit-1a-vanilla-build/nfr-requirements/toolchain-versions.md` (documentation artifact, not inside `.build-toolchain/` itself).
- **Lifecycle**: Written once per successful setup; overwritten if the toolchain is ever recreated from scratch (Clean-Slate Retry).

## Build Output — `msys-2.0.dll` (+ `bash.exe` if feasible)
- **Purpose**: The actual compiled artifact(s) FR-1 exists to produce.
- **Location**: Standard autotools build output location under the repo's own build tree (`winsup/cygwin/` build directory, or wherever `make`/`make install` places it — determined during actual execution, not fixed here).
- **Relationship to `.build-toolchain/`**: Produced *by* the toolchain but is itself a separate artifact — this is what Unit 2's harness will actually launch (via its `--target <path>` argument), not the toolchain directory itself.
