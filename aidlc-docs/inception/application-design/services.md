# Services

**Note**: Neither unit is a network service. "Services" here means orchestration workflows/entry points that coordinate the components in `components.md`.

## Build Environment Setup (manual procedure, Unit 1)
- **Definition**: A one-time, manual setup procedure (per design decision — not committed as a reusable script) to establish an isolated, project-local MSYS2 toolchain, per FR-1 and NFR-5.
- **Responsibilities**: Extract/install a portable MSYS2 environment rooted under the project folder; install the bootstrap packages (`msys2-devel`, `base-devel`, `autotools`) and the `mingw-w64-cross-*` toolchain from official MSYS2 repositories only; run `winsup/autogen.sh` → `configure` → `make` to produce the vanilla build.
- **Orchestration**: Performed directly (not via a committed script) during Build and Test for Unit 1; the exact commands are recorded in the build-and-test instructions for reproducibility, satisfying NFR-5's traceability intent without adding a maintained script artifact.

## Shared Namespace Resolution Workflow (Unit 1, internal runtime service)
- **Definition**: The internal startup-time workflow realized inside `get_shared_parent_dir()`/`get_session_parent_dir()`.
- **Responsibilities/Orchestration**: On first call — (1) AppContainer Capability Detector checks the current token; (2a) if AppContainer, delegate to AppContainer Namespace Resolver; (2b) if not, fall back to today's existing literal-path + `NtCreateDirectoryObject` logic unchanged; (3) cache the resulting handle for the process's lifetime (existing pattern, unchanged); (4) on any unrecoverable resolution failure, fail closed via `api_fatal()` (NFR-2). This workflow is triggered transitively by `memory_init()` on every process start, fork, and exec (`dcrt0.cc`) — no changes needed to `dcrt0.cc` itself.

## AppContainer Validation Workflow (Unit 2, realized by the CLI Entry Point)
- **Definition**: The harness's own orchestration for reproducing the failure (FR-2) and validating the fix (FR-3/FR-4).
- **Responsibilities/Orchestration**: (1) Profile Manager creates/opens an AppContainer profile; (2) Scenario Runner drives the Sandboxed Process Launcher to run the requested scenario(s) against the target `bash.exe`/`msys-2.0.dll` build under that profile's token; (3) Scenario Runner/Reporter compares actual outcome to expected outcome per scenario and produces a report; (4) Profile Manager cleans up the profile. Runs in two distinct modes against two distinct builds over the engagement's lifetime: against the *vanilla* build (Phase 2, expecting the documented failure) and against the *patched* build (Phase 3/4, expecting success plus the smoke-test scenarios from NFR-6 to pass).
