# Requirements: msys2-runtime AppContainer Startup Support

## Intent Analysis Summary
- **User Request**: Get `git-for-windows/msys2-runtime` (`winsup/cygwin`, backing Git for Windows' `bash.exe`) to compile from source on Windows, then patch it so it can start under a Windows AppContainer (LowBox token) sandbox, where it currently aborts at process-startup time with `NtCreateDirectoryObject(...): 0xC0000022 STATUS_ACCESS_DENIED`.
- **Request Type**: Bug fix / new capability (enabling an unsupported runtime execution context), delivered via a 4-phase engineering approach (build → reproduce → patch → deeper-wall check).
- **Scope Estimate**: Single component (`winsup/cygwin`), localized to two functions on the process-startup path (`get_shared_parent_dir()`, `get_session_parent_dir()` in `mm/shared.cc`), plus new AppContainer-detection code and a standalone test harness.
- **Complexity Estimate**: Complex — kernel-native-API-level Windows security-context work, in an unfamiliar/self-hosted build toolchain, with an explicit "there may be more walls beyond this one" risk (per brief Phase 4) and hard security-isolation constraints.

## Functional Requirements

### FR-1: Vanilla build (Phase 1)
`winsup/cygwin` MUST compile from an unmodified checkout into a working `msys-2.0.dll`, and `bash.exe` if feasible, using the self-hosted MSYS2 toolchain documented in `.github/workflows/build.yaml` (see `aidlc-docs/inception/reverse-engineering/technology-stack.md`). The resulting vanilla build MUST be verified to behave normally in an ordinary (non-sandboxed) shell before any source is modified.

- **Constraint**: The MSYS2 bootstrap toolchain and all build tooling MUST be installed in an isolated location under the project's own folder (`D:\trd\Programming\Git\msys2-runtime\`) — not system-wide (e.g. not `C:\msys64`, not `Program Files`) — so nothing is left on the user's disk outside the project. A portable/self-contained MSYS2 install rooted in a project subdirectory is the intended approach.

### FR-2: Independent failure reproduction (Phase 2)
A standalone AppContainer test harness MUST be built, in **C#** (per user preference), using `CreateAppContainerProfile` + `CreateProcess` with `PROC_THREAD_ATTRIBUTE_SECURITY_CAPABILITIES`/`SECURITY_CAPABILITIES`, with no dependency on any other project. Running the vanilla `bash.exe`/`msys-2.0.dll` from FR-1 under this harness MUST reproduce the exact same `NtCreateDirectoryObject`/`0xC0000022` failure described in the brief, confirming the build is a faithful repro before patching begins.

### FR-3: Startup patch (Phase 3)
Both vulnerable functions identified in reverse engineering MUST be patched together (not just the one named in the original bug report):
- `get_shared_parent_dir()` (`winsup/cygwin/mm/shared.cc`) — global `\BaseNamedObjects\...` path
- `get_session_parent_dir()` (`winsup/cygwin/mm/shared.cc`) — session-scoped `\Sessions\BNOLINKS\<n>\...` path

When running under an AppContainer token, the runtime MUST detect that context (e.g. via `GetTokenInformation`/`TokenAppContainerSid`) and construct the shared-object parent-directory path using the AppContainer's own private, permitted namespace (e.g. via `GetAppContainerNamedObjectPath`) instead of the literal global/session path, so the process no longer aborts at startup. Multiple processes launched under the *same* AppContainer profile MUST still be able to find and share the resulting shared state (per the brief — this is not a single-process-only fix).

### FR-4: Further-wall investigation (Phase 4)
After FR-3, the patched build MUST be exercised under the AppContainer harness beyond a bare startup: at minimum, run a simple echo-only script, then progressively more complex bash usage (see NFR-6 for the specific smoke-test scenarios). Any further AppContainer-namespace-isolation failures encountered MUST be diagnosed and reported (file, function, failing API, literal path pattern) — this phase is a diagnose-and-report deliverable, not an open-ended fix-everything mandate.

## Non-Functional Requirements

### NFR-1: Security isolation is a hard constraint
The fix MUST NOT weaken AppContainer isolation guarantees to achieve functionality — no falling back to placing shared objects somewhere accessible outside the AppContainer's intended boundary, and no granting broader permissions than necessary. If a safe fix cannot be found for part of the startup path, that MUST be reported as a blocker rather than worked around with a security-reducing shortcut. *(Maps to Security Baseline SECURITY-11 "Defense in depth" / misuse-case design and SECURITY-15 "Fail closed" — see Security Compliance below.)*

### NFR-2: Fail-safe defaults
On any failure to resolve or create the AppContainer-scoped namespace path, the process MUST fail closed (halt with a clear diagnostic, as today's `api_fatal()` does) rather than silently falling back to a location that could leak shared state outside the AppContainer or otherwise degrade the security boundary. *(Maps to SECURITY-15.)*

### NFR-3: Non-sandboxed path tolerance
Normal (non-AppContainer) startup behavior does not need byte-for-byte fidelity. Minor, justified additions (e.g. one extra token-type check at startup) are acceptable. No dedicated zero-diff regression suite is required for the non-AppContainer path — the smoke tests in NFR-6 are sufficient coverage, since this build is intended for AppContainer-only use (the user maintains a separate normal Git-for-Windows bash install). *(Resolved via clarification — see `aidlc-docs/inception/requirements/requirements-clarification-questions.md`.)*

### NFR-4: Target platform
The fix MUST support Windows 10 and Windows 11 (per user answer) — no requirement to support older AppContainer-capable versions (e.g. Windows 8.1).

### NFR-5: Build/toolchain supply chain
Toolchain packages (MSYS2 bootstrap + `mingw-w64-cross-*`) MUST be installed only from official MSYS2 package repositories, isolated to the project folder per FR-1. *(Maps to SECURITY-10 "Trusted sources only" — see Security Compliance below.)*

### NFR-6: Smoke-test coverage (replaces Property-Based Testing, which is opted out)
Property-based testing is explicitly out of scope (user opted out — this is imperative, side-effecting OS-API code, not pure business logic well-suited to PBT). Instead, after the patch, a basic bash smoke-test pass MUST confirm common usage scenarios are not broken, run both under the AppContainer harness and normally. Minimum scenarios: launching an interactive-equivalent shell and running a simple command; running a shell script with variables/control flow; spawning a child process (fork/exec, e.g. a subshell or external command); basic file I/O in the MSYS filesystem; environment variable handling; pipes/redirection.

### NFR-7: Deliverables
The engagement MUST produce: (a) the source patch/diff, (b) a written report of findings for each of the 4 phases, and (c) actually built and tested binaries (`msys-2.0.dll`, and `bash.exe` if feasible) as build artifacts. Upstream-contribution-ready commit formatting is explicitly **not** required for this engagement.

## Security Compliance (Security Baseline extension — enabled per user opt-in)

Most Security Baseline rules target cloud/web applications and are **N/A** to a native Windows systems DLL with no data stores, network services, or user authentication:

| Rule | Status | Rationale |
|---|---|---|
| SECURITY-01, 02, 03, 04, 05, 06, 07, 08, 12, 14 | N/A | No data stores, network intermediaries, HTTP surface, API endpoints, IAM policies, network configs, or user authentication in this project |
| SECURITY-09 (Hardening/Misconfiguration) | Partially applicable | Interpreted as: don't introduce a hardcoded/default fallback path that bypasses the AppContainer boundary; see NFR-1/NFR-2 |
| SECURITY-10 (Supply Chain) | Applicable | See NFR-5 — toolchain from official sources only, isolated install location |
| SECURITY-11 (Secure Design) | Applicable | See NFR-1 — defense in depth, must consider misuse (e.g. a malicious AppContainer profile attempting to reach another profile's shared namespace) |
| SECURITY-13 (Integrity Verification) | Partially applicable | Downloaded toolchain packages should come through MSYS2's standard signed-package mechanism (pacman) rather than unverified downloads |
| SECURITY-15 (Fail-Safe Defaults) | Applicable | See NFR-2 — fail closed on any inability to safely resolve the AppContainer namespace path |

## Summary

This engagement builds `winsup/cygwin` from source in an isolated, project-local MSYS2 toolchain (FR-1), independently reproduces the documented AppContainer startup failure with a purpose-built C# test harness (FR-2), patches both vulnerable shared-namespace functions in `mm/shared.cc` to detect and use the AppContainer's own private object namespace without weakening isolation (FR-3, NFR-1/NFR-2), and then probes for further AppContainer-related startup walls beyond the initial fix (FR-4). Non-sandboxed-path fidelity is treated as low-priority (NFR-3) since this build is AppContainer-only in intended use; validation relies on a basic bash smoke-test pass rather than property-based testing (NFR-6). Deliverables are patch + report + built/tested binaries (NFR-7).
