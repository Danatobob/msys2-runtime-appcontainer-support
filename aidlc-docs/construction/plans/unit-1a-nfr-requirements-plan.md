# NFR Requirements Plan — Unit 1a (Vanilla Build Enablement)

## Note on Prerequisites
Functional Design was intentionally **skipped** for this unit (see `aidlc-docs/aidlc-state.md`) — Unit 1a has no new business logic or data models, only a build/environment task (FR-1). This plan instead draws context directly from `requirements.md` (FR-1, NFR-4, NFR-5) and `unit-of-work.md`.

## Category Applicability
- **Scalability, Performance, Availability, Usability**: N/A — this is a one-time local build-environment setup and compile, not a running service with load/uptime/UX concerns.
- **Tech Stack Selection**: Applicable — the exact portable-MSYS2 acquisition mechanism is still open.
- **Security Requirements**: Applicable — NFR-5 (trusted sources, isolated location) and Security Baseline SECURITY-10/13 need concrete decisions.
- **Reliability / Maintainability**: Applicable — whether toolchain versions get recorded for reproducibility, given the setup itself is manual (per Application Design Q3).

## Plan Checklist
- [ ] Generate `aidlc-docs/construction/unit-1a-vanilla-build/nfr-requirements/nfr-requirements.md`
- [ ] Generate `aidlc-docs/construction/unit-1a-vanilla-build/nfr-requirements/tech-stack-decisions.md`

## Clarifying Questions

Please fill in each `[Answer]:` tag, then let me know when you're done.

### Question 1 — Portable MSYS2 Acquisition
How should the isolated MSYS2 toolchain be obtained?

A) Download the official MSYS2 "base" portable archive (`msys2-base-x86_64-<date>.tar.xz` from the official msys2.org/GitHub releases) and extract into a project-local folder — standard, well-documented, matches the CI recipe's package set
B) Use `pacman -Sy --root <project-folder>`-style install sourced from an existing system MSYS2 install, if one exists on this machine, to avoid a second download
C) No preference — choose the most reliable/reproducible approach
X) Other (please describe after [Answer]: tag below)

[Answer]: A

### Question 2 — Toolchain Version Recording
Should the exact toolchain package versions used be recorded in a manifest file for future reproducibility, even though the setup itself stays manual (per your earlier "one-time manual setup" decision)?

A) Yes — record exact package versions in a short manifest file
B) No — this is a one-off local setup, don't bother recording versions
X) Other (please describe after [Answer]: tag below)

[Answer]: A

### Question 3 — Isolated Toolchain Location
Exact path convention for the isolated toolchain under the project folder?

A) `.build-toolchain/` (leading dot, easy to `.gitignore`, visually separate from source)
B) `build-toolchain/` (no leading dot)
C) Somewhere else — describe
X) Other (please describe after [Answer]: tag below)

[Answer]: A

### Question 4 — Package Signature Verification
MSYS2's `pacman` verifies packages against signed package databases by default. Keep this fully enabled, or is relaxed/unattended verification acceptable for speed?

A) Keep full signature verification enabled — no shortcuts on package trust (matches NFR-5/SECURITY-13)
B) `--noconfirm` for unattended prompts is fine, but signature verification (`SigLevel`) itself must stay on
X) Other (please describe after [Answer]: tag below)

[Answer]: A
