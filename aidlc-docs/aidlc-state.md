# AI-DLC State Tracking

## Project Information
- **Project Type**: Brownfield
- **Start Date**: 2026-07-17T00:00:00Z
- **Current Stage**: INCEPTION - Workspace Detection

## Workspace State
- **Existing Code**: Yes
- **Reverse Engineering Needed**: Yes (scoped - see note below)
- **Workspace Root**: D:\trd\Programming\Git\msys2-runtime

## Scope Note
This workspace is the upstream `git-for-windows/msys2-runtime` fork of the newlib/Cygwin
monorepo (winsup/cygwin, winsup/cygserver, newlib, libgloss, texinfo, etc.). The user's
existing brief (`aidlc-docs/brief.md`, `aidlc-docs/info.md`) scopes the actual task narrowly:
get `winsup/cygwin` (the MSYS runtime backing Git for Windows' bash.exe) building from
source, then patch process/shared-memory startup (`get_shared_parent_dir()` in
`winsup/cygwin/shared.cc` and related startup code) so it can run under a Windows
AppContainer (LowBox token) sandbox. Reverse Engineering will therefore focus on
`winsup/cygwin` process-startup and shared-memory subsystems rather than documenting the
entire monorepo (newlib/libgloss are C library / embedded-target support code unrelated to
this task).

## Code Location Rules
- **Application Code**: Workspace root (NEVER in aidlc-docs/)
- **Documentation**: aidlc-docs/ only
- **Structure patterns**: See code-generation.md Critical Rules

## Reverse Engineering Status
- [x] Reverse Engineering - Completed on 2026-07-17T00:00:00Z
- **Artifacts Location**: aidlc-docs/inception/reverse-engineering/
- **Scope**: winsup/cygwin process-startup and shared-memory subsystem (see Scope Note above)

## Stage Progress
## Extension Configuration
| Extension | Enabled | Decided At |
|---|---|---|
| Security Baseline | Yes | Requirements Analysis |
| Property-Based Testing | No | Requirements Analysis |

## Execution Plan Summary
- **Total Stages to Execute**: 8 (Application Design, Units Generation, then per-unit: Functional Design, NFR Requirements, NFR Design, Code Generation, Build and Test)
- **Stages to Skip**: User Stories (no distinct personas/workflows), Infrastructure Design (no cloud/deployment infra)
- **Units planned**: 2 - (1) winsup/cygwin build+patch, (2) AppContainer C# test harness
- **Plan document**: aidlc-docs/inception/plans/execution-plan.md

### INCEPTION PHASE
- [x] Workspace Detection
- [x] Reverse Engineering
- [x] Requirements Analysis
- [x] User Stories (SKIPPED)
- [x] Workflow Planning
- [x] Application Design
- [x] Units Generation

### 🟢 CONSTRUCTION PHASE
**Unit processing order** (per unit-of-work-dependency.md critical path): Unit 1a -> Unit 2 -> Unit 1b

#### Unit 1a: winsup/cygwin - Vanilla Build Enablement
- [x] Functional Design - SKIP (no new business logic/data models - build/environment task only)
- [x] NFR Requirements
- [x] NFR Design
- [x] Infrastructure Design - SKIP (no cloud/deployment infra)
- [x] Code Generation - COMPLETE. Vanilla build succeeded: `_dest/usr/bin/msys-2.0.dll` (24,823,137 bytes), toolchain at `.build-toolchain/msys64/`, smoke-verified via uname/echo/subshell-fork/file-I-O/env-vars/pipe/exit-code. No `bash.exe` producible from this repo alone (separate MSYS2 package) - use toolchain's own `bash.exe` paired with the built DLL for later units. Build must use `make -j4` (not -j12, OOMs on this machine).

#### Unit 2: AppContainer Test Harness
- [x] Functional Design
- [x] NFR Requirements
- [x] NFR Design
- [x] Infrastructure Design - SKIP
- [x] Code Generation - COMPLETE. Harness built at tools/appcontainer-harness/ (.NET 10, CsWin32). **FR-2 ACHIEVED**: independently reproduced the documented bug against Unit 1a's vanilla msys-2.0.dll - exact match: `NtCreateDirectoryObject(\BaseNamedObjects\msys-2.0S5-611d995b890bea2a): 0xC0000022`. Real AppContainer profile `AIDLC.AppContainerHarness` now exists on this machine (persists until final --cleanup per user decision). Usage: `appcontainer-harness.exe --target <dll> --expect failing|working [--scenario <name>] [--verbose] [--cleanup]`.

#### Unit 1b: winsup/cygwin - AppContainer Patch
- [x] Functional Design
- [x] NFR Requirements
- [x] NFR Design
- [x] Infrastructure Design - SKIP
- [x] Code Generation - PARTIAL SUCCESS, ROUND 2 UPDATE. Patch implemented and builds cleanly. Normal/non-sandboxed path verified working (NFR-3 passed) across both rounds. **Round 2** (triggered by user challenging the round-1 "kernel wall" conclusion): confirmed 2 REAL bugs in our own code, not a kernel restriction - (1) path-walk requested too-broad access mask on shared/intermediate components, fixed to minimal DIRECTORY_TRAVERSE; (2) GetAppContainerNamedObjectPath's returned path is missing the \Sessions\<n>\ prefix that raw Nt* calls need explicitly (Win32 APIs get it transparently via object-manager symlinks) - fixed by querying session ID same as existing get_session_parent_dir() does. Sandboxed failure moved one level earlier (was \AppContainerNamedObjects, now \Sessions itself) - concrete forward progress. Still not fully resolved: STATUS_ACCESS_DENIED on \Sessions traversal for this specific token composition; TokenGroups dump never shows AppContainer-related SIDs in any tested config, suggesting TokenCapabilities (not yet examined) may be the actual mechanism. Original "needs UWP packaging" claim neither confirmed nor refuted. **ROUND 5 MILESTONE: THE ORIGINALLY-REPORTED BUG IS FIXED.** NtCreateDirectoryObject STATUS_ACCESS_DENIED (0xC0000022) on the shared-object namespace no longer occurs - confirmed via the real harness across all 7 scenarios. Root cause: separate per-path-component NtOpenDirectoryObject calls were each independently access-checked and \Sessions's own DACL grants nothing to AppContainer SIDs; fixed via a single NtCreateDirectoryObject(OBJ_OPENIF) call on the complete path string (matches how Win32's CreateEventW resolves internally - confirmed via a positive-control test in Round 4). Normal path re-verified working, no regression.

**ROUND 6: SIGNAL PIPE WALL FIXED.** Two bugs: bare pipe name rejected for LowBox token (fixed via Local\ prefix), and the re-opened pipe's DACL only trusted the real user SID (fixed by adding the AppContainer SID via appcontainer_get_current_sid()). Confirmed via harness: no ACCESS_DENIED anywhere in the signal-pipe path across all 7 scenarios. Normal path re-verified working.

**ROUND 7: /tmp warning is a RED HERRING.** Both fix hypotheses (env var override, creating a real /tmp dir) disproven. Decisive test: identical staged bash.exe run UNSANDBOXED produces the exact same /tmp warning but exits 0 normally - proves the warning is cosmetic and not what's killing the sandboxed run. Real cause of exit 2816 under the sandbox is still unidentified - produces no additional captured output.

**ROUND 8: Decoded exit 2816 = genuine SIGSEGV crash** (do_exit(11) from exceptions.cc's signal_exit(), confirmed via exception record: exc_code=0xC0000005 STATUS_ACCESS_VIOLATION). No fix applied this round, diagnosis only.

**ROUND 9 MAJOR MILESTONE: ORIGINAL BUG CONFIRMED FIXED.** Crash symbolized correctly (fixed truncation + ASLR module-base accounting) to hash_path_name() in path.cc:4033 - a PRE-EXISTING Cygwin robustness gap (unchecked NULL on a UNICODE_STRING* that can legitimately be NULL when NT-native-path conversion fails), NOT a bug in this unit's own new code - the sandbox just exposed a latent issue, didn't create it. One-line fix (NULL check mirroring the function's own early-return pattern). All Round-8 temp diagnostics cleanly removed (confirmed via git diff). **Harness validation: startup scenario - the exact original bug this whole engagement exists to fix - NOW PASSES.** 4/7 scenarios pass (startup, echo, control-flow-script, env-vars). 3 new failures further into execution: fork-subshell (2nd different named pipe, likely same fix pattern as Round 6), file-io (staged path not found from inside sandbox), pipes (15s timeout). Normal path re-verified working.

**ROUND 10: prefork() pipe fixed (confirmed)**, exact mirror of Round 6's pattern. Still 4/7 passing, but the 3 remaining scenarios now share ONE unified root cause instead of 3 separate problems: STATUS_DLL_INIT_FAILED on forked/spawned child processes specifically (confirmed via fork-subshell, which uses no external tool at all and still fails identically - points at the forked child's own re-initialization under the sandbox, not a missing-file issue). Separately confirmed: external tools (cat/tr/rm) aren't staged into the granted bin folder - a real, separate, still-needed fix for file-io/pipes regardless. Normal path re-verified working (real subshell fork succeeds unsandboxed). Full detail in patch-summary.md (Rounds 1-10). Presented to user: down to one well-understood remaining wall + one known staging gap, not 3 mysteries. Decision point on continuing.

**ROUND 11: STATUS_DLL_INIT_FAILED root-caused and FIXED - genuine pre-existing Cygwin bug** (wincapc's `caps` field was a raw pointer in genuinely cross-process SHARED memory, set once-ever by whichever process touches it first; under ASLR each process loads the DLL at a different address, so a forked child inherits the parent's dangling shared pointer and crashes - not an AppContainer-specific bug, fork just exposed it since every prior scenario was single-process). Fixed via a self-contained 2-file change (wincap.h/wincap.cc, raw pointer -> per-process-safe integer index into an ordinary non-shared lookup table). Confirmed: DLL_INIT_FAILED no longer occurs anywhere. Still 4/7 passing, zero regression. NEW crash found: fork-subshell/file-io now fail with a different STATUS_ACCESS_VIOLATION in clk_monotonic_t::now() (clock.cc:137) - structurally similar (also inside a wincap.has_*() check), strong candidate for the SAME CLASS of bug elsewhere, not yet fixed. pipes back to plain timeout. Normal path re-verified working. Hygiene: stray scratch files outside E:\Temp found and removed per user's flag. Full detail in patch-summary.md (Rounds 1-11).

**ROUND 12: clk_monotonic_t crash gone (moot), real deeper blocker found.** Audit confirmed no other ASLR-shared-pointer bugs exist (only 2 shared declarations total, other one is a non-pointer LONG, safe). REAL FINDING: forked children get STATUS_ACCESS_DENIED on NtOpenProcessToken(NtCurrentProcess()...) - the very first call in our sandboxed-detection function - so they fail-open as "not sandboxed" and hit the ORIGINAL pre-engagement crash. Fix attempt (DACL-SID pattern from Rounds 6/10) tested and DISPROVEN, cleanly reverted - honest negative result. Still 4/7 passing, zero regression. LIKELY REAL ROOT CAUSE (undconfirmed): Cygwin's own fork() creates children via a PLAIN, non-AppContainer-aware CreateProcessW call - fork.cc may not be propagating the LowBox security context to child processes at all. This would be a more architecturally significant fix than anything done so far (prior fixes were narrow: pipe names, DACL entries, path resolution, one pointer). Full detail in patch-summary.md (Rounds 1-12).

**ROUND 13: 2 fixes confirmed.** (1) Self-token-query denial fixed via GetCurrentProcessToken() pseudo-handle instead of explicit NtOpenProcessToken(NtCurrentProcess()...) - documented Windows mechanism for exactly this case. (2) PROCESS RISK DISCOVERED AND RECOVERED: Round 10's validated prefork() pipe fix had been silently destroyed by Round 12's own `git checkout -- fork.cc` (reverting its own failed experiment blanket-reverted the whole uncommitted file, discarding an unrelated legitimate fix living in the same file). Reapplied, confirmed. PROCESS LESSON: nothing in this engagement is committed to git, so blanket file-level reverts are dangerous - future recovery should target only the bad hunk. Still 4/7 passing, zero regression. NEW WALL: forked child dies at CreateFileMapping shared.5 in mm/shared.cc - DACL used is NULL/most-permissive so not a DACL issue this time; leading untested hypothesis is Mandatory Integrity Control (pre-existing mapping likely Medium-labeled, blocking the Low-integrity LowBox child) matching Round 3/4's precedent for a different object. Not yet fixed - needs empirical verification first per this engagement's established discipline. Normal path re-verified working. Full detail in patch-summary.md (Rounds 1-13). Given the git-checkout near-miss, raising whether to start committing validated fixes as checkpoints.

- [ ] Build and Test - EXECUTE (ALWAYS, after all units complete)
  - **MUST INCLUDE as a final step**: run the AppContainer test harness with `--cleanup` to remove the `AIDLC.AppContainerHarness` profile once the overall engagement goal is reached (per user decision - not deleted after individual runs, only at the very end).

## Units Defined
- **Unit 1a**: winsup/cygwin - Vanilla Build Enablement (FR-1, NFR-4, NFR-5) - no source changes
- **Unit 1b**: winsup/cygwin - AppContainer Patch (FR-3, FR-4, NFR-1/2/3/4) - depends on Unit 1a
- **Unit 2**: AppContainer Test Harness, C#/.NET 10, tools/appcontainer-harness/ (FR-2, FR-4, NFR-6) - no compile-time dependency on 1a/1b
- [ ] User Stories
- [ ] Workflow Planning
- [ ] Application Design
- [ ] Units Generation
