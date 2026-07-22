# Business Rules — Unit 1b (AppContainer Patch)

## BR-1: AppContainer detection is computed once and cached
`appcontainer_current_process_is_sandboxed()` performs the `GetTokenInformation` check exactly once per process (cached, mirroring the existing `shared_parent_dir` static-cache pattern) — never re-queried on subsequent `get_shared_parent_dir()`/`get_session_parent_dir()` calls.

## BR-2: Under AppContainer, both namespace-root functions resolve identically
When the cached AppContainer flag is true, `get_shared_parent_dir()` and `get_session_parent_dir()` both delegate to the same `appcontainer_resolve_shared_parent_dir()` and are expected to produce equivalent results — the session/global distinction that exists in the non-AppContainer path does not apply here, per the Business Logic Model's analysis of `GetAppContainerNamedObjectPath`'s inherent session+container scoping.

## BR-3: The non-AppContainer path is unchanged except for one new leading check
Every existing line of logic in the non-AppContainer branch of `get_shared_parent_dir()`/`get_session_parent_dir()` is preserved exactly as-is; the only new cost for normal (non-sandboxed) processes is the single cached capability check (per NFR-3 from `requirements.md`, minor differences are acceptable and no dedicated zero-diff regression suite is required).

## BR-4: No fallback that weakens isolation — fail closed identically to today
If `GetAppContainerNamedObjectPath` fails, or the subsequent `NtCreateDirectoryObject` on the AppContainer-scoped path fails, the process aborts via the existing `api_fatal()` mechanism — exactly the same failure mode as today's global-path failure. There is no code path that falls back to the global `\BaseNamedObjects\...` location when running under AppContainer (that would defeat the fix's entire purpose and violate NFR-1).

## BR-5: No new capabilities or broadened permissions are introduced
This patch changes *where* the shared-object namespace root lives, never *what* access is granted to it. The security descriptor (`everyone_sd(CYG_SHARED_DIR_ACCESS)`) and the object-naming suffix construction are reused unchanged from the existing code — the isolation boundary comes entirely from the namespace location itself, which Windows already restricts to processes holding the matching AppContainer token.

## BR-6: Object-naming suffix is reused unchanged for collision safety
The same `cygwin_version.shared_id` + version + `cygheap->installation_key` suffix construction used today is reused when building the AppContainer-scoped path, so multiple distinct Cygwin/MSYS installations running inside the same AppContainer profile still get distinct, non-colliding shared-object roots (matches the existing collision-avoidance property of the global path).

## BR-7: `get_session_parent_dir()`'s existing session-detection logic (`NtQueryInformationProcess`) is bypassed, not removed, under AppContainer
The existing session-ID query and the `\Sessions\BNOLINKS\<n>\...` literal-path construction remain in the non-AppContainer branch untouched (BR-3); under AppContainer, that logic is simply not reached — the function branches to the shared AppContainer resolver before ever executing the legacy session-detection code.
