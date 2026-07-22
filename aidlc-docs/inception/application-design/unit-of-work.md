# Unit of Work

## Unit 1a: winsup/cygwin — Vanilla Build Enablement
- **Responsibility**: Get an unmodified `winsup/cygwin` checkout compiling into a working `msys-2.0.dll` (and `bash.exe` if feasible), using an isolated, project-local MSYS2 toolchain (per NFR-5). Verify the vanilla build behaves normally in an ordinary (non-sandboxed) shell.
- **Requirements covered**: FR-1, NFR-4 (Windows 10/11 target), NFR-5 (isolated/trusted-source toolchain).
- **Source changes**: None — vanilla checkout, build configuration/environment only.
- **Output**: A working `msys-2.0.dll` (+ `bash.exe` if feasible) build, plus the reproducible build procedure (recorded per the Build Environment Setup service in `services.md`).
- **Risk**: Highest in the engagement — explicitly flagged as the biggest unknown in the brief.

## Unit 1b: winsup/cygwin — AppContainer Patch
- **Responsibility**: Implement the AppContainer Capability Detector, AppContainer Namespace Resolver, and the modified Shared Parent Directory Provider (`get_shared_parent_dir()`/`get_session_parent_dir()` in `mm/shared.cc`), plus supporting OS/Token API wrapper extensions (`wincap.cc`, `advapi32.cc`, `autoload.cc`). Investigate and address further AppContainer-related startup walls found via Unit 2's harness.
- **Requirements covered**: FR-3, FR-4, NFR-1 (security isolation hard constraint), NFR-2 (fail-safe defaults), NFR-3 (non-sandboxed path tolerance), NFR-4.
- **Source changes**: New `winsup/cygwin/appcontainer.cc`/`.h`; modifications to `mm/shared.cc`, `wincap.cc`, `advapi32.cc`, `autoload.cc`/`ntdll.h` (per `components.md`).
- **Output**: Source patch/diff; a patched `msys-2.0.dll` (+ `bash.exe` if feasible) build.
- **Depends on**: Unit 1a's working vanilla build as the baseline to patch and rebuild from.

## Unit 2: AppContainer Test Harness
- **Responsibility**: Independently reproduce the documented AppContainer startup failure (FR-2) against Unit 1a's vanilla build, and validate the fix (FR-3/FR-4) plus run smoke tests (NFR-6) against Unit 1b's patched build.
- **Requirements covered**: FR-2, FR-4 (test execution side), NFR-6.
- **Components**: Profile Manager, Sandboxed Process Launcher, Scenario Library, Scenario Runner/Reporter, CLI Entry Point (per `components.md`).
- **Output**: A standalone C# console application; a findings/pass-fail report from each run.
- **Depends on**: No compile-time dependency on Unit 1a/1b. Runtime dependency only — needs a target executable path (via `--target <path>` runtime argument, per design decision) to actually exercise a build.

## Code Organization Strategy (Unit 2 — greenfield component)

Unit 2 is new code with no pre-existing structure to match, unlike Units 1a/1b which extend the existing `winsup/cygwin` layout.

- **Location**: `tools/appcontainer-harness/` (new top-level folder, per Application Design decision).
- **Project structure**: A single .NET 10 (`net10.0`) console project (per design decision — no separate class-library split). Internal organization within that one project, by folder:
  - `tools/appcontainer-harness/Profiles/` — Profile Manager
  - `tools/appcontainer-harness/Launching/` — Sandboxed Process Launcher
  - `tools/appcontainer-harness/Scenarios/` — Scenario Library
  - `tools/appcontainer-harness/Reporting/` — Scenario Runner/Reporter
  - `tools/appcontainer-harness/Program.cs` — CLI Entry Point
- **Target framework**: `net10.0` (resolved via Q2 — newest SDK already installed on the development machine, no extra install required).
- **Invocation**: `--target <path-to-bash.exe-or-msys-2.0.dll>` (required) plus `--scenario <name>` (single-scenario mode) or no scenario flag (full-suite mode), per Application Design's Q2/Q4 decisions.
