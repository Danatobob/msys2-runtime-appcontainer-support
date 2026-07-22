# Domain Entities — Unit 1b (AppContainer Patch)

**Note**: This is native C/C++ runtime code, not an OOP domain model — "entities" here are the key data/state elements the patch introduces or touches.

## New State: AppContainer Capability Cache
- **Form**: `static NO_COPY bool appcontainer_checked; static NO_COPY bool appcontainer_is_sandboxed;` (or a single tri-state cache) in the new `appcontainer.cc`.
- **Purpose**: Backs `appcontainer_current_process_is_sandboxed()` — computed once, mirrors the existing `shared_parent_dir`/`session_parent_dir` static-cache pattern already in `mm/shared.cc`.
- **Lifetime**: Process lifetime (never invalidated — a process's token type cannot change after creation).

## Reused State: `shared_parent_dir` / `session_parent_dir` (existing, unchanged declaration)
- **Form**: `static HANDLE NO_COPY shared_parent_dir;` / `static HANDLE NO_COPY session_parent_dir;` — already exist in `mm/shared.cc` (per `code-structure.md`'s "Cached singleton handle" pattern).
- **Change**: No structural change. Under this patch, the HANDLE they end up caching may now point into the AppContainer's private namespace instead of the global one — the cache mechanism itself is untouched.

## New Function Surface (`appcontainer.cc` / `appcontainer.h`)
- `bool appcontainer_current_process_is_sandboxed()` — see Business Logic Model.
- `HANDLE appcontainer_resolve_shared_parent_dir()` — see Business Logic Model. Returns `NULL`/invalid handle on failure; caller treats this identically to today's `NtCreateDirectoryObject` failure path (BR-4).

## Touched Existing Entities
- `get_shared_parent_dir()`, `get_session_parent_dir()` (`mm/shared.cc`) — control-flow modified per the Business Logic Model; no signature change.
- `cygheap->installation_key` — read (not modified) by the new resolver, exactly as it's already read by the existing global-path logic.
- `wincap` table (`wincap.cc`) — gains an "OS supports AppContainer APIs" static capability flag (Windows 8+ gate; per Application Design's `components.md`), consulted before even attempting the AppContainer path (defensive — the Windows 10/11-only target in NFR-4 means this should always be true in practice, but the check costs nothing and matches the codebase's existing capability-table convention).
- `advapi32.cc` — gains a `GetTokenInformation`-based wrapper used by `appcontainer_current_process_is_sandboxed()`.
- `autoload.cc` / `local_includes/ntdll.h` — extended only if `GetAppContainerNamedObjectPath` needs lazy binding (open item, resolved during Code Generation based on whether it's statically linkable given the Windows 10/11-only target).
