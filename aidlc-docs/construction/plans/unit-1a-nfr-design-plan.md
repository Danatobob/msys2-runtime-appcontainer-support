# NFR Design Plan — Unit 1a (Vanilla Build Enablement)

## Category Applicability
- **Scalability Patterns**: N/A — no load/growth dimension for a one-time local build.
- **Performance Patterns**: N/A — no latency/throughput targets established in NFR Requirements; build wall-clock time isn't a tracked requirement.
- **Resilience Patterns**: Applicable (lightweight) — failure/retry behavior for the download+install steps.
- **Security Patterns**: Applicable — translating NFR-5's isolation/trust requirements into concrete verification steps.
- **Logical Components**: Applicable (lightweight) — lifecycle of the `.build-toolchain/` directory itself, since Unit 1b depends on it as a rebuild baseline.

## Plan Checklist
- [ ] Generate `aidlc-docs/construction/unit-1a-vanilla-build/nfr-design/nfr-design-patterns.md`
- [ ] Generate `aidlc-docs/construction/unit-1a-vanilla-build/nfr-design/logical-components.md`

## Clarifying Questions

Please fill in each `[Answer]:` tag, then let me know when you're done.

### Question 1 — Failure Handling & Archive Integrity
If the portable MSYS2 download or an individual `pacman` package install fails partway through (e.g. network interruption), how should this be handled?

A) Verify the downloaded archive's published checksum before extraction; on any partial failure (download or install), delete `.build-toolchain/` entirely and redo the setup from scratch rather than resuming a partial state
B) No archive-level checksum verification (rely on HTTPS transport security + `pacman`'s own package-level signature verification only); on partial failure, just retry the specific failed step manually without a full clean redo
X) Other (please describe after [Answer]: tag below)

[Answer]: A

### Question 2 — Toolchain Directory Lifecycle
Should `.build-toolchain/` be kept in place after Unit 1a completes (for reuse when Unit 1b rebuilds the patched source), or cleaned up once no longer immediately needed?

A) Leave it in place for the duration of the engagement — Unit 1b needs to rebuild from this same toolchain, and Unit 2's repro/validation runs need build outputs regenerated more than once
B) Clean it up once Unit 1a's own deliverables are produced, and re-set-up if/when Unit 1b needs it again
X) Other (please describe after [Answer]: tag below)

[Answer]: A
