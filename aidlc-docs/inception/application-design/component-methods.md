# Component Methods

**Note**: Signatures below are high-level interface contracts for Application Design. Detailed business rules (exact error-handling branches, exact struct layouts) are defined in Functional Design (per-unit, CONSTRUCTION phase).

## Unit 1: winsup/cygwin AppContainer Support

### AppContainer Capability Detector (`appcontainer.cc`/`.h`)
- `bool appcontainer_current_process_is_sandboxed()`
  - **Purpose**: Returns whether the current process token is an AppContainer/LowBox token. Cached after first call.
  - **Input**: None (reads current process token internally).
  - **Output**: `bool`.

### AppContainer Namespace Resolver (`appcontainer.cc`/`.h`)
- `HANDLE appcontainer_resolve_shared_parent_dir()`
  - **Purpose**: Resolves (opens/creates) the AppContainer-private directory object to use as the shared-object namespace root, for the current process's AppContainer profile.
  - **Input**: None (reads current process token internally).
  - **Output**: `HANDLE` on success; caller (Shared Parent Directory Provider) treats a failure return the same way it treats today's `NtCreateDirectoryObject` failure — fail closed.

### Shared Parent Directory Provider (`mm/shared.cc`, modified)
- `HANDLE get_shared_parent_dir()` *(existing signature, unchanged — behavior extended)*
  - **Purpose**: Returns the cached global shared-object parent directory handle, now AppContainer-aware.
  - **Input**: None.
  - **Output**: `HANDLE`; aborts process (`api_fatal()`) on unrecoverable failure, as today.
- `HANDLE get_session_parent_dir()` *(existing signature, unchanged — behavior extended)*
  - **Purpose**: Returns the cached session-scoped shared-object parent directory handle, now AppContainer-aware.
  - **Input**: None.
  - **Output**: `HANDLE`; aborts process (`api_fatal()`) on unrecoverable failure, as today.

### OS/Token API Wrapper Extensions
- `bool wincap_t::has_appcontainer_support()` (or equivalent table entry in `wincap.cc`)
  - **Purpose**: Static OS-version capability gate (Windows 8+; effectively always true for the Windows 10/11 target).
  - **Input**: None.
  - **Output**: `bool`.
- `bool advapi32_is_appcontainer_token(HANDLE token)` (name illustrative — `advapi32.cc`)
  - **Purpose**: Wraps `GetTokenInformation(TokenIsAppContainer)` for a given token handle.
  - **Input**: `HANDLE token`.
  - **Output**: `bool`.

## Unit 2: AppContainer Test Harness (C#)

### Profile Manager
- `AppContainerProfile CreateProfile(string profileName, IEnumerable<Capability> capabilities)`
  - **Purpose**: Creates (or opens, if it already exists) an AppContainer profile with the given capabilities.
  - **Output**: A handle/wrapper object representing the profile (including its SID).
- `void DeleteProfile(AppContainerProfile profile)`
  - **Purpose**: Cleans up a previously created profile.

### Sandboxed Process Launcher
- `LaunchResult Launch(AppContainerProfile profile, string exePath, string arguments, TimeSpan timeout)`
  - **Purpose**: Launches `exePath` under the given AppContainer profile's token and waits (bounded by `timeout`).
  - **Output**: `LaunchResult` — exit code, stdout, stderr, whether it timed out.

### Scenario Library
- `IReadOnlyList<Scenario> GetAllScenarios()`
  - **Purpose**: Returns the full set of defined scenarios (bare startup, echo, control-flow script, fork/exec, file I/O, env vars, pipes/redirection).
- `Scenario GetScenario(string name)`
  - **Purpose**: Looks up a single named scenario.

### Scenario Runner / Reporter
- `ScenarioResult RunScenario(Scenario scenario, AppContainerProfile profile, string targetExePath)`
  - **Purpose**: Runs one scenario via the Launcher and evaluates actual vs. expected outcome.
- `SuiteReport RunAllScenarios(AppContainerProfile profile, string targetExePath)`
  - **Purpose**: Runs every scenario from the Scenario Library and aggregates results into a report.

### CLI Entry Point
- `int Main(string[] args)`
  - **Purpose**: Parses arguments; dispatches to `RunScenario` (single-scenario mode) or `RunAllScenarios` (full-suite mode); prints results; returns a process exit code reflecting overall pass/fail.
