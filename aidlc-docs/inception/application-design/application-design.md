# Application Design (Consolidated)

**See also**: `components.md`, `component-methods.md`, `services.md`, `component-dependency.md` for full detail. This document consolidates them.

## Overview
Two components, no shared build/link dependency, related only at runtime (harness launches the patched binary):

1. **winsup/cygwin AppContainer Support** — additive patch to `mm/shared.cc` (`get_shared_parent_dir()`, `get_session_parent_dir()`), backed by a new `appcontainer.cc`/`.h` pair, extending `wincap.cc` and `advapi32.cc`.
2. **AppContainer Test Harness** — new standalone C# console application under `tools/appcontainer-harness/`, with Profile Manager, Sandboxed Process Launcher, Scenario Library, Scenario Runner/Reporter, and CLI Entry Point components.

## Component Summary
| Component | Unit | New/Modified | Location |
|---|---|---|---|
| AppContainer Capability Detector | 1 | New | `winsup/cygwin/appcontainer.cc`/`.h` |
| AppContainer Namespace Resolver | 1 | New | `winsup/cygwin/appcontainer.cc`/`.h` |
| Shared Parent Directory Provider | 1 | Modified | `winsup/cygwin/mm/shared.cc` |
| OS/Token API Wrapper Extensions | 1 | Modified | `wincap.cc`, `advapi32.cc`, `autoload.cc` |
| Profile Manager | 2 | New | `tools/appcontainer-harness/` |
| Sandboxed Process Launcher | 2 | New | `tools/appcontainer-harness/` |
| Scenario Library | 2 | New | `tools/appcontainer-harness/` |
| Scenario Runner/Reporter | 2 | New | `tools/appcontainer-harness/` |
| CLI Entry Point | 2 | New | `tools/appcontainer-harness/` |

## Key Design Decisions (from application-design-plan.md)
1. New AppContainer logic goes in dedicated new files (`appcontainer.cc`/`.h`), not scattered across existing files.
2. The test harness supports both single-scenario CLI invocation and full-suite mode via one core library + CLI wrapper.
3. MSYS2 toolchain setup (Phase 1) is a one-time manual procedure, not a committed script — recorded in build-and-test instructions instead.
4. The harness lives in-repo under `tools/appcontainer-harness/`.
5. The fix uses `GetAppContainerNamedObjectPath` to preserve cross-process shared state within the same AppContainer profile (not a process-local-only fallback).

## Services / Orchestration Summary
- **Build Environment Setup** (manual, Unit 1) — establishes the isolated project-local toolchain.
- **Shared Namespace Resolution Workflow** (internal runtime, Unit 1) — the actual startup-time fix logic: detect → resolve (AppContainer) or existing-path (normal) → cache → fail closed on error.
- **AppContainer Validation Workflow** (Unit 2, via CLI Entry Point) — create profile → launch target under profile → run scenario(s) → report → clean up. Used against both the vanilla build (Phase 2 repro) and the patched build (Phase 3/4 validation).

## Dependency Summary
- No compile/link-time dependency between Unit 1 and Unit 2.
- Unit 1 internal dependency chain: `dcrt0.cc` → Shared Parent Directory Provider → (AppContainer Capability Detector + AppContainer Namespace Resolver) → (`advapi32.cc`, `wincap.cc`, possibly `autoload.cc`). Downstream named-object consumers (`kernel32.cc`, `pinfo.cc`, `flock.cc`, `fhandler/*`) are unaffected/transparent.
- Unit 2 internal dependency chain: CLI Entry Point → (Profile Manager, Scenario Runner/Reporter) → (Sandboxed Process Launcher, Scenario Library).
- Cross-unit relationship is runtime-only: Unit 2 launches Unit 1's build output as an external process and observes exit code/stdout/stderr.

## Open Items Deferred to Functional Design
- Exact mechanism for binding `GetAppContainerNamedObjectPath` (static link vs. `autoload.cc`-style lazy binding).
- Exact struct/error-handling details for the AppContainer Capability Detector's caching mechanism.
- Full enumeration and pass/fail criteria for each Scenario Library entry (NFR-6 lists the minimum required scenarios; exact expected-output definitions are a Functional Design concern).
