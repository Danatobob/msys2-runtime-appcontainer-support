# Logical Components — Unit 2 (AppContainer Test Harness)

## `E:\Temp\appcontainer-harness\bin\` — Staged Test Binary Folder
- **Purpose**: Holds the copy of `bash.exe` + runtime DLL dependencies + the `--target` DLL under test (per Functional Design's Test Bin Staging).
- **Lifecycle**: Overwritten fresh at the start of every run — never accumulates run-specific subfolders.
- **Access**: Read+execute granted to the AppContainer profile's SID.

## `E:\Temp\appcontainer-harness\data\` — Scratch Data Folder
- **Purpose**: Writable working directory for the file-I/O smoke scenario.
- **Lifecycle**: Present for the duration of the engagement; individual scenario runs may leave/clean up their own test files within it, but the folder itself isn't recreated per run.
- **Access**: Read+write granted to the AppContainer profile's SID.

## `E:\Temp\appcontainer-harness\last-report.json` — Report Output
- **Purpose**: Machine-readable `SuiteReport` from the most recent run.
- **Lifecycle**: Overwritten every run (per the Maintainability requirement — no run-history clutter). If historical results matter for a specific phase, they're captured into `aidlc-docs/` documentation at that point in time by me, not retained here.

## AppContainer Profile (`AIDLC.AppContainerHarness`)
- **Purpose**: The reused sandbox identity every scenario in a run launches under.
- **Lifecycle**: Created on first use, persists across runs/sessions, only removed via explicit `--cleanup`.
- **Access it grants itself**: none beyond the two scratch folders above (Capability-Free, ACL-Scoped Profile pattern).

## Cross-Reference
These logical components correspond 1:1 to the workflow described in `functional-design/business-logic-model.md` — this document exists to record their lifecycle/access rules explicitly for implementation, not to redefine them.
