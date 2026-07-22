# NFR Requirements — Unit 1b (AppContainer Patch)

**Note**: No clarifying questions needed (see question-scope memory) — remaining open items are implementation-mechanism decisions already bounded by `requirements.md`'s NFR-1/NFR-2/NFR-4/NFR-5 and the Security Baseline.

## Applicability Summary
- **Scalability, Performance, Availability, Usability**: N/A — this is a startup-path code change in a native DLL, not a running service.
- **Security**: Applicable — largely already specified at the top-level requirements stage; this section concretizes it for this specific unit.
- **Reliability**: Applicable — fail-closed behavior, consistent with existing code.
- **Maintainability**: Applicable — code style/build-integration consistency with the existing `winsup/cygwin` codebase.

## Security Requirements (concretized from NFR-1, NFR-2)
- **No new capabilities requested**: the AppContainer namespace resolution MUST NOT request any process capabilities beyond what the token already has — it only reads existing token information and resolves a path, per Functional Design's BR-5.
- **Fail closed, no silent fallback**: any failure in AppContainer detection or namespace resolution MUST result in `api_fatal()`, never a silent fallback to the global namespace (BR-4) — this is the single most important security property of this entire patch.
- **No behavior change to the security descriptor model**: the same `everyone_sd(CYG_SHARED_DIR_ACCESS)` DACL is reused (BR-5) — this unit does not introduce a new access-control scheme to reason about.
- **Maps to**: Security Baseline SECURITY-11 (secure design, misuse-case consideration — the misuse case here is "a bug in this patch that accidentally shares state across different AppContainer profiles or falls back to the global namespace," which BR-2/BR-4/BR-6 directly guard against), SECURITY-15 (fail-safe defaults).

## Reliability Requirements
- **Consistent failure mode**: an AppContainer-path failure must produce the same class of diagnostic (`api_fatal()` with a descriptive message) as today's global-path failure, so operators/debuggers see a consistent failure pattern regardless of which code path failed (BR-4).
- **Cache correctness**: the AppContainer capability check (BR-1) must be computed before it's relied upon and never re-computed mid-process — a process's token type is immutable for its lifetime, so this is a safe one-time cache, not a potential staleness bug.

## Maintainability Requirements
- **Code style consistency**: new code in `appcontainer.cc`/`.h` MUST follow the existing `winsup/cygwin` code style (minimal comments, matching brace/naming conventions observed in `mm/shared.cc` and `wincap.cc`) rather than introducing a different style for just the new files.
- **Build integration**: the new source file(s) must be added to the existing build file list (`winsup/cygwin/Makefile.am`) the same way every other `.cc` file is already listed — no separate/parallel build mechanism.
- **No dead code**: the existing (non-AppContainer) branch logic must remain fully exercised and unmodified in behavior — this patch adds a new branch, it does not restructure or duplicate existing logic (BR-3).

## Tech Stack Decision Summary
See `tech-stack-decisions.md`.
