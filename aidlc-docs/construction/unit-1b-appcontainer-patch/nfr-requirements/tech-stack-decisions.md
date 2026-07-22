# Tech Stack Decisions — Unit 1b (AppContainer Patch)

## `GetAppContainerNamedObjectPath` Binding Mechanism
- **Decision**: Statically link against it (declared via `<securityappcontainer.h>`, exported by `kernelbase.dll`/`kernel32.dll` with a standard import library) — **not** added to `autoload.cc`'s lazy-binding table.
- **Rationale**: `autoload.cc`'s lazy-binding pattern in this codebase is specifically reserved for `ntdll.dll` native APIs, which have no standard import library and must be resolved at runtime (per `code-structure.md`'s "Lazy-bound native API table" pattern). `GetAppContainerNamedObjectPath` is a regular Win32 API with a normal import library, available since Windows 8 — well within the Windows 10/11-only target (NFR-4). Static linking is simpler and matches how other non-ntdll Win32 APIs are already handled elsewhere in the codebase (e.g. `advapi32.cc`'s existing static links against `advapi32.dll`).

## `GetTokenInformation` Binding Mechanism
- **Decision**: Statically link against `advapi32.dll`, consistent with `advapi32.cc`'s existing pattern for every other token/SID API it wraps.
- **Rationale**: Same reasoning as above — this is a standard, long-available Win32 API, no lazy-binding needed.

## Build Integration
- **Decision**: Add `appcontainer.cc` to `winsup/cygwin/Makefile.am`'s existing source file list (same mechanism as every other `.cc` file), and ensure `appcontainer.h` is available via the existing `local_includes/`-style header search path used by the rest of the codebase.
- **Rationale**: No new build machinery needed — this is a straightforward addition to an existing, well-understood autotools-based build (per `aidlc-docs/inception/reverse-engineering/code-structure.md`).

## Code Style
- **Decision**: Match existing `winsup/cygwin` conventions exactly (brace style, naming, minimal inline comments) — no new style introduced for the new files.
- **Rationale**: This is a small, additive patch to a long-lived upstream-adjacent codebase; consistency reduces review friction and matches this engagement's earlier decision (NFR-7) that upstream-contribution-ready formatting isn't required, but plain consistency with the surrounding code still is (avoids the patch looking visually foreign).
