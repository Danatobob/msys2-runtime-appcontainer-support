# Functional Design Plan — Unit 2 (AppContainer Test Harness)

## Unit Context
- **Requirements covered**: FR-2, FR-4 (test execution side), NFR-6
- **Components** (from Application Design): Profile Manager, Sandboxed Process Launcher, Scenario Library, Scenario Runner/Reporter, CLI Entry Point
- **Key constraint learned from Unit 1a**: no `bash.exe` exists in this repo — the harness will pair the isolated toolchain's own `bash.exe` (`.build-toolchain/msys64/usr/bin/bash.exe`) with whichever `msys-2.0.dll` is under test, per Unit 1a's recommendation.

## Plan Checklist
- [x] Generate `aidlc-docs/construction/unit-2-test-harness/functional-design/business-logic-model.md`
- [x] Generate `aidlc-docs/construction/unit-2-test-harness/functional-design/business-rules.md`
- [x] Generate `aidlc-docs/construction/unit-2-test-harness/functional-design/domain-entities.md`

## Clarifying Questions

Please fill in each `[Answer]:` tag, then let me know when you're done.

### Question 1 — Expected-Outcome Model
The harness must expect **failure** (a specific known error) when testing the vanilla/unpatched build (Phase 2 repro), but **success** when testing the patched build (Phase 3/4 validation). How should this be modeled?

A) Each scenario declares two independent expected outcomes — one for "known-failing" target mode, one for "expected-working" target mode; the CLI takes a mode flag (e.g. `--expect failing|working`) telling the harness which to check against
B) Smoke-test scenarios only ever declare a "success" expectation; a separate, distinct one-off "repro" scenario is hardcoded specifically to check for the known `STATUS_ACCESS_DENIED`/`NtCreateDirectoryObject` failure signature, used only for Phase 2
C) Something else — describe
X) Other (please describe after [Answer]: tag below)

[Answer]: A (resolved by AI judgment — user indicated implementation-mechanism questions like this don't need their input)

### Question 2 — Pass/Fail Matching Mechanism
How should actual vs. expected outcome be compared for each scenario run?

A) Exit code only — simplest, but can't distinguish "crashed at startup" from "ran, but the script itself exited nonzero"
B) Exit code + stderr/stdout pattern matching (e.g. checking for `NtCreateDirectoryObject`/`0xC0000022`/`fatal error` substrings for the known-failure case; checking expected content for success cases)
C) Full stdout+stderr+exit-code exact-string comparison against a fixed expected value (strict, more brittle to incidental output changes)
X) Other (please describe after [Answer]: tag below)

[Answer]: B (resolved by AI judgment — best balance of high-confidence verification without brittleness)

### Question 3 — AppContainer Filesystem Access for the File-I/O Scenario
AppContainer processes are restricted from writing to arbitrary filesystem locations by default. NFR-6's file-I/O smoke scenario needs somewhere writable inside the sandbox. How should this be handled?

A) Grant the test AppContainer profile a specific writable scratch folder via an explicit ACL grant (`icacls` granting the AppContainer SID access) — closer to a realistic deployment scenario
B) Have the file-I/O scenario write only within the AppContainer's own default-accessible per-package folder (`%LOCALAPPDATA%\Packages\<profile>\...`) — no extra ACL setup needed
C) Skip the write step if ACL setup proves complex; verify read-only access to something already accessible instead
X) Other (please describe after [Answer]: tag below)

[Answer]: A, use E:\Temp for experiments

### Question 4 — Timeout / Hang Handling
What should happen if a launched process hangs (e.g. the bug manifests as a hang rather than a clean fatal error)?

A) Enforce a fixed timeout (e.g. 10-15 seconds) per scenario launch; on timeout, forcibly terminate the process and record the scenario as FAILED with a "timeout" reason
B) No timeout — rely on the process always exiting on its own
X) Other (please describe after [Answer]: tag below)

[Answer]: A, 15 seconds (resolved by AI judgment — standard practice for an automated harness)

### Question 5 — AppContainer Profile Lifecycle
Should the harness create a fresh AppContainer profile per scenario, or reuse one profile across an entire suite run?

A) One profile created once (if it doesn't already exist) and reused across all scenarios in a run — faster, and lets us actually test the "multiple processes sharing state under the same AppContainer profile" requirement from the brief
B) Fresh profile per scenario — cleaner isolation, but can't exercise the shared-state-across-processes requirement
X) Other (please describe after [Answer]: tag below)

[Answer]: A (resolved by AI judgment — needed to exercise the brief's multi-process shared-state requirement)
