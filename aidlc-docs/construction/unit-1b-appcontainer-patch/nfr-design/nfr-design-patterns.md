# NFR Design Patterns — Unit 1b (AppContainer Patch)

**Note**: No clarifying questions needed (see question-scope memory) — patterns below resolved directly.

## Security Pattern: Namespace-Location-as-Boundary
- **Pattern**: The AppContainer-scoped shared object namespace relies on its *location* (inside the AppContainer's own private, Windows-enforced namespace segment) as its isolation boundary, not a custom DACL — the DACL is deliberately left identical to the non-AppContainer case (`everyone_sd()`).
- **Rationale**: Windows itself already prevents any process without the matching AppContainer token from even reaching that namespace segment; layering a second, differently-behaved access-control scheme on top would add complexity without adding real security, and risks introducing a bug in the *new* ACL logic that the existing, well-tested `everyone_sd()` path doesn't have.

## Security Pattern: Capability-Gated Path Selection, No Re-Derivation
- **Pattern**: The branch between AppContainer and non-AppContainer logic is driven exclusively by the cached `appcontainer_current_process_is_sandboxed()` result — computed once, never re-derived or second-guessed later in either code path.
- **Rationale**: Prevents a class of bug where inconsistent/repeated token queries could theoretically observe different results at different points in a process's life (even though a token's AppContainer status cannot actually change, defensive single-computation is cheap and eliminates the question entirely).

## Resilience Pattern: Fail-Closed AppContainer Resolution
- **Pattern**: `appcontainer_resolve_shared_parent_dir()` has exactly one success path and one failure path (propagate to `api_fatal()`) — there is no partial-success state, no retry, no fallback to a different (less isolated) namespace.
- **Rationale**: Directly implements BR-4/NFR-2 — for a security-boundary-establishing function, "fail loudly and stop" is strictly safer than any form of graceful degradation that could silently place shared state somewhere less isolated.

## Maintainability Pattern: Additive-Only Change
- **Pattern**: Every change in this unit is additive (new file, new branch in two existing functions, new capability-table entry) — no existing function is restructured, renamed, or has its non-AppContainer behavior altered beyond the one new leading check.
- **Rationale**: Minimizes review surface and regression risk for a patch touching process-startup-critical code; makes it trivial to `git diff` and see exactly what changed (supports NFR-7's rollback-via-git-revert expectation from `execution-plan.md`).
