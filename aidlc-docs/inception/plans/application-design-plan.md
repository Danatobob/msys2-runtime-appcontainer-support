# Application Design Plan

## Context
Source: `aidlc-docs/inception/requirements/requirements.md` (FR-1..FR-4, NFR-1..NFR-7), `aidlc-docs/inception/plans/execution-plan.md` (2 planned units: winsup/cygwin patch, AppContainer C# test harness). No user stories exist (stage was skipped).

## Design Scope
Two components:
1. **winsup/cygwin AppContainer support** — additive changes to the existing runtime (`mm/shared.cc`, `wincap.cc`, `advapi32.cc`, `autoload.cc`/`ntdll.h`)
2. **AppContainer test harness** — a new, standalone C# component with no dependency on winsup/cygwin source

## Plan Checklist
- [x] Generate `components.md` with component definitions and high-level responsibilities
- [x] Generate `component-methods.md` with method signatures (business rules detailed later in Functional Design)
- [x] Generate `services.md` with service/orchestration definitions
- [x] Generate `component-dependency.md` with dependency relationships and communication patterns
- [x] Generate `application-design.md` consolidating the above
- [x] Validate design completeness and consistency

## Clarifying Questions

Please fill in each `[Answer]:` tag, then let me know when you're done.

### Question 1 — Component Identification (winsup/cygwin side)
Should the new AppContainer-detection/resolution logic live in a new dedicated file, or be added to existing files matching how the codebase currently organizes small helpers (capability flags in `wincap.cc`, token/SID work in `advapi32.cc`, namespace logic in `mm/shared.cc`)?

A) New dedicated file(s) (e.g. `appcontainer.cc` + `appcontainer.h`) grouping all new AppContainer logic together
B) Extend existing files in place (`wincap.cc`, `advapi32.cc`, `mm/shared.cc`) — matches current code organization, no new files
C) Hybrid: a new small file for namespace-path resolution logic only; capability detection added directly to `wincap.cc`/`advapi32.cc`
X) Other (please describe after [Answer]: tag below)

[Answer]: A

### Question 2 — Component Methods (test harness usage model)
How should the test harness be invoked?

A) Single CLI invocation per scenario (e.g. `harness.exe --target bash.exe --scenario echo`) — composable, scriptable from outside
B) One full-suite runner that executes all scenarios in one invocation and prints a pass/fail report
C) Both — a core library with a CLI wrapper supporting single-scenario and full-suite modes
X) Other (please describe after [Answer]: tag below)

[Answer]: C

### Question 3 — Service Layer / Build Orchestration
Phase 1 (FR-1) requires an isolated, project-local MSYS2 toolchain setup. Should that setup be:

A) A one-time manual/interactive setup performed now (downloading/extracting portable MSYS2), not represented as reusable code in the repo
B) A scripted setup component (e.g. a committed setup script) so the build environment can be reproduced later without re-deriving the steps
X) Other (please describe after [Answer]: tag below)

[Answer]: A

### Question 4 — Component Dependencies (harness location)
Where should the C# test harness live?

A) Inside this repository, in a new top-level folder (e.g. `tools/appcontainer-harness/`)
B) Outside this repository entirely, in a separate location
X) Other (please describe after [Answer]: tag below)

[Answer]: A

### Question 5 — Design Pattern (namespace resolution fix direction)
For the actual fix in `get_shared_parent_dir()`/`get_session_parent_dir()`, do you have a preference on approach?

A) Use `GetAppContainerNamedObjectPath` (the suggested direction in the brief) — preserves cross-process shared state for multiple processes under the same AppContainer profile
B) Fall back to process-local-only mode under AppContainer (simpler, but breaks multi-process shared state within the same profile — conflicts with the brief's explicit requirement that this must still work)
C) Let Functional Design decide based on what's actually feasible once Phase 2/3 findings are in
X) Other (please describe after [Answer]: tag below)

[Answer]: A
