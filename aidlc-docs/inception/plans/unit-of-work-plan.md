# Unit of Work Plan

## Context
Source: `aidlc-docs/inception/application-design/` (already settled on a 2-component decomposition: winsup/cygwin AppContainer support, AppContainer C# test harness). No user stories exist (stage skipped) — `unit-of-work-story-map.md` will map **requirements (FR-1..FR-4)** to units instead of stories.

## Category Applicability
- **Story Grouping**: N/A as literal "stories" (none exist) — reframed as requirement-to-unit mapping; see Question 1 for unit-boundary confirmation.
- **Dependencies**: Already established in `component-dependency.md` (no compile-time coupling, runtime-only relationship) — Question 4 confirms the integration mechanism.
- **Team Alignment**: N/A — single-requester, AI-driven engagement with no team/ownership boundaries to negotiate.
- **Technical Considerations**: Applicable — the two units target genuinely different platforms (native C/autotools vs .NET); see Question 2.
- **Business Domain**: N/A — this is a systems/runtime engagement, not a business-domain application with bounded contexts.
- **Code Organization**: Applicable to Unit 2 only (new/greenfield C# project); see Question 3.

## Plan Checklist
- [x] Generate `aidlc-docs/inception/application-design/unit-of-work.md` with unit definitions and responsibilities
- [x] Generate `aidlc-docs/inception/application-design/unit-of-work-dependency.md` with dependency matrix
- [x] Generate `aidlc-docs/inception/application-design/unit-of-work-story-map.md` mapping requirements (FR-1..FR-4) to units
- [x] Document Unit 2 code organization strategy (greenfield component) in `unit-of-work.md`
- [x] Validate unit boundaries and dependencies
- [x] Ensure all requirements are assigned to units

## Final Decisions (all questions resolved)
- **Q1 = B**: Unit 1 split into **Unit 1a (vanilla build enablement)** and **Unit 1b (AppContainer patch)**.
- **Q2 = resolved to .NET 10** (`net10.0`) — newest SDK already installed on this machine (`dotnet --list-sdks` showed 2.0 through 10.0.301).
- **Q3 = A**: Unit 2 (harness) is a single console project, no separate class-library split.
- **Q4 = A**: Harness accepts the target executable path as a runtime argument (`--target <path>`).

## Clarifying Questions

Please fill in each `[Answer]:` tag, then let me know when you're done.

### Question 1 — Unit Boundary Confirmation
Application Design settled on 2 units (winsup/cygwin patch; test harness). Given FR-1 (vanilla build) is explicitly flagged as the biggest unknown/risk in the whole engagement, should it be tracked as its own separate unit from the actual patch work?

A) Keep as a single Unit 1 (build enablement + patch together) — they're sequential phases of the same component/toolchain, no need for a separate unit
B) Split into two units: "Unit 1a: vanilla build enablement" and "Unit 1b: AppContainer patch" — track the build-feasibility milestone separately given it's the biggest risk
X) Other (please describe after [Answer]: tag below)

[Answer]: B

### Question 2 — Unit 2 Technical Considerations (.NET target)
What .NET target should the harness use?

A) .NET 8 (or latest LTS) — modern, no legacy constraints
B) .NET Framework 4.8 — maximizes compatibility if this needs to run on older/locked-down Windows images
C) No preference — choose what's most reliable for P/Invoke-heavy Win32 security API usage
X) Other (please describe after [Answer]: tag below)

[Answer]: X whatever .NET is already installed in the OS and is suitable for the task.

**Resolved**: Checked this machine — `dotnet --list-sdks` shows SDKs from 2.0 through 10.0.301 installed, with `dotnet --version` defaulting to 10.0.301. Target framework: **.NET 10** (`net10.0`) — newest installed, no extra install needed, and modern P/Invoke/interop support for the Win32 security APIs the harness needs.

### Question 3 — Unit 2 Code Organization
How should the harness project be structured under `tools/appcontainer-harness/`?

A) Single console project containing everything (Profile Manager, Launcher, Scenario Library, Runner, CLI all in one project)
B) Solution with separate projects: a core class library (Profile Manager, Launcher, Scenario Library, Runner) + a thin CLI executable project
X) Other (please describe after [Answer]: tag below)

[Answer]: A

### Question 4 — Cross-Unit Integration Mechanism
Unit 2 needs Unit 1's build output to run meaningful tests. Should the harness accept the target executable path as a runtime argument, or use a fixed/expected relative-path convention?

A) Runtime argument only (`--target <path>`) — most flexible, matches the "no compile-time coupling" design
B) Fixed relative-path convention (e.g. a known build-output folder) with an optional override argument — less typing during iterative testing
X) Other (please describe after [Answer]: tag below)

[Answer]: A
