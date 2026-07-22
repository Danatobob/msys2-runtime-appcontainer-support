# Logical Components — Unit 1b (AppContainer Patch)

## `appcontainer.cc` / `appcontainer.h` (new)
- **Purpose**: Houses all new AppContainer-specific logic — capability detection and namespace-path resolution — kept separate from the existing `mm/shared.cc`, `wincap.cc`, `advapi32.cc` per Application Design's decision to use dedicated new files.
- **Public surface**: `appcontainer_current_process_is_sandboxed()`, `appcontainer_resolve_shared_parent_dir()`.
- **Internal state**: the AppContainer capability cache (see `functional-design/domain-entities.md`).

## `mm/shared.cc` (modified)
- **Purpose**: Unchanged high-level responsibility (owns the shared-object namespace root handles); gains a new conditional branch in `get_shared_parent_dir()`/`get_session_parent_dir()` that delegates to `appcontainer.cc` when sandboxed.
- **Relationship**: Consumer of `appcontainer.cc`'s public surface — no back-dependency.

## `wincap.cc` (modified)
- **Purpose**: Gains one new static capability-table entry ("OS supports AppContainer APIs", Windows 8+ gate).
- **Relationship**: Consulted (read-only) by `appcontainer.cc` before attempting AppContainer-specific Windows API calls — a defensive check given the Windows 10/11-only target already guarantees this is true.

## `advapi32.cc` (modified)
- **Purpose**: Gains a `GetTokenInformation`-based wrapper for AppContainer token detection, following the file's existing pattern of wrapping `advapi32.dll` security/token APIs.
- **Relationship**: Consumed by `appcontainer.cc`'s capability-detection logic.

## `winsup/cygwin/Makefile.am` (modified)
- **Purpose**: Build-file-list entry for the new `appcontainer.cc`.
- **Relationship**: Build-system integration only — no runtime relationship.

## Downstream Consumers (unmodified, per `component-dependency.md`)
`kernel32.cc`, `pinfo.cc`, `flock.cc`, `fhandler/{fifo,mqueue,socket_unix}.cc` all consume the `HANDLE` returned by `get_shared_parent_dir()`/`get_session_parent_dir()` without any awareness of whether it came from the AppContainer or non-AppContainer path — confirmed unchanged, no modifications needed to any of these files.
