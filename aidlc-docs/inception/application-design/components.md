# Components

## Unit 1: winsup/cygwin AppContainer Support

### AppContainer Capability Detector
- **Purpose**: Determine whether the current process is running under a Windows AppContainer (LowBox) token.
- **Responsibilities**: Query the process token via `GetTokenInformation`; cache the result per-process (following the existing `shared_parent_dir` cached-static pattern in `mm/shared.cc`) so the check runs once per process, not once per call.
- **Location**: New file `winsup/cygwin/appcontainer.cc` + `winsup/cygwin/appcontainer.h` (per design decision — new dedicated files, not scattered into existing files).

### AppContainer Namespace Resolver
- **Purpose**: Resolve the AppContainer's own private, permitted object-manager namespace path, for use as the shared-object parent directory.
- **Responsibilities**: Wrap `GetAppContainerNamedObjectPath` (from `securityappcontainer.h`) given the current process token; return a handle/path usable as the new root for `get_shared_parent_dir()`/`get_session_parent_dir()`. Preserves cross-process sharing for multiple processes launched under the *same* AppContainer profile (per design decision).
- **Location**: Same new file(s) as the Capability Detector (`appcontainer.cc`/`appcontainer.h`).

### Shared Parent Directory Provider (modified, existing)
- **Purpose**: Owns creation/caching of the process's root handle(s) into the NT object-manager namespace — unchanged responsibility, extended behavior.
- **Responsibilities**: `get_shared_parent_dir()` and `get_session_parent_dir()` (`winsup/cygwin/mm/shared.cc`) now consult the AppContainer Capability Detector first; if the process is running under an AppContainer, delegate to the AppContainer Namespace Resolver instead of building the literal `\BaseNamedObjects\...`/`\Sessions\BNOLINKS\...` path. Non-AppContainer behavior is preserved, with one additional cheap capability check added at startup (per NFR-3, minor differences are acceptable). Failure to resolve a namespace path in either case still fails closed via the existing `api_fatal()` path (per NFR-2) — no fallback that would compromise isolation.
- **Location**: Existing `winsup/cygwin/mm/shared.cc` (modified, not replaced).

### OS/Token API Wrapper Extensions (modified, existing)
- **Purpose**: Provide the underlying native/Win32 API access the two new components above need, following the codebase's existing wrapper-layer conventions.
- **Responsibilities**:
  - `wincap.cc`: extend the OS-version capability table with a static "OS supports AppContainer APIs" flag (Windows 8+ gate; effectively always true given the Windows 10/11-only target in NFR-4, but kept consistent with the existing capability-table pattern).
  - `advapi32.cc`: add a `GetTokenInformation`-based wrapper for reading `TokenIsAppContainer`, used by the Capability Detector.
  - `autoload.cc`/`local_includes/ntdll.h`: extend only if `GetAppContainerNamedObjectPath` needs lazy binding rather than static linking (an open technical detail — see Functional Design).
- **Location**: Existing `winsup/cygwin/wincap.cc`, `winsup/cygwin/advapi32.cc`, `winsup/cygwin/autoload.cc`.

## Unit 2: AppContainer Test Harness (new, C#)

### Profile Manager
- **Purpose**: Manage the lifecycle of Windows AppContainer profiles used for testing.
- **Responsibilities**: Create, open, and delete AppContainer profiles (`CreateAppContainerProfile`/`DeleteAppContainerProfile`) with the capabilities needed to reproduce and validate the fix.
- **Location**: `tools/appcontainer-harness/` (new, in-repo).

### Sandboxed Process Launcher
- **Purpose**: Launch a target executable under a given AppContainer profile's token.
- **Responsibilities**: Build `STARTUPINFOEX` with `PROC_THREAD_ATTRIBUTE_SECURITY_CAPABILITIES`, call `CreateProcess`, capture exit code, stdout, and stderr for later comparison.
- **Location**: `tools/appcontainer-harness/`.

### Scenario Library
- **Purpose**: Define the concrete test scenarios the harness can run.
- **Responsibilities**: Encapsulate each scenario (bare startup, echo script, control-flow script, fork/exec/subshell, file I/O, environment variables, pipes/redirection — per NFR-6) as a named, self-contained definition with its expected outcome.
- **Location**: `tools/appcontainer-harness/`.

### Scenario Runner / Reporter
- **Purpose**: Execute one or more scenarios and report results.
- **Responsibilities**: Drive the Sandboxed Process Launcher against a chosen scenario (or all scenarios), compare actual vs. expected outcome, and produce a pass/fail report per scenario.
- **Location**: `tools/appcontainer-harness/`.

### CLI Entry Point
- **Purpose**: User-facing entry point for the harness.
- **Responsibilities**: Parse command-line arguments and dispatch to either single-scenario mode (`--target <exe> --scenario <name>`) or full-suite mode (run all scenarios, print a report) — per design decision to support both.
- **Location**: `tools/appcontainer-harness/`.
