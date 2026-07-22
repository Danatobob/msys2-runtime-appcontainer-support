# Technology Stack

## Programming Languages
- C / C++ (mixed, `.cc` extension used throughout `winsup/cygwin`) — the entire runtime and its startup path.

## Frameworks
- None (no application framework) — this is a low-level OS-emulation runtime built directly against the Win32 and native NT APIs.

## Build Tools
- GNU Autotools (`autoconf`/`automake`, invoked via `winsup/autogen.sh` and the repo-root `configure`) — primary build system.
- GNU Make — build execution (`make -j8`, `make install`).
- `mingw-w64-cross-gcc` / `mingw-w64-cross-crt` / `mingw-w64-cross-zlib` — the actual cross-toolchain that compiles `msys-2.0.dll`, run **inside** a bootstrap MSYS2 shell (self-hosted build, not a plain Linux→Windows cross-compile).
- MSYS2 bootstrap packages (`msys2-devel`, `base-devel`, `autotools`) — provide the self-hosted shell/toolchain the build itself runs in.
- Perl, `cocom`, `gettext-devel`, `libiconv-devel`, `zlib-devel`, `libzstd-devel`, `xmlto`, `docbook-xsl` — auxiliary build/codegen/doc dependencies pulled in by the official CI recipe.

## Testing Tools
- DejaGNU-based test suite under `winsup/testsuite` (standard for the Cygwin/newlib toolchain family); no AppContainer-specific test harness exists yet.

## Native OS APIs (the actual "platform" this code targets)
- **ntdll.dll** (native NT API layer, lazily bound at runtime via `autoload.cc` — no static import library) — `NtCreateDirectoryObject`, `NtOpenDirectoryObject`, `NtQueryInformationProcess`, and related object-manager APIs.
- **advapi32.dll** — security/token APIs (wrapped in `advapi32.cc`); `GetTokenInformation` not currently used anywhere — a gap relevant to the planned fix.
- **kernel32.dll** — standard process/thread/file APIs used throughout `dcrt0.cc`/`spawn.cc`.
- **securityappcontainer.h** (`GetAppContainerNamedObjectPath`) — documented API for the suggested fix direction; not currently referenced anywhere in the codebase.

## CI / Known-Working Build Recipes (reference)
- `.github/workflows/build.yaml` — authoritative MSYS2-based build recipe (Windows runner, self-hosted MSYS shell). Primary reference for Phase 1 of the task.
- `.appveyor.yml` — legacy/alternate Cygwin (not MSYS2) build on a Visual Studio 2019 image.
- `.github/workflows/cygwin.yml` — legacy/alternate Cygwin build via a Fedora container + `mingw64-gcc-c++` + cross-cygwin copr packages.
