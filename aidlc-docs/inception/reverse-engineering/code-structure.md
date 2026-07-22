# Code Structure

**Scope**: `winsup/cygwin` only (the `msys-2.0.dll` source tree). See `component-inventory.md` for why `newlib`/`libgloss`/`winsup/cygserver` are excluded.

## Build System
- **Type**: GNU autotools, two-level (repo root `configure.ac`/`Makefile.def` drives the monorepo; `winsup/configure.ac` + `winsup/autogen.sh` regenerate configure for the `winsup` subtree — cygwin, cygserver, utils, doc). There is no separate `winsup/cygwin/configure.ac`; cygwin builds as a subdirectory of the winsup configure.
- **Configuration**: Authoritative known-working recipe is `.github/workflows/build.yaml` (`build` job, `windows-latest`, MSYS2 self-hosted build):
  ```yaml
  install: msys2-devel base-devel autotools cocom diffutils gcc gettext-devel
            libiconv-devel make mingw-w64-cross-crt mingw-w64-cross-gcc
            mingw-w64-cross-zlib perl zlib-devel xmlto docbook-xsl libzstd-devel
  run: |
    (cd winsup && ./autogen.sh)
    ./configure --disable-dependency-tracking --with-msys2-runtime-commit="$GITHUB_SHA"
    make -j8
    make DESTDIR="$(pwd)"/_dest install
  ```
  This confirms the build is **self-hosted inside an MSYS shell on Windows** (not a cross-compile invoked from Linux), using a bootstrap MSYS2 install (`msys2-devel`/`base-devel`/`autotools`) plus a `mingw-w64-cross-*` toolchain to produce the target `msys-2.0.dll`. Alternate/legacy paths exist (`.appveyor.yml`: Cygwin+VS2019 image; `.github/workflows/cygwin.yml`: Fedora container + `mingw64-gcc-c++` + cross-cygwin copr packages) targeting upstream Cygwin rather than MSYS2 — same `autogen.sh` → `configure` → `make` → `make install` shape, lower priority as a reference.

## Key Modules / Directory Hierarchy
```
winsup/cygwin/
├── *.cc                  (~90 files, top-level runtime: dcrt0, dll_init, fork,
│                           spawn, exec, sigproc, exceptions, syscalls, path, ...)
├── mm/                    shared.cc, cygheap.cc, heap.cc, malloc.cc, mmap.cc
├── sec/                   acl.cc, auth.cc, base.cc, helper.cc, posixacl.cc
├── fhandler/              base.cc, disk_file.cc, pipe.cc, fifo.cc, mqueue.cc,
│                          socket_unix.cc, proc.cc, procsys.cc, pty.cc, console.cc, ...
└── local_includes/        ~79 headers (ntdll.h, shared_info.h, cygheap.h, ...)
```

### Existing Files Inventory (grouped by subsystem, primary candidates for this task in **bold**)

**Process/startup**
- `dcrt0.cc` — CRT startup glue: `dll_crt0_1` (first-time init), `child_info_fork::handle_fork()`, `child_info_spawn::handle_spawn()` (re-enter shared-memory init on every fork/exec)
- `dll_init.cc` — DLL attach/detach chain (`dll_dllcrt0`)
- `init.cc` — low-level asm/C init glue
- `cygtls.cc` — thread-local storage init
- `forkable.cc` — fork-readiness bookkeeping

**Shared memory / MM (`mm/`)**
- **`mm/shared.cc`** — global/session shared-object namespace root: `get_shared_parent_dir()`, `get_session_parent_dir()`, `shared_info::create()`/`initialize()` — **the bug site**
- `mm/cygheap.cc` — per-process heap/state (`cygheap_t`)
- `mm/heap.cc`, `mm/malloc.cc`, `mm/malloc_wrapper.cc` — heap allocators
- `mm/mmap.cc`, `mm/mmap_alloc.cc` — `mmap()` emulation

**Fork/exec**
- `fork.cc`, `spawn.cc`, `exec.cc`, `winf.cc` (Win32 process-creation helper)

**Signals**
- `exceptions.cc` (SEH handler install, signal delivery), `sigproc.cc` (signal/process-table core), `strsig.cc`

**Process info/tables**
- `pinfo.cc` — per-process shared info; 4 call sites depend on `get_shared_parent_dir()`
- `dtable.cc` — fd table

**Filesystem/fhandler (`fhandler/`)**
- `base.cc`, `disk_file.cc`, `pipe.cc`, `fifo.cc`, `mqueue.cc`, `socket_unix.cc`, `proc.cc`, `procsys.cc`, `pty.cc`, `console.cc`, `dev_disk.cc`, etc.

**Security/registry (`sec/` + top level)**
- `sec/acl.cc`, `sec/auth.cc`, `sec/base.cc` (SD helpers incl. `everyone_sd()`), `sec/helper.cc`, `sec/posixacl.cc`, `registry.cc`

**Sync primitives**
- `sync.cc`, `cygwait.cc`, `flock.cc` (creates a per-inode directory object relative to the shared parent dir)

**Win32-API emulation**
- `kernel32.cc` — Cygwin's own `CreateEvent`/`CreateMutex`/`CreateSemaphore`/`CreateFileMapping` wrappers; all take `get_shared_parent_dir()`'s handle as parent when named

**Platform capability**
- **`wincap.cc`** — OS-version/feature-capability table; natural extension point to add an "is AppContainer" flag

**Misc/syscalls**
- `syscalls.cc` (POSIX syscall shims), `path.cc`, `environ.cc`, `autoload.cc` (lazy ntdll/kernel32 import table), `advapi32.cc` (token/SID API wrappers)

## Design Patterns

### Lazy-bound native API table (autoload)
- **Location**: `autoload.cc`, `local_includes/ntdll.h`
- **Purpose**: `ntdll.dll` has no static import library; native NT APIs (`NtCreateDirectoryObject`, `NtOpenDirectoryObject`, `NtQueryInformationProcess`, etc.) are declared and resolved at runtime via autoload macros rather than link-time imports.
- **Implementation**: Macro-generated thunks bind on first call; a new API (e.g. an AppContainer named-object-path helper) would follow the same pattern.

### Cached singleton handle
- **Location**: `mm/shared.cc:35,62` — `static HANDLE NO_COPY shared_parent_dir` / `session_parent_dir`
- **Purpose**: Root namespace handle computed once per process and reused for the process's lifetime.
- **Implementation**: First call performs the NT API work and caches; a fix only needs to change first-call behavior, not add per-call overhead.

### Directory-relative object creation
- **Location**: All downstream named-object creators (`kernel32.cc`, `pinfo.cc`, `flock.cc`, `fhandler/fifo.cc`, `fhandler/mqueue.cc`, `fhandler/socket_unix.cc`)
- **Purpose**: Objects are named *relative* to the cached parent-directory handle (`RootDirectory` in `OBJECT_ATTRIBUTES`) rather than each constructing an absolute path.
- **Implementation**: Confirms the object-namespace problem is structurally contained to `mm/shared.cc`'s two root-directory functions — fixing those two functions should transparently fix all downstream named-object creation too.

## Critical Dependencies

### ntdll.dll (native NT API layer)
- **Usage**: `NtCreateDirectoryObject`, `NtOpenDirectoryObject`, `NtQueryInformationProcess`, and related object-manager APIs, declared in `local_includes/ntdll.h` and lazily bound via `autoload.cc`.
- **Purpose**: Direct native-layer access bypassing Win32 APIs that would otherwise auto-redirect into an AppContainer's private namespace.

### advapi32.dll (security/token APIs)
- **Usage**: Wrapped in `advapi32.cc`; currently no call to `GetTokenInformation` anywhere in the tree.
- **Purpose**: Would be the mechanism for detecting an AppContainer token (`TokenIsAppContainer` / `TokenAppContainerSid`) and, if the fix uses it, resolving the AppContainer's private namespace path (`GetAppContainerNamedObjectPath`, `securityappcontainer.h`).
