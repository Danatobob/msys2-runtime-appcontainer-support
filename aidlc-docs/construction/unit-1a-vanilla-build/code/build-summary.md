# Build Summary — Unit 1a (Vanilla Build Enablement)

**Status: Complete and verified.** FR-1 satisfied: an unmodified `winsup/cygwin` checkout builds successfully with an isolated, project-local toolchain, and the resulting DLL was confirmed to behave normally in an ordinary (unsandboxed) shell.

## Toolchain
- **Source**: `msys2-base-x86_64-latest.sfx.exe` from `msys2/msys2-installer` (tag `nightly-x86_64`), 53,088,864 bytes, SHA256 `03f02a1eaaf27fa0ac517a2812fba9168ac664e5b9519888fdc340a29c85d7b2`, uploaded 2026-07-17. Checksum verified against GitHub's asset-level digest before extraction (no separate `.sha256` file is published for this rolling nightly tag).
- **Location**: `.build-toolchain/msys64/` (project-local, gitignored, ~306MB base + installed packages).
- **Package set**: exact CI list installed via `pacman -S --needed` with default `SigLevel` verification untouched. Full manifest (165 packages) in `aidlc-docs/construction/unit-1a-vanilla-build/nfr-requirements/toolchain-versions.md`.
- **Key environment detail**: The isolated MSYS2 instance must be invoked with `MSYSTEM=MSYS` explicitly set (do not rely on inherited environment — this machine's ambient shell has `MSYSTEM=MINGW64`, which silently causes `winsup` to be skipped from the build entirely). Under `MSYSTEM=MSYS`, MSYS2's own `/etc/config.site` resolves `build_alias` to `x86_64-pc-cygwin` (via `MSYSTEM_CHOST`) — this is MSYS2's own canonical/correct value for the plain `MSYS` personality, not a bug. `host_makefile_frag` resolves to `./config/mh-cygwin`, and `winsup` is configured as a target module (`configure-target-winsup`/`all-target-winsup`).

## Build
- **Commands** (all run via `.build-toolchain/msys64/usr/bin/bash.exe -lc "..."` with `MSYSTEM=MSYS` set):
  1. `(cd winsup && ./autogen.sh)`
  2. `./configure --disable-dependency-tracking --with-msys2-runtime-commit=2345cc9e2343830ef452ef207796ccca0aab3ccd`
  3. `make -j4` (see note below on `-j12` failing)
  4. `make DESTDIR="$(pwd)/_dest" install`
- **`-j12` failed with `cc1plus: out of memory`** on several `.cc` files — aggregate memory pressure from 12 concurrent C++ compiles on this machine (~9.5GB free RAM at the time), not a source or toolchain defect. Retried with `-j4`: clean success, 207 compile units, zero errors.
- **Build output**: `x86_64-pc-cygwin/winsup/cygwin/new-msys-2.0.dll` (24,823,137 bytes, unstripped/debug build) + full static library set (`libc.a`, `libpthread.a`, `libmsys-2.0.a`, etc.).
- **Install output**: `_dest/usr/bin/msys-2.0.dll` (24,823,137 bytes).
- **No `bash.exe` produced**: `winsup/cygwin` only builds the runtime DLL. `bash` is a separate MSYS2 source package, not part of this repository — building it was out of scope for this unit. FR-1's "bash.exe if feasible" therefore resolves to **not feasible from `winsup/cygwin` alone**; if a `bash.exe` is needed for later units (e.g. Unit 2's harness target), the isolated toolchain's own pacman-installed `bash.exe` (`.build-toolchain/msys64/usr/bin/bash.exe`) can be paired with the freshly-built `msys-2.0.dll`, exactly as done for verification below.

## Normal-Behavior Verification (FR-1 requirement)
Backed up the isolated toolchain's own working `msys-2.0.dll`, temporarily replaced it with the freshly-built one, and ran the toolchain's existing `bash.exe` against it:

- `uname -a` → `MSYS_NT-10.0-19045 spacestation 3.6.9-2345cc9e-api-357.x86_64 2026-07-17 11:42 UTC x86_64 Msys` — the embedded build identifier `2345cc9e` matches repo HEAD exactly, positively confirming the freshly-built DLL (not the pre-existing one) was what actually executed.
- Verified: `echo`, `uname`, `ls /`, `pwd`, a subshell/fork (`(echo child-subshell-ok)`), file I/O (write/read/delete a temp file), environment variables, a pipe (`echo | tr`), and an exit-code check (`true; echo $?`) — all succeeded with expected output.
- **Restored** the toolchain's original `msys-2.0.dll` immediately after testing and re-verified `uname -a` reverted to the original version string (`3.6.9-01d6c708...`) — the isolated toolchain is left exactly as it was, intact and ready for Unit 1b to rebuild the patched source from.

## Artifact Paths (for later units)
- Isolated toolchain: `D:\trd\Programming\Git\msys2-runtime\.build-toolchain\msys64\`
- Vanilla build output (DLL): `D:\trd\Programming\Git\msys2-runtime\_dest\usr\bin\msys-2.0.dll`
- Raw build tree (for rebuilds): `D:\trd\Programming\Git\msys2-runtime\x86_64-pc-cygwin\winsup\cygwin\`
- Both `.build-toolchain/` and `_dest/` are gitignored; no source files under `winsup/` were modified.

## Recommended Follow-ups for Later Units
- **Unit 1b**: Rebuild via `make -j4` (not `-j12`) from this same toolchain after patching — the OOM issue will recur at higher parallelism regardless of source changes.
- **Unit 2 (test harness)**: When it needs a real `bash.exe` + `msys-2.0.dll` pair to launch under an AppContainer profile, use the isolated toolchain's own `bash.exe` (`.build-toolchain/msys64/usr/bin/bash.exe`) alongside whichever `msys-2.0.dll` is under test (vanilla for Phase 2 repro, patched for Phase 3/4 validation) — the same swap approach used for this unit's verification.
