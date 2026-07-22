# Business Rules — Unit 2 (AppContainer Test Harness)

## BR-1: Only the "startup" scenario is meaningful in `--expect failing` mode
The known bug fails the process before it ever reaches user-script execution — so in `--expect failing` mode (used for Phase 2's repro against the vanilla build), every scenario would fail identically at startup for the same underlying reason, which isn't independently informative. Rule: the Scenario Library's `startup` scenario is the only one with a real "failing" expectation (the known `NtCreateDirectoryObject`/`0xC0000022` signature). If `--expect failing` is combined with full-suite mode, only the `startup` scenario is actually executed and reported as meaningful; other scenarios are skipped with a "not applicable in failing mode" note rather than silently passed or falsely failed. If `--expect failing --scenario <other>` is explicitly requested, it still just checks for the same startup-failure signature (since nothing gets further than that when the bug is present).

## BR-2: "Working" mode requires all scenarios to define real success criteria
In `--expect working` mode (Phase 3/4 validation against the patched build), every scenario in the Scenario Library MUST have a defined expected exit code (0) and expected stdout/stderr pattern(s) — a scenario with no working-mode expectation is a Scenario Library authoring defect, not a valid state.

## BR-3: Pass/fail matching is exit code + pattern-based, not exact string match
A scenario PASSES if: the process exits within the timeout, the exit code matches the expected value, AND all required output patterns are found in stdout/stderr (substring/regex match, not exact full-string equality). This gives high-confidence verification (distinguishes "crashed at startup" from "ran, but exited nonzero" from "ran and produced wrong output") without being brittle to incidental formatting differences in output.

## BR-4: Timeout is a hard failure, not a hang
Any scenario launch exceeding 15 seconds is forcibly terminated and recorded as FAILED with reason `timeout` — the harness itself must never hang waiting on a sandboxed process, since a hang is itself a plausible symptom of an unresolved AppContainer wall (per FR-4).

## BR-5: A run's overall result is the AND of all executed scenario results
`SuiteReport.OverallPassed` is true only if every *executed* scenario (see BR-1 for what's executed in failing mode) passed. A single scenario failure fails the whole run's overall status, even though execution continues through all scenarios for reporting completeness (per the business logic model's continue-on-failure rule).

## BR-6: The shared toolchain is never mutated by a run
No scenario execution, staging step, or cleanup may modify anything under `.build-toolchain/` — the fixed scratch bin/data folders under `E:\Temp\appcontainer-harness\` are the only locations written to. This preserves Unit 1a's toolchain as a stable, reusable rebuild base for Unit 1b.

## BR-7: AppContainer profile reuse is keyed by a fixed, deterministic name
The profile lookup is by a fixed name (not per-run-generated), so repeated invocations across a session (and across Phase 2/3/4 work) reuse the same profile/SID rather than accumulating orphaned AppContainer profiles on the machine. Explicit `--cleanup` is required to remove it.

**Update (per user decision)**: the profile is NOT removed automatically after individual runs — it persists through Phase 2/3/4. It MUST be removed as an explicit final step once the overall engagement goal is reached (i.e., as one of the last actions in the Build and Test stage, after all validation is complete), not left behind indefinitely. This is tracked as a required Build and Test step, not left to the user to remember.
