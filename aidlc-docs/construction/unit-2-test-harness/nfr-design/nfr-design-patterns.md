# NFR Design Patterns — Unit 2 (AppContainer Test Harness)

**Note**: No clarifying questions needed for this stage (see question-scope memory) — patterns below resolved directly.

## Resilience Pattern: Idempotent Find-or-Create Profile
- **Pattern**: Profile Manager always attempts to open the fixed-name profile first; only calls `CreateAppContainerProfile` if the open fails with "not found". If a profile exists but appears to be in an unusable state (e.g. SID lookup fails), the harness deletes and recreates it rather than trying to repair it in place — mirrors Unit 1a's Clean-Slate Retry pattern.
- **Rationale**: Reused profiles are the design (BR-7), but "reuse" must not mean "blindly trust a possibly-corrupt profile."

## Resilience Pattern: Guaranteed Process Cleanup on Timeout
- **Pattern**: When a launched process exceeds the 15s timeout (BR-4), the Sandboxed Process Launcher forcibly terminates the process tree (not just the top-level process — fork/subshell scenarios may spawn children) before returning control to the Scenario Runner.
- **Rationale**: A timed-out AppContainer-sandboxed process left running could interfere with the *next* scenario launch under the same reused profile (BR-7's reuse design means scenarios aren't fully isolated from each other at the profile level).

## Security Pattern: Capability-Free, ACL-Scoped Profile
- **Pattern**: The AppContainer profile requests zero `SECURITY_CAPABILITIES`; all filesystem access is granted narrowly via `icacls` on exactly two folders (`bin`, `data` under `E:\Temp\appcontainer-harness\`), nothing else.
- **Rationale**: Directly implements least-privilege (SECURITY-11) — the profile can do nothing beyond running the staged binaries and read/writing its two scratch folders.

## Security Pattern: Fail-Closed Setup Sequence
- **Pattern**: Setup order is strict: (1) stage test bin, (2) find-or-create profile, (3) grant ACLs, (4) only then launch. If any step 1-3 fails, the harness aborts before any process launch — it never launches a process under a profile it isn't certain has the correct, minimal access.
- **Rationale**: Matches NFR-2 (fail-safe defaults) from the top-level requirements — extends the same principle from the winsup/cygwin patch (Unit 1b) into the test tooling itself.

## Reliability Pattern: Report Always Written, Even on Partial Failure
- **Pattern**: The `SuiteReport` (both stdout summary and `last-report.json`) is written in a `finally`-equivalent path — even if a scenario throws an unexpected exception (not just an expected failure/timeout), the harness catches it, records that scenario as FAILED with the exception detail, and continues to the next scenario / final report rather than crashing silently.
- **Rationale**: This tool's entire value is producing a trustworthy report; an uncaught exception mid-run that skips reporting would be worse than a reported failure.
