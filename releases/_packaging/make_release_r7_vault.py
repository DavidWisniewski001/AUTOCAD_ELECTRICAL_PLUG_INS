"""Combined r7 package: the Doctor tick-box cut (peer session's make_release_r7.py guide edits) plus the Vault 2026 work.
Usage: python -I make_release_r7_vault.py <repo root> <build root with catalog-tools/ project-tools/ doctor/ librarian/ env/> <netload-src folder>
Writes releases/LSE-Plugins-2026-11-12-r7/: six source zips, ALL zip (with the guide and the NETLOAD folder), NETLOAD-READY zip, guide.
"""
import os, sys, zipfile, hashlib, io, re
root, build, nlsrc = sys.argv[1], sys.argv[2], sys.argv[3]
tag, old = "2026-11-12-r7", "2026-11-12-r6"
out = os.path.join(root, "releases", "LSE-Plugins-" + tag); os.makedirs(out, exist_ok=True)
oldrel = os.path.join(root, "releases", "LSE-Plugins-" + old)
top = "LSE-Plugins-NETLOAD"

# ---------------------------------------------------------------- guide: r6 -> peer's r7 edits -> Vault edits ----
g = open(os.path.join(oldrel, "LSE-Plugins-BUILD-AND-INSTALL-GUIDE.md"), encoding="utf-8").read()
n = 0
def rep(a, b, must=True):
    global g, n
    c = g.count(a)
    if c != 1:
        print(("MISSING" if must else "skip") + f" ({c}x): " + a[:90].replace("\n", "\\n")); return
    g = g.replace(a, b); n += 1
# the peer session's r7 edits, reproduced by executing the guide part of its script against g
peer = open(os.path.join(root, "releases", "_packaging", "make_release_r7.py"), encoding="utf-8").read()
peer_guide = peer[peer.index("rep(\"Release date"):peer.index("open(os.path.join(out, \"LSE-Plugins-BUILD-AND-INSTALL-GUIDE.md\")")]
exec(peer_guide)
peer2 = open(os.path.join(root, "releases", "_packaging", "make_netload_r7.py"), encoding="utf-8").read()
peer2_guide = peer2[peer2.index("rep(\"This supersedes"):peer2.index("open(gp, \"w\"")]
exec(peer2_guide)
print("peer guide edits applied:", n)

# Vault edits
rep("`LSEDOCTOR` and check the title bar shows `[2026-11-12-r7]`.** LSE_Env is unchanged since r6 (build tag `[2026-11-12-r6]`): its `LSEENV` plan grid no longer",
    "`LSEDOCTOR` and check the title bar shows `[2026-11-12-r7]`.** LSE_Env is build `[2026-11-12-r7]` (the r6 grid fix plus the Vault guard, 91-check smoke test): its `LSEENV` plan grid no longer")
rep("**r7 also ships ready-to-load binaries (first added in r6; the `LSE_Doctor\\` folder and bundle inside are the r7 build, the rest is the r6 build):**",
    "**r7 also ships ready-to-load binaries (first added in r6; every DLL in r7 is a fresh r7 build):**")
rep("(for example `[2026-11-12-r6]` for LSE_Env and `[2026-11-12-r7]` for LSE_Doctor)", "(`[2026-11-12-r7]` for both)", must=False)
g = g.replace("`LSEENV` and check the title bar shows `[2026-11-12-r6]`.**", "`LSEENV` and check the title bar shows `[2026-11-12-r7]`.**")
g = g.replace("Every LSE_Env window title now ends with the build tag\n`[2026-11-12-r6]`, and the error dialog", "Every LSE_Env window title now ends with the build tag\n`[2026-11-12-r7]` (r6 introduced the tag), and the error dialog")
lines = g.split("\n")
for i, l in enumerate(lines):
    if l.startswith("| `LSE_Env-2026-11-12-r7.zip`"): lines[i] = l.replace("build tag `[2026-11-12-r6]`", "build tag `[2026-11-12-r7]`")
g = "\n".join(lines)
rep("This guide is the single starting point.",
    "**r7 is also the Vault 2026 cut (all six plugins).** Autodesk moved AutoCAD 2026.1.2, Inventor 2026.4 and Vault 2026.4 to .NET 10 in\n"
    "August 2026 (no API changes); the plugins stay net8.0-windows and still load, but a Vault 2026.4 client cannot be loaded into an\n"
    "AutoCAD or Inventor that is still on .NET 8, so **install the 2026 updates of AutoCAD, Inventor and Vault together**. Every `...DIAG`\n"
    "command now prints the host .NET, the installed Vault client (folder, file version, which .NET it is built for, compatible or not) and\n"
    "whether the Vault add-in in AutoCAD / Inventor is logged in. **Catalog Tools and Footprint Tools** share a rewritten\n"
    "`VaultCatalogService.cs`: the old code could not work against Vault 2025/2026 (credential constructor shape, an `out` parameter passed\n"
    "through `dynamic`, an undo-check-out lookup that never matched, a missing-file result read as a file, and a stale working copy that\n"
    "could be checked in over a newer catalog). The new code uses the Vault add-in's logged-in session first, then the standard Vault\n"
    "login dialog (Vault, Windows and Autodesk ID accounts), then Windows authentication; check-out fetches the latest Vault version when\n"
    "the working copy differs, refuses when another user holds the check-out, and check-in / undo go through the Vault Framework with the\n"
    "raw DocumentService as fallback. It is verified by a 27-check regression test against a stub SDK that carries the real type names\n"
    "(`tests\\VaultFakeTest` in both plugins), **not yet against a live Vault**: first run `LSECATALOGSETTINGS` > **Test Vault** (or the\n"
    "Inventor **Test** button). **Librarian, Project Tools, LSE_Doctor and LSE_Env** never talk to Vault; they now refuse before writing\n"
    "when any target in the working folder is read-only (Vault's not-checked-out state), naming every locked file, and after a successful\n"
    "run list the files to check in (shared `VaultProbe.cs`). Project Tools also refuses when a sidecar file already exists in the job folder.\n"
    "The seventh plugin folder in the source tree, `acade-wire-tofrom` (LSE_ToFrom), is a separate work in progress and is not in r7.\n\n"
    "This guide is the single starting point.")
def row_append(zipname, text):
    global g, n
    lines = g.split("\n")
    for i, l in enumerate(lines):
        if l.startswith("| `" + zipname + "`") and l.rstrip().endswith("|"):
            lines[i] = l.rstrip()[:-1].rstrip() + " " + text + " |"; g = "\n".join(lines); n += 1; return
    print("MISSING row:", zipname)
row_append(f"LSE.AutoCADElectrical.CatalogTools-{tag}.zip", "r7: Vault code rewritten for the Vault 2025/2026 SDK (27-check `tests\\VaultFakeTest` against a stub SDK; not run against a live Vault); `Test Vault` button in `LSECATALOGSETTINGS`; `LSECATALOGDIAG` prints host .NET, Vault client and add-in login; a Vault-locked catalog is refused before the write when Vault is off. README added.")
row_append(f"LSE.AutoCADElectrical.ProjectTools-{tag}.zip", "r7: refuses before writing when a sidecar file already exists in the job folder; the report lists the new files to add to Vault; `LSEPROJECTDIAG` prints the Vault lines and a Get-Latest hint for missing template files.")
row_append(f"LSE_Doctor-{tag}.zip", "r7 also: one refusal naming every read-only (not checked out) file before any write, check-in reminder in the repair log, Vault lines in `LSEDOCTORDIAG`.")
row_append(f"LSE.AutoCADElectrical.Librarian-{tag}.zip", "r7: refuses before the lock and backups when the icon menu, library drawing, lookup or catalog is read-only (Vault: not checked out), lists what to check in afterwards, `LSELIBRARYDIAG` shows read-only/writable per file plus the Vault lines.")
row_append(f"LSE_Env-{tag}.zip", "r7: `VaultProbe` guard (one refusal naming every read-only file, check-in reminder in the apply log), Vault lines in `LSEENVDIAG`, build tag `[2026-11-12-r7]`, 91-check smoke test.")
row_append(f"LSE.Inventor.FootprintTools-{tag}.zip", "r7: same rewritten Vault code as Catalog Tools (`src\\VaultCatalogService.cs`, 27-check `tests\\VaultFakeTest`), Vault test inside the Settings **Test** button, read-only refusal for the lookup / catalog; still never compiled against Inventor. The paused lock / assembly-code fix in `docs\\pending` stays out of the zip.")
g = re.sub(r"\(expect 89 passed\)", "(expect 91 passed)", g)
# section 2 rows
def row_replace(prefix, text):
    global g, n
    lines = g.split("\n")
    for i, l in enumerate(lines):
        if l.startswith(prefix): lines[i] = text; g = "\n".join(lines); n += 1; return
    print("MISSING row:", prefix)
row_replace("| Vault Explorer (optional) |",
    "| Vault Client 2026 (optional) | Catalog Tools admin adds, Footprint Tools catalog check-in | Only when the \"Catalog Vault path\" setting is filled in. The plugins use the session of the Vault add-in inside AutoCAD / Inventor when it is logged in; otherwise they load the SDK by reflection from `C:\\Program Files\\Autodesk\\Vault Client 2026\\Explorer` (auto-discovered when the setting is blank or wrong) and show the Vault login dialog. **.NET 10:** AutoCAD 2026.1.2, Inventor 2026.4 and Vault 2026.4 are .NET 10 builds (August 2026); install them together. `...DIAG` and **Test Vault** print the host .NET, the Vault client's .NET and whether they match. |")
row_replace("| Write access to the Vault working folder |",
    "| Vault working folder | Librarian, Doctor, Project Tools, LSE_Env | `C:\\_Lake_Shore_E_Vault\\...` as listed in `Locations.txt`. Files that are not checked out are read-only: each plugin refuses before writing, names every locked file, and after a successful run lists the files to check in (new files: Add to Vault). None of the four checks files in or out itself. Template files that were never fetched: Vault Explorer > Get Latest Version. |")
rep("shared framework that AutoCAD 2026 runs on, so nothing else has to be installed beside the 64-bit Access Database Engine (section 2).",
    "shared framework that AutoCAD 2026 runs on, so nothing else has to be installed beside the 64-bit Access Database Engine (section 2).\nOn AutoCAD 2026.1.2 or later the host runs .NET 10; a .NET 8 class library still loads there (Autodesk: no API changes) and `...DIAG` prints the active runtime.")
# section 5
rep("Vault server/vault/path (leave blank to skip Vault), LSE part attribute",
    "Vault server, vault name, Catalog Vault path as `$/folder/.../default_cat.mdb` (blank = no Vault), then **Test Vault**; LSE part attribute")
rep("naming standard JSON, units, layers, Vault; press **Test**", "naming standard JSON, units, layers, Vault (Catalog Vault path `$/...`, blank = no Vault); press **Test** (includes the Vault connection test)")
# section 7
rep("**Project Tools**\n- `.wdp`, `.wdt` and `.wdl` line formats and ordering.",
    "**Vault (all plugins)**\n- The Vault SDK calls (add-in session via `Connectivity.Application.VaultBase`, `Forms.Library.Login`, `WinAuthCredentials`, `AcquireFiles`, `CheckinFile`, `UndoCheckoutFile`) are verified only against a stub SDK with the real type names. First real test: `LSECATALOGSETTINGS` > **Test Vault** against the live vault (read-only: it logs in, finds the catalog, compares the working copy), then `LSECATALOGADMIN` on a COPY whose Catalog Vault path points at a test copy of the MDB inside Vault.\n- The read-only refusal and the check-in reminder in the other four plugins rely on Vault leaving files that are not checked out read-only (the working-folder default).\n- A .NET 10 Vault client inside a .NET 8 host is refused with an explanation; the reverse (older client, newer host) is loaded and untested.\n\n**Project Tools**\n- `.wdp`, `.wdt` and `.wdl` line formats and ordering.")
rep("- Vault checkout/checkin is copied from the Catalog Tools plugin; the footprint DWG and `footprint_lookup.mdb` are not Vault-controlled.",
    "- Vault check-out/check-in is the same `VaultCatalogService.cs` as in Catalog Tools (verified against the stub SDK only); the footprint DWG and `footprint_lookup.mdb` are not Vault-controlled and are only guarded against read-only files.")
# section 8
row_replace("| Vault errors on admin add / footprint create |",
    "| Vault errors on admin add / footprint create | Run `LSECATALOGSETTINGS` > **Test Vault** (Inventor: the **Test** button) and read the report. \"not found\" Vault client: install the Vault Client 2026 or point the Vault Explorer folder setting at its `Explorer` folder. \"built for .NET 10 but ... running .NET 8\": update AutoCAD to 2026.1.2 / Inventor to 2026.4 so the host matches Vault 2026.4. \"not found in Vault at $/...\": the Catalog Vault path must be the file's path inside Vault (Vault Explorer > Properties). \"checked out in Vault by <user>\": wait for the check-in. \"login was cancelled\": nothing was changed. Leave the Catalog Vault path blank to skip Vault entirely. |")
row_replace("| Doctor or Project Tools says a template is missing |",
    "| Doctor or Project Tools says a template is missing | The vault working folder is not at `C:\\_Lake_Shore_E_Vault\\...`, or the files were never fetched: in Vault Explorer select the folder and Get Latest Version. Point the Settings dialog at the real path; the Doctor falls back to the bundled `standard\\` copies (no drawings, so no `WD_M` insertion). |\n| \"This file is read-only ... check them out in Vault first\" | Vault keeps files that are not checked out read-only. Check them out (Vault tab or Vault Explorer), run the command again; if the file is not under Vault control, clear the read-only attribute. Nothing was written. |")
gp = os.path.join(out, "LSE-Plugins-BUILD-AND-INSTALL-GUIDE.md")
open(gp, "w", encoding="utf-8", newline="\n").write(g)
print("guide edits total:", n)

# ---------------------------------------------------------------- source zips ----
plugins = {"acade-catalog-tools": "LSE.AutoCADElectrical.CatalogTools", "acade-project-creator": "LSE.AutoCADElectrical.ProjectTools", "acade-project-doctor": "LSE_Doctor", "acade-project-env": "LSE_Env", "acade-librarian": "LSE.AutoCADElectrical.Librarian", "inventor-addin": "LSE.Inventor.FootprintTools"}
SKIP = ("bin", "obj", ".claude", ".git")
def add_tree(z, folder):
    for dp, dn, fn in os.walk(os.path.join(root, folder)):
        dn[:] = [d for d in dn if d not in SKIP and not (folder == "inventor-addin" and d == "pending" and os.path.basename(dp) == "docs")]
        for f in sorted(fn):
            full = os.path.join(dp, f); z.write(full, os.path.relpath(full, root).replace("\\", "/"))
for folder, asm in plugins.items():
    p = os.path.join(out, f"{asm}-{tag}.zip")
    with zipfile.ZipFile(p, "w", zipfile.ZIP_DEFLATED) as z: add_tree(z, folder)
    names = zipfile.ZipFile(p).namelist()
    print(os.path.basename(p), len(names), "entries; VaultProbe:", any(n_.endswith("VaultProbe.cs") for n_ in names), "; pending:", any("docs/pending" in n_ for n_ in names))

# ---------------------------------------------------------------- NETLOAD folder from fresh builds ----
nl = {"LSE.AutoCADElectrical.CatalogTools": ("catalog-tools", "acade-catalog-tools", "LSE.CatalogTools.bundle"),
      "LSE.AutoCADElectrical.ProjectTools": ("project-tools", "acade-project-creator", "LSE.ProjectTools.bundle"),
      "LSE_Doctor": ("doctor", "acade-project-doctor", "LSE_Doctor.bundle"),
      "LSE.AutoCADElectrical.Librarian": ("librarian", "acade-librarian", "LSE.Librarian.bundle"),
      "LSE_Env": ("env", "acade-project-env", "LSE_Env.bundle")}
entries = []
for asm, (bdir, folder, bundle) in nl.items():
    src = os.path.join(build, bdir); dll = os.path.join(src, asm + ".dll")
    assert os.path.isfile(dll), "missing build: " + dll
    if asm in ("LSE_Doctor", "LSE_Env"): assert tag.encode("utf-16-le") in open(dll, "rb").read(), asm + " does not carry the r7 build tag"
    manifest = os.path.join(root, folder, "bundle", bundle, "PackageContents.xml"); assert os.path.isfile(manifest), manifest
    for dp, dn, fn in os.walk(src):
        for f in sorted(fn):
            full = os.path.join(dp, f); relp = os.path.relpath(full, src).replace("\\", "/"); b = open(full, "rb").read()
            entries.append((f"{top}/{asm}/{relp}", b)); entries.append((f"{top}/ApplicationPlugins/{bundle}/Contents/{relp}", b))
    entries.append((f"{top}/ApplicationPlugins/{bundle}/PackageContents.xml", open(manifest, "rb").read()))
for f in ("LSE-NETLOAD-ALL.lsp", "LSE-NETLOAD-ALL.scr", "NETLOAD-README.md"):
    t = open(os.path.join(nlsrc, f), encoding="utf-8").read().replace(old, tag)
    if f.endswith(".lsp"):
        t = t.replace("; LSEDOCTOR LSEDOCTORSCAN LSEDOCTORSETTINGS LSEDOCTORDIAG\n", "; LSEDOCTOR LSEDOCTORSCAN LSEDOCTORSETTINGS LSEDOCTORDIAG LSEDOCTORHELP\n")
    if f.endswith(".md"):
        t = t.replace("| `LSEDOCTOR`, `LSEDOCTORSCAN`, `LSEDOCTORSETTINGS`, `LSEDOCTORDIAG` |", "| `LSEDOCTOR`, `LSEDOCTORSCAN`, `LSEDOCTORSETTINGS`, `LSEDOCTORDIAG`, `LSEDOCTORHELP` |")
        i = t.index("## Status\n\n") + len("## Status\n\n")
        t = t[:i] + ("All five DLLs are fresh r7 builds (2026-11-12): LSE_Doctor `[2026-11-12-r7]` (tick boxes handled by the form, `note` rows, Help, `LSEDOCTORHELP`),\n"
                     "LSE_Env `[2026-11-12-r7]`, and every plugin carries the Vault 2026 changes (Catalog Tools: rewritten Vault check-out/check-in with a **Test Vault**\n"
                     "button; the others: refusal before writing when a working-folder file is read-only, check-in reminder, Vault lines in `...DIAG`).\n"
                     "On AutoCAD 2026.1.2 or later the host is .NET 10; these .NET 8 DLLs still load there.\n\n") + t[i:]
    entries.append((f"{top}/{f}", t.encode("utf-8")))
entries.sort(key=lambda e: e[0])
zp = os.path.join(out, f"LSE-Plugins-NETLOAD-READY-{tag}.zip")
with zipfile.ZipFile(zp, "w", zipfile.ZIP_DEFLATED) as z:
    for name, data in entries: z.writestr(name, data)
print(os.path.basename(zp), len(entries), "entries,", sum(1 for e in entries if e[0].endswith(".dll")), "dlls")

# ---------------------------------------------------------------- ALL zip ----
p = os.path.join(out, f"LSE-Plugins-ALL-{tag}.zip")
with zipfile.ZipFile(p, "w", zipfile.ZIP_DEFLATED) as z:
    for folder in plugins: add_tree(z, folder)
    add_tree(z, "agents")
    for f in ["README.docx", "STYLE-GUIDE.docx", "README.md"]: z.write(os.path.join(root, f), f)
    z.write(os.path.join(root, "reference", "Locations.txt"), "Locations.txt")
    z.write(gp, "LSE-Plugins-BUILD-AND-INSTALL-GUIDE.md")
    for name, data in entries: z.writestr(name, data)
names = zipfile.ZipFile(p).namelist()
print("ALL", len(names), "entries; stray bin/obj:", any(("/bin/" in x or "/obj/" in x) and not x.startswith(top + "/") for x in names), "; tofrom:", any(x.startswith("acade-wire-tofrom") for x in names))
for f in sorted(os.listdir(out)):
    fp = os.path.join(out, f); print(hashlib.sha256(open(fp, "rb").read()).hexdigest()[:16], f"{os.path.getsize(fp):>9}", f)
