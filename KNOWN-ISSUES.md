# Known issues

Suspected bugs found while the plugin docs were checked against the source on 2026-11-13 (main at 0d81a50).
They were not fixed then. Each was seen while reading the code. None has been reproduced in AutoCAD, Inventor or a live Vault.
Line numbers refer to that commit.

## LSE_Env (acade-project-env)

- **"Trace wires" does nothing in LSEENV unless "Read every drawing" is also ticked** (`src/EnvironmentForm.cs:418`, `src/EnvironmentService.cs:33`). The only sign is a log line. LSEENVWIRES traces when either box is ticked (`WireSchemeForm.cs:22`). This is the likely cause of the "check box does not work" report. The tick boxes are explained in the README and in `docs/OPERATIONS.md` sections 4.1-4.6.
- Changing inputs after Plan does not disable Apply, so the old plan is applied (`EnvironmentForm.cs:189,291-299`).
- On first use it auto-plans and immediately shows "Pick a customer profile or a wire scheme first." (`EnvironmentForm.cs:76-81,410`).
- Labels still say "closed drawings only" (`EnvironmentForm.cs:29`, `SnapshotForm.cs:4`, `ProfilesForm.cs:564`).
- A read-only refusal is reported as "failed and was rolled back" (`EnvironmentForm.cs:553-558`).
- `bundle/.../PackageContents.xml:5` AppVersion is 1.6.0, but the csproj is 1.7.0 / r7.

## LSE_Doctor (acade-project-doctor)

- A Vault refusal is reported as "FAILED (rolled back)" (`ScanForm.cs:413`).
- Refusals are detected by searching the message text, which breaks if the wording changes (`DoctorForm.cs:416`).
- The scan saves ScanChecksDrawings every time, but the Doctor's matching box is never saved (`ScanForm.cs:206`).
- The settings form has no Cancel button, and Esc does nothing (`DoctorSettingsForm.cs:23`).
- The help text says help appears at the command line, but it opens a window (`HelpText.cs:65`).

## Catalog Tools (acade-catalog-tools)

- Assign with no block picked says "Assigned" and updates Recent, but nothing is written (`CatalogBrowserForm.cs:451-470`). `Apply` has no try/catch (`:470`).
- The backup and duplicate check run before the Vault checkout downloads the latest catalog. Rollback can then copy the stale file over the newer one (`CatalogTransactionService.cs:3-5`).
- After a check-in failure, the message says the record is still checked out, but it was already rolled back (`VaultCatalogService.cs:55,67`).
- The duplicate check reads at most 20,000 rows (`AccessCatalogService.cs`).
- The COLOR branch can never run (`AutoCadComponentService.cs:61-65`).
- The Settings, Admin and Diag commands have no try/catch (`Plugin.cs`).

## Librarian (acade-librarian)

- The read-only footprint lookup is not checked before the catalog row is written. You get an orphan row and a false "restored" message (`CatalogRecordForm.cs:507-520,561-576`).
- A message names a Vault Explorer setting the Librarian does not have (`VaultProbe.cs:60`).

## Project Tools (acade-project-creator)

- With Set WD_M off, the log says the WD_M library is missing when it is not (`ProjectTransactionService.cs:220`).
- SUCCESS is logged before the report and settings are saved. A failure there rolls everything back but leaves the report (`:226-232`).
- On success it saves a stale settings object, overwriting edits made in between (`:227`).
- Changing the product line silently reloads the drawing list and loses typed entries (`NewProjectForm.cs:686-689`).
- The read-only pre-check covers only the report file (`ProjectTransactionService.cs:205`).

## Wire To/From (acade-wire-tofrom)

All ten items listed here on 2026-11-13 were fixed in LSE_ToFrom build 2026-11-13-r1 (commit 166e5a8, smoke test 109/109). See `acade-wire-tofrom/docs/DEVELOPER-GUIDE.md`. Still open: it has no Vault probe, and it isn't part of the combined LSE-Plugins release.

## Footprint Tools (inventor-addin)

- The part number and duplicate check use the local copy before the Vault checkout, so duplicates are possible (`FootprintTransactionService.cs:10,15`).
- A failure after check-in leaves the row in Vault and logs a false undo PASS (`:17-21`).
- Misleading messages after a check-in failure, as in Catalog Tools (`VaultCatalogService.cs:55,67`).
- Setting the part attribute to MFG or CAT breaks every create (`FootprintTransactionService.cs:13`).
- A lock clash leaves an empty backup folder (`:6`).

## Other

- Catalog Tools, Librarian, Project Tools and Footprint Tools have no build tag, and their AppVersion is still 1.0.0. Only Doctor and Env carry one (`BuildInfo.cs`).
- Tests the docs mention are not in the repo: Project Tools' 55-check test and the Doctor's 90-check harness.
- `agents/lse-catalog-data.md` still says Vault uses Windows authentication only.
- `inventor-addin/docs/pending/PENDING-LOCK-AND-ASSEMBLYCODE.md` says the source still matches r4, which is no longer true.
