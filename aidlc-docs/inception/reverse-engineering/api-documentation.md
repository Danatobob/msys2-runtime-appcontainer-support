# API Documentation

**Scope note**: `winsup/cygwin` exposes no REST/network APIs. This document instead covers the **internal native-API surface and internal function "API"** relevant to the shared-object-namespace startup path, per the scope in `aidlc-state.md`.

## REST APIs
Not applicable — this is a native Windows runtime DLL, not a network service.

## Internal APIs (native NT / Win32 surface consumed)

### NtCreateDirectoryObject (ntdll.dll, native)
- **Declared**: `local_includes/ntdll.h`, bound via `autoload.cc`
- **Called from**: `get_shared_parent_dir()` (`mm/shared.cc:37-60`) and `get_session_parent_dir()` (`mm/shared.cc:64-97`)
- **Purpose**: Creates (or opens, via `OBJ_OPENIF`) a directory object in the NT object-manager namespace at a literal absolute path, used as the parent for all subsequent named shared objects.
- **Parameters (as used)**: `DirectoryHandle` (out), `DesiredAccess = CYG_SHARED_DIR_ACCESS`, `ObjectAttributes` (built via `InitializeObjectAttributes` with `OBJ_OPENIF`, a security descriptor from `everyone_sd()`, and a literal Unicode path).
- **Return**: `NTSTATUS`; on failure (`!NT_SUCCESS`), the caller invokes `api_fatal()`, which aborts the process — **no fallback path exists today**. This is the exact call that fails with `STATUS_ACCESS_DENIED (0xC0000022)` under an AppContainer token.

```c
// winsup/cygwin/mm/shared.cc — get_shared_parent_dir() (paraphrased from investigation)
HANDLE get_shared_parent_dir () {
  if (!shared_parent_dir) {
    WCHAR bnoname[MAX_PATH];
    __small_swprintf (bnoname, L"\\BaseNamedObjects\\%s%s-%S",
                       cygwin_version.shared_id, ..., &cygheap->installation_key);
    RtlInitUnicodeString (&uname, bnoname);
    InitializeObjectAttributes (&attr, &uname, OBJ_OPENIF, NULL,
                                 everyone_sd (CYG_SHARED_DIR_ACCESS));
    status = NtCreateDirectoryObject (&shared_parent_dir, CYG_SHARED_DIR_ACCESS, &attr);
    if (!NT_SUCCESS (status))
      api_fatal ("NtCreateDirectoryObject(%S): %y", &uname, status);  // <- crash point
  }
  return shared_parent_dir;
}
```

### NtQueryInformationProcess(ProcessSessionInformation) (ntdll.dll, native)
- **Called from**: `get_session_parent_dir()` (`mm/shared.cc:64`)
- **Purpose**: Determines the current Terminal Services session ID, used to decide between the session-scoped `\Sessions\BNOLINKS\<n>\...` path and falling back to `get_shared_parent_dir()`'s global path.
- **Relevance**: `get_session_parent_dir()` has the **identical vulnerable pattern** (literal absolute path + direct `NtCreateDirectoryObject`) and is reachable on the very same startup path whenever session ID is non-zero — it must be patched in parallel with `get_shared_parent_dir()`, not instead of it.

### GetTokenInformation (advapi32.dll, Win32) — not currently used
- **Status**: Zero call sites found anywhere in `winsup/cygwin` today.
- **Relevance**: Needed to detect `TokenIsAppContainer` / read `TokenAppContainerSid` from the process token — a prerequisite for any fix that branches on "am I running under an AppContainer."

### GetAppContainerNamedObjectPath (kernelbase.dll via securityappcontainer.h) — not currently used
- **Status**: Zero references anywhere in the tree.
- **Relevance**: The documented API for resolving an AppContainer's own private, permitted object-namespace path — the suggested fix direction per `aidlc-docs/brief.md`.

## Data Models (key structs on the startup path)

### `shared_info` (`local_includes/shared_info.h`)
- **Fields (relevant)**: `spinlock version`, `cb` (struct size/version check), plus subsystem sub-objects triggered from `initialize()` (tty state, mount table, load-average tracking, pid-source seed).
- **Relationships**: Created once globally via `shared_info::create()`/`initialize()` (`mm/shared.cc:278-320`), parented under the handle from `get_shared_parent_dir()`/`get_session_parent_dir()`.
- **Validation**: None beyond struct version/size check on open.

### `user_info`
- **Fields (relevant)**: Per-installation/per-user counterpart of `shared_info`.
- **Relationships**: Created immediately after `shared_info` in `memory_init()` (`mm/shared.cc:327`, via `user_info::create(false)`); same namespace dependency chain, one level down from `shared_info`.

### `cygheap_t` (global `cygheap`)
- **Fields (relevant)**: `installation_key` (used to derive the literal object name in `get_shared_parent_dir()`), `installation_root`.
- **Relationships**: Must already be initialized before `get_shared_parent_dir()` runs, since the function reads `cygheap->installation_key` to build the object name.

### `shared_parent_dir` / `session_parent_dir` (file-scope statics, `mm/shared.cc:35,62`)
- **Fields**: `static HANDLE NO_COPY` — cached once per process.
- **Relationships**: Returned to every downstream named-object creator (`kernel32.cc`, `pinfo.cc`, `flock.cc`, `fhandler/fifo.cc`, `fhandler/mqueue.cc`, `fhandler/socket_unix.cc`) as the `RootDirectory` for their own `OBJECT_ATTRIBUTES`, meaning a fix here propagates to all of them without further changes.
