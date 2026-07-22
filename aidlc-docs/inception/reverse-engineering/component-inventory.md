# Component Inventory

## Application Packages (in scope for this task)
- `winsup/cygwin` — The Cygwin/MSYS POSIX-emulation runtime DLL (`msys-2.0.dll`); primary build and patch target.

## Excluded Packages (out of scope, part of the same monorepo)
- `winsup/cygserver` — Optional System-V IPC daemon; not on the process-startup path being patched.
- `winsup/utils` — Cygwin userland utilities (`ps`, `mount`, etc.); consumers of the runtime, not part of it.
- `winsup/doc` — DocBook documentation sources.
- `newlib` — Standalone C library for embedded/bare-metal cross-compilation targets; not linked into `msys-2.0.dll`.
- `libgloss` — Low-level board-support glue for embedded newlib targets; not linked into `msys-2.0.dll`.
- `texinfo` — Vendored documentation tool.
- `include`, `etc`, `config` (repo root) — Shared top-level build machinery, peripheral to this task.

## Shared/Infrastructure Packages
- None in the CDK/Terraform sense — this is a native Windows DLL project built via GNU autotools, not a cloud-deployed system.

## Test Packages
- `winsup/testsuite` — Existing Cygwin/MSYS test suite (DejaGNU-based); relevant for validating the patch doesn't regress normal (non-sandboxed) startup, though it has no AppContainer-specific coverage today.

## Total Count
- **Total Packages (monorepo top level)**: ~9 (winsup/{cygwin,cygserver,utils,doc,testsuite}, newlib, libgloss, texinfo, top-level build machinery)
- **In Scope for this task**: 1 (`winsup/cygwin`)
- **Test**: 1 (`winsup/testsuite`)
- **Excluded**: 7
