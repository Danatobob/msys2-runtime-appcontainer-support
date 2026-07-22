# Tech Stack Decisions — Unit 2 (AppContainer Test Harness)

## Target Framework
- **Decision**: `net10.0`, single console project, no separate class-library split (both already decided in Units Generation — `unit-of-work.md`).

## Win32/AppContainer API Access
- **Decision**: Use the `Microsoft.Windows.CsWin32` source generator (official Microsoft NuGet package) to generate P/Invoke signatures for `CreateAppContainerProfile`, `DeleteAppContainerProfile`, `CreateProcess`, `STARTUPINFOEX`, `PROC_THREAD_ATTRIBUTE_SECURITY_CAPABILITIES`, `GetTokenInformation`, and related structs, rather than hand-written `DllImport` declarations.
- **Rationale**: These APIs involve security-sensitive struct marshaling (tokens, security capabilities, process attribute lists) where hand-rolled P/Invoke is a common source of subtle bugs. CsWin32 is Microsoft-maintained, generates code at build time (not a runtime dependency shipped in the binary), and is standard practice for modern .NET Win32 interop.

## JSON Serialization
- **Decision**: `System.Text.Json` (built into .NET, no additional NuGet dependency) for writing `SuiteReport` to `last-report.json`.
- **Rationale**: Zero extra dependency footprint (Security Baseline SECURITY-10 — no unused/unnecessary dependencies) for a simple serialization need.

## CLI Argument Parsing
- **Decision**: Hand-written lightweight argument parsing (no CLI framework dependency like `System.CommandLine`).
- **Rationale**: The argument surface is small and fixed (`--target`, `--scenario`, `--expect`, `--verbose`, `--cleanup`) — a parsing library would be a dependency with no real payoff at this scale, and `System.CommandLine` is still pre-1.0/preview as of this engagement.

## Testing Approach
- **Decision**: No separate unit-test project for this tool's internals.
- **Rationale**: Consistent with the engagement's NFR-6 decision to skip Property-Based Testing in favor of real smoke-test runs — this harness's entire purpose *is* to be the integration-test mechanism for Units 1a/1b; validating it means actually running it against Unit 1a's real build output (which happens in Build and Test) rather than unit-testing its internals in isolation. Keeps the single-console-project structure simple, per the Units Generation decision.

## Dependency Summary
| Dependency | Type | Purpose |
|---|---|---|
| `Microsoft.Windows.CsWin32` | Build-time source generator (NuGet, not shipped at runtime) | AppContainer/process-creation P/Invoke signatures |
| `System.Text.Json` | Built into .NET 10 | Report serialization |

No other third-party packages. Matches NFR-5/SECURITY-10's supply-chain minimalism.
