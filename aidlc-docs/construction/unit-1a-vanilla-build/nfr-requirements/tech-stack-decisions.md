# Tech Stack Decisions — Unit 1a (Vanilla Build Enablement)

## Toolchain Acquisition
- **Decision**: Download the official MSYS2 portable "base" archive (`msys2-base-x86_64-<date>.tar.zst`/`.tar.xz` — exact filename/date resolved at build time from the current `msys2/msys2-installer` GitHub release) and extract it locally, rather than installing from an existing system MSYS2 (if any) or building a toolchain from scratch.
- **Rationale**: Matches the authoritative CI recipe (`.github/workflows/build.yaml`) most closely, is officially documented, and guarantees a clean, isolated, reproducible starting point independent of whatever may or may not already be on this machine.

## Install Location
- **Decision**: `.build-toolchain/` at the project root (`D:\trd\Programming\Git\msys2-runtime\.build-toolchain\`). The extracted portable archive creates its own `msys64/` subfolder, so the working toolchain root is `.build-toolchain\msys64\`.
- **Rationale**: Leading-dot naming visually separates it from tracked source, and it is added to `.gitignore` so it's never accidentally committed.

## Package Set
Per `aidlc-docs/inception/reverse-engineering/technology-stack.md` (from the authoritative CI recipe):
```
msys2-devel base-devel autotools cocom diffutils gcc gettext-devel
libiconv-devel make mingw-w64-cross-crt mingw-w64-cross-gcc
mingw-w64-cross-zlib perl zlib-devel xmlto docbook-xsl libzstd-devel
```
- **Decision**: Install this exact package set via `pacman -S` inside the extracted MSYS2 environment, sourced from MSYS2's default official repositories only.
- **Rationale**: This is the known-working set from the project's own CI; deviating risks introducing a build that doesn't match how upstream actually validates this repo.

## Package Verification
- **Decision**: Leave `pacman`'s default `SigLevel` (signature verification) untouched/enabled. Use `--noconfirm` only to skip interactive confirmation prompts, never to bypass signature checks.
- **Rationale**: Per NFR-5 and Security Baseline SECURITY-13 — trust in the toolchain supply chain must not be weakened for convenience.

## Version Recording
- **Decision**: After install, capture `pacman -Q` output (or equivalent) into `aidlc-docs/construction/unit-1a-vanilla-build/nfr-requirements/toolchain-versions.md` once the actual setup is performed (during Build and Test execution — this file is a placeholder decision now, populated with real data when the environment is actually built).
- **Rationale**: Enables future reproducibility despite the setup procedure itself being manual, not scripted.

## Build System
- **Decision**: No new build-system choice — this project already uses GNU Autotools (`winsup/autogen.sh` → repo-root `configure` → `make`), confirmed in `aidlc-docs/inception/reverse-engineering/code-structure.md`. Unit 1a uses it as-is; no wrapper or alternative build system is introduced.
