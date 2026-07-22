# Unit of Work — Requirement Map

**Note**: User Stories stage was skipped (no distinct personas/workflows — see `execution-plan.md`), so this maps `requirements.md`'s functional and non-functional requirements to units instead of stories.

## Functional Requirements

| Requirement | Unit(s) | Notes |
|---|---|---|
| FR-1: Vanilla build | Unit 1a | Sole owner — no source changes, build/toolchain only |
| FR-2: Independent failure reproduction | Unit 2 (harness build) + Unit 1a (test target) | Harness is built in Unit 2; the repro test itself runs Unit 2 against Unit 1a's output |
| FR-3: Startup patch | Unit 1b | Sole owner — both `get_shared_parent_dir()` and `get_session_parent_dir()` |
| FR-4: Further-wall investigation | Unit 1b (fixes) + Unit 2 (test execution) | Iterative — Unit 2 surfaces failures, Unit 1b addresses them, repeat until scenarios pass or remaining walls are documented as findings |

## Non-Functional Requirements

| Requirement | Unit(s) | Notes |
|---|---|---|
| NFR-1: Security isolation hard constraint | Unit 1b | Design constraint on the patch itself |
| NFR-2: Fail-safe defaults | Unit 1b | `api_fatal()` fail-closed behavior preserved/extended |
| NFR-3: Non-sandboxed path tolerance | Unit 1b | Governs how cautious the patch needs to be for the existing (non-AppContainer) code path |
| NFR-4: Windows 10/11 target | Unit 1a, Unit 1b | Both the build environment and the patch's OS-version assumptions |
| NFR-5: Build/toolchain supply chain | Unit 1a | Isolated install location, trusted MSYS2 sources only |
| NFR-6: Smoke-test coverage | Unit 2 (Scenario Library) | Executed against Unit 1b's output as the final validation gate |
| NFR-7: Deliverables | All units | Patch (1b) + report (all) + built/tested binaries (1a build, 1b patched build, validated by 2) |

## Coverage Check
All 4 functional requirements and all 7 non-functional requirements from `requirements.md` are assigned to at least one unit. No orphaned requirements.
