# Requirements Clarification Questions

Your brief (`aidlc-docs/brief.md`, `aidlc-docs/info.md`) already gives a clear, well-specified technical goal and phased approach. These questions fill in the remaining gaps needed to write `requirements.md` — mostly around scope boundaries, security constraints, and practical execution permissions.

Please answer each question by filling in the letter choice after the `[Answer]:` tag. If none of the options match, choose the last option (Other) and describe your preference.

## Question 1
What counts as "done" for this engagement, given the brief's 4-phase approach (1: vanilla build, 2: reproduce failure, 3: patch, 4: check for further walls)?

A) Complete all 4 phases, with findings reported at each phase
B) Phases 1-3 only (build, reproduce, patch) — Phase 4's deeper investigation is a stretch goal, not required for "done"
C) Just get the vanilla build working (Phase 1) for now — the patch work is a separate future task
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 2
Phase 1 requires installing a self-hosted MSYS2 build toolchain (per the CI recipe found in `.github/workflows/build.yaml`: `msys2-devel`, `base-devel`, `autotools`, `mingw-w64-cross-gcc`, etc.) on this machine. Can I install and run this autonomously?

A) Yes, install and run whatever's needed autonomously
B) Yes, but check with me before installing new packages/tools
C) No — treat this as a design/code task only; don't attempt to actually build or install anything
X) Other (please describe after [Answer]: tag below)

[Answer]: A, but make sure its all installed within the project's folder (isolated). I dont want clutter on my disk.

## Question 3
Phase 2 needs a standalone AppContainer test harness (a small program using `CreateAppContainerProfile` + `CreateProcess` with `SECURITY_CAPABILITIES`). What implementation language do you prefer?

A) C (matches winsup/cygwin's own style)
B) C++
C) C# (.NET, using P/Invoke)
D) No strong preference — choose what's most reliable
X) Other (please describe after [Answer]: tag below)

[Answer]: C

## Question 4
Reverse engineering found that `get_session_parent_dir()` (session-scoped `\Sessions\BNOLINKS\<n>\...`) has the *identical* vulnerable pattern as `get_shared_parent_dir()` (`\BaseNamedObjects\...`), and both sit on the same startup path. Should the fix cover both functions together?

A) Yes, patch both functions together
B) Only `get_shared_parent_dir()` for now — leave `get_session_parent_dir()` for a follow-up
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 5
Is preserving AppContainer security isolation a hard constraint on the fix (e.g., the fix must NOT fall back to placing shared objects somewhere accessible outside the AppContainer, or grant broader permissions than necessary just to make things work)?

A) Yes — this is a hard constraint; security isolation must be preserved or the fix is not acceptable
B) Best-effort — some pragmatic relaxation is acceptable if strictly necessary to unblock functionality
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 6
What Windows version(s) must the fix support?

A) Windows 10 and Windows 11 only
B) Windows 8.1+ (broadest AppContainer-capable range)
C) Whatever this msys2-runtime fork already targets as its minimum supported OS — no new floor
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 7
Must normal (non-sandboxed) startup behavior and performance remain completely unchanged by this fix?

A) Yes — zero behavior/performance change for the non-AppContainer path is required
B) Minor differences acceptable if justified (e.g., one extra token check at startup)
X) Other (please describe after [Answer]: tag below)

[Answer]: A or B. this project's bash is going to be used only within AppContainer. I already have a normal bash installed separately.

## Question 8
What deliverables do you expect from this engagement?

A) Source code patch/diff only, with a written report of findings per phase
B) Patch + actually built/tested binaries (msys-2.0.dll, bash.exe) as build artifacts
C) Patch + build artifacts + an upstream-contribution-ready commit (following msys2-runtime/Cygwin coding & commit-message conventions)
X) Other (please describe after [Answer]: tag below)

[Answer]: B

## Question 9 (Extension Opt-In: Security Baseline)
Should security extension rules be enforced for this project as blocking constraints during Construction?

A) Yes — enforce all SECURITY rules as blocking constraints (recommended given this is a security-isolation-relevant OS patch)
B) No — skip all SECURITY rules (suitable for PoCs, prototypes, and experimental projects)
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 10 (Extension Opt-In: Property-Based Testing)
Should property-based testing (PBT) rules be enforced for this project?

A) Yes — enforce all PBT rules as blocking constraints (recommended for projects with significant algorithmic complexity)
B) Partial — enforce PBT rules only for pure functions and serialization round-trips
C) No — skip all PBT rules (suitable given this is mostly imperative OS-API/side-effecting code, not pure business logic)
X) Other (please describe after [Answer]: tag below)

[Answer]: X. we are just patching a code to allow it to run within AppContainer. If it works after that as normal then its acceptable. Run a few basic bash usage tests to make sure all the common scenarios are not broken by the patch.
