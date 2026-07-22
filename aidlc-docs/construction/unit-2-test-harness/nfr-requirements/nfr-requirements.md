# NFR Requirements — Unit 2 (AppContainer Test Harness)

**Note**: No clarifying questions were needed for this stage — the remaining open items (privilege requirements, report format/location, dependency approach, error-handling strategy) are implementation-mechanism decisions, resolved directly by engineering judgment per user feedback (see memory: question-scope).

## Applicability Summary
- **Scalability**: N/A — a local CLI tool run interactively/on-demand, not a service under load.
- **Performance**: N/A beyond the 15s per-scenario timeout already established in Functional Design (BR-4); no throughput targets.
- **Availability**: N/A — no uptime concept.
- **Usability**: Lightweight — CLI output must be readable by a human (the requesting user) reviewing pass/fail results directly in a terminal.
- **Security**: Applicable — see below.
- **Reliability**: Applicable — see below.
- **Maintainability**: Applicable — see below.

## Security Requirements
- **No elevation required**: `CreateAppContainerProfile`/`DeleteAppContainerProfile` and ACL grants via `icacls` on a user-owned scratch folder do not require administrator privileges under normal Windows configuration; the harness MUST NOT require running as admin.
- **Least-privilege AppContainer profile**: per Functional Design, no `SECURITY_CAPABILITIES` are requested — the profile gets exactly the scratch-folder ACL access it needs and nothing more.
- **Fail-safe on setup failure**: if profile creation or ACL granting fails partway, the harness MUST NOT proceed to launch a process under a partially-configured profile — it fails closed with a clear error, consistent with NFR-2/NFR-1 from `requirements.md`.
- **No credential/secret handling**: this tool touches no credentials, tokens (beyond the AppContainer token itself, which is a security *boundary* the tool constructs, not a secret it stores), or network calls.
- **Maps to**: Security Baseline SECURITY-11 (secure design — capability-based least privilege), SECURITY-15 (fail-safe defaults).

## Reliability Requirements
- **Target validation**: before launching anything, the harness MUST verify the `--target` DLL path exists and is a readable file; a missing/invalid target is a clear, immediate error — not a confusing downstream launch failure.
- **Timeout enforcement**: already established in Functional Design (BR-4) — 15s hard timeout per scenario launch, process forcibly terminated on exceed.
- **Deterministic reporting**: a `SuiteReport` is always produced for whatever scenarios actually ran, even if some individual launches failed or timed out (per BR-5's continue-on-failure rule) — the harness itself should not crash/exit uncleanly on an individual scenario failure.

## Maintainability Requirements
- **Report output**: the harness prints a human-readable pass/fail summary to stdout on every run, AND writes the same `SuiteReport` as JSON to a fixed path (`E:\Temp\appcontainer-harness\last-report.json`, overwritten each run — consistent with the "no disk clutter" preference already established for Unit 1a) so a specific run's results can be inspected or re-parsed afterward without re-running.
- **Findings documentation**: the actual per-phase findings reports required by NFR-7 are written to `aidlc-docs/` (documentation, not application code) during the Build and Test stage when Phases 2-4 are actually executed, based on the harness's JSON output — the harness itself never writes into `aidlc-docs/` directly (per the Code Location Rules in `aidlc-state.md`).
- **Logging verbosity**: default output is the summary report; a `--verbose` flag additionally echoes each scenario's full captured stdout/stderr, useful when diagnosing an unexpected failure.

## Tech Stack Decision Summary
See `tech-stack-decisions.md`.
