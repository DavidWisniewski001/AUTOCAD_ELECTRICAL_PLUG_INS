# Pending work: shared library lock + blank ASSEMBLYCODE fix (paused 2026-11-12)

Status: **paused by the user** on 2026-11-12 at 08:10 while they gather more information about the Inventor side.
Nothing below is in the code. `src\` matches release 2026-11-12-r4 exactly. Do not start this work until the user says so.

## Why it exists
- The AutoCAD LSE Librarian now has two writers of `footprint_lookup.mdb` (its library transaction and `LSEFOOTPRINTMAP`).
  Both take `LSE_Library.lock`. This add-in also writes `footprint_lookup.mdb` (and `default_cat.mdb`) when it issues a
  footprint, but it only takes `LSE_PartNumber.lock`, so it can write the lookup MDB at the same time as a Librarian run.
- `AccessCatalogService.Find` and `Upsert` filter on `ASSEMBLYCODE IS NULL`. Real lookup data has both NULL and empty-string
  ASSEMBLYCODE, so rows with `''` are missed and can be duplicated.

## User approvals already given (2026-11-12, in the hub chat)
- The user said "Everything is ok" to both items, then answered **"Yes"** to: "Do you confirm this exact lock-order change in
  the Inventor add-in (part-number lock first, then the Librarian's LSE_Library.lock just before the footprint_lookup.mdb write,
  released in reverse order on every path including rollback, contention fails with a clear message and rolls back), plus the
  blank assembly code fix (NULL or empty in Upsert and Find)?"
- Then paused it: "Cancel the inventor build I will get more information." **Re-confirm with the user before restarting**;
  the guardian's rule G1 also requires the user's own approval in the session that makes the change.

## Plan (from lse-footprint-tools-guardian)
1. Read `acade-librarian\docs\DATA-STORES.md` and `CODE-REFERENCE.md` (read-only) for the `LSE_Library.lock` path, file format,
   acquire/release and stale-lock handling.
2. Add a second lock to `IssuanceLock` with the same path and semantics as the Librarian's.
3. Order in `FootprintTransactionService.Execute`: `LSE_PartNumber.lock` first (as today); after the backup, acquire
   `LSE_Library.lock` just before the `footprint_lookup.mdb` write; release both in reverse order in `finally`, including on
   rollback. If the Library lock is held: clear message ("The LSE Librarian is currently updating the footprint library...
   try again in a minute") and roll back. Document the order PartNumber -> Library for any tool that takes both.
4. ASSEMBLYCODE: match `(ASSEMBLYCODE IS NULL OR ASSEMBLYCODE='')` in `Find` and `Upsert`. A ready-made version of the file
   is in `AccessCatalogService.cs.assemblycode-fix.txt` (diff: `assemblycode-fix.diff`). It was written by an agent but was
   never built or tested. Re-check it against the current `src\` before using it.
5. Tests on copies of the real MDBs (`AutoCAD_Electrical_2026\Catalogs\`, copy to %TEMP% first): lock acquired/released,
   contention refused with rollback, release on failure, empty-string ASSEMBLYCODE row found and not duplicated.
   Update README and docs.

## Open questions the user is looking into
- Build environment: this PC has no Inventor 2026 interop and no installed .NET SDK, so only a partial build
  (`-p:InventorBin=`) is possible here. Inventor behaviour cannot be verified on this PC.
- Whatever else the user brings back about how the add-in is used alongside the Librarian.

## Release
After it's done, ship it in the next combined release per the project-root CLAUDE.md (archive Current first, upload to
Releases/Current only, the user approves the upload).
