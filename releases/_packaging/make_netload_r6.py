import os, sys, shutil, zipfile, hashlib, io
root = sys.argv[1]
tag = "2026-11-12-r6"
rel = os.path.join(root, "releases", "LSE-Plugins-" + tag)
final = r"C:\Users\david\.claude\jobs\41ec834d\tmp\final"
stage = r"C:\Users\david\.claude\jobs\41ec834d\tmp\netload-stage"
work = r"C:\Users\david\.claude\jobs\41ec834d\tmp\netload-pkg"
top = "LSE-Plugins-NETLOAD"
plugins = [  # (assembly, source folder, bundle folder name)
    ("LSE.AutoCADElectrical.CatalogTools", "acade-catalog-tools", "LSE.CatalogTools.bundle"),
    ("LSE.AutoCADElectrical.ProjectTools", "acade-project-creator", "LSE.ProjectTools.bundle"),
    ("LSE_Doctor", "acade-project-doctor", "LSE_Doctor.bundle"),
    ("LSE.AutoCADElectrical.Librarian", "acade-librarian", "LSE.Librarian.bundle"),
    ("LSE_Env", "acade-project-env", "LSE_Env.bundle"),
]

# ---- 1. assemble the NETLOAD folder --------------------------------------------------------------
if os.path.isdir(work): shutil.rmtree(work)
pkg = os.path.join(work, top); os.makedirs(pkg)
for asm, src, bundle in plugins:
    out = os.path.join(final, asm)
    assert os.path.isfile(os.path.join(out, asm + ".dll")), out
    shutil.copytree(out, os.path.join(pkg, asm))
    manifest = os.path.join(root, src, "bundle", bundle, "PackageContents.xml")
    assert os.path.isfile(manifest), manifest
    bdir = os.path.join(pkg, "ApplicationPlugins", bundle); os.makedirs(bdir)
    shutil.copy2(manifest, bdir)
    shutil.copytree(out, os.path.join(bdir, "Contents"))
for f in ("LSE-NETLOAD-ALL.lsp", "LSE-NETLOAD-ALL.scr", "NETLOAD-README.md"):
    shutil.copy2(os.path.join(stage, f), pkg)

# sanity: Env DLL carries the r6 build tag, every DLL is present once per place
env = open(os.path.join(pkg, "LSE_Env", "LSE_Env.dll"), "rb").read()
assert tag.encode("utf-16-le") in env, "LSE_Env.dll does not carry the r6 build tag"
print("LSE_Env.dll build tag:", tag)

def walk(folder):
    for dp, dn, fn in os.walk(folder):
        dn.sort()
        for f in sorted(fn):
            full = os.path.join(dp, f); yield full, os.path.relpath(full, work).replace("\\", "/")

zp = os.path.join(rel, f"LSE-Plugins-NETLOAD-READY-{tag}.zip")
with zipfile.ZipFile(zp, "w", zipfile.ZIP_DEFLATED) as z:
    for full, arc in walk(pkg): z.write(full, arc)
names = zipfile.ZipFile(zp).namelist()
print(os.path.basename(zp), len(names), "entries,", sum(1 for n in names if n.endswith(".dll")), "dlls")

# ---- 2. guide edits in place ---------------------------------------------------------------------
gp = os.path.join(rel, "LSE-Plugins-BUILD-AND-INSTALL-GUIDE.md")
g = open(gp, encoding="utf-8").read()
n = 0
def rep(a, b, count=1):
    global g, n
    c = g.count(a)
    if c != count: print(f"MISSING/AMBIGUOUS ({c}x):", a[:90].replace("\n", "\\n")); return
    g = g.replace(a, b); n += 1

rep("`LSEENV` and check the title bar shows `[2026-11-12-r6]`.** The other five plugins are unchanged since r5.",
    "`LSEENV` and check the title bar shows `[2026-11-12-r6]`.** **r6 also ships ready-to-load binaries:**\n"
    f"`LSE-Plugins-NETLOAD-READY-{tag}.zip` holds all five AutoCAD Electrical plugins compiled Release (0 warnings) with their JSON,\n"
    "`standard\\` and `profiles\\` files, a one-step loader (`LSE-NETLOAD-ALL.lsp`, command `LSELOAD`) and ready-made `ApplicationPlugins`\n"
    "bundles, so nothing has to be built on the workstation (sections 3.0 and 4.1). Catalog Tools compiled for the first time in r6 (four\n"
    "nullable warnings fixed, `bundle\\LSE.CatalogTools.bundle` added); the source of the other four AutoCAD plugins is unchanged since r5.")
rep("Four compile cleanly against the Autodesk reference package and passed console smoke tests of their file logic,\n"
    "two have never been compiled, and none has ever been loaded into AutoCAD Electrical or Inventor.",
    "All five AutoCAD Electrical plugins compile cleanly (0 warnings) against the Autodesk reference package and four of them passed\n"
    "console smoke tests of their file logic; the Inventor add-in has never been compiled; and none has ever been loaded into AutoCAD\n"
    "Electrical or Inventor.")
rep("| Source only (10 files). The `.csproj` was added at packaging time; it has not been compiled. |",
    "| Compiles Release, 0 warnings (first built 2026-11-12 in r6; four nullable warnings fixed, `bundle\\LSE.CatalogTools.bundle` added). No console smoke test: its logic runs through AutoCAD and late-bound ADODB. Not loaded in AutoCAD. |")
rep(f"| `LSE-Plugins-ALL-{tag}.zip` | all of the above | one folder per plugin plus `agents\\`, `README.docx`, `STYLE-GUIDE.docx`, `Locations.txt`, `README.md` | | The same content as the six zips, in one download. |",
    f"| `LSE-Plugins-NETLOAD-READY-{tag}.zip` | AutoCAD Electrical 2026 | `LSE-Plugins-NETLOAD\\` | `LSE-NETLOAD-ALL.lsp` (drag into AutoCAD; defines `LSELOAD`), `LSE-NETLOAD-ALL.scr`, `NETLOAD-README.md` | **Prebuilt binaries, no build needed.** The five AutoCAD plugins above as `bin\\Release\\` output (DLL, `.pdb`, JSON, `standard\\`, `profiles\\`), one sub-folder per DLL, plus `ApplicationPlugins\\` with the five bundles already filled. Built 2026-11-12 against the AutoCAD.NET 25.1.0 reference package; each DLL references only the AutoCAD 2026 managed assemblies and the .NET 8 shared framework. Not yet loaded in AutoCAD. |\n"
    f"| `LSE-Plugins-ALL-{tag}.zip` | all of the above | one folder per plugin plus `agents\\`, `README.docx`, `STYLE-GUIDE.docx`, `Locations.txt`, `README.md` and `LSE-Plugins-NETLOAD\\` (the prebuilt binaries) | | The same content as the seven zips, in one download. |")
rep("## 3. Build\n\n### 3.1 Unblock and extract",
    "## 3. Build\n\n"
    "### 3.0 No build needed: the NETLOAD-READY zip\n\n"
    f"`LSE-Plugins-NETLOAD-READY-{tag}.zip` already contains the Release output of all five AutoCAD Electrical plugins. Use it unless\n"
    "you have changed the source. Unblock it, extract it to `C:\\` (giving `C:\\LSE-Plugins-NETLOAD`), then go straight to section 4.1:\n\n"
    "```powershell\n"
    f"Unblock-File \"$env:USERPROFILE\\Downloads\\LSE-Plugins-NETLOAD-READY-{tag}.zip\"\n"
    f"Expand-Archive \"$env:USERPROFILE\\Downloads\\LSE-Plugins-NETLOAD-READY-{tag}.zip\" -DestinationPath \"C:\\\"\n"
    "Get-ChildItem \"C:\\LSE-Plugins-NETLOAD\" -Recurse | Unblock-File\n"
    "```\n\n"
    "Inside: one folder per DLL (`LSE.AutoCADElectrical.CatalogTools\\`, `LSE.AutoCADElectrical.ProjectTools\\`, `LSE_Doctor\\`,\n"
    "`LSE.AutoCADElectrical.Librarian\\`, `LSE_Env\\`), each with its JSON / `standard\\` / `profiles\\` beside the DLL; the loader\n"
    "`LSE-NETLOAD-ALL.lsp`; `NETLOAD-README.md`; and `ApplicationPlugins\\` with the five bundles ready to copy (section 4.2).\n"
    "The binaries were built on 2026-11-12 on a machine without AutoCAD, against Autodesk's `AutoCAD.NET` 25.1.0 reference package\n"
    "(the AutoCAD 2026 API, version 25.1.0.0). Each DLL is x64, .NET 8, and references only `acmgd`, `acdbmgd`, `accoremgd` and the .NET 8\n"
    "shared framework that AutoCAD 2026 runs on, so nothing else has to be installed beside the 64-bit Access Database Engine (section 2).\n"
    "The rest of this section is for rebuilding from source.\n\n"
    "### 3.1 Unblock and extract")
rep("Expect a clean build with 0 warnings for Project Tools, Doctor, Librarian and LSE_Env (they were built that way against the\n"
    "reference package).",
    "Expect a clean build with 0 warnings for all five (they were built that way against the\n"
    "reference package; the binaries in the NETLOAD-READY zip are exactly that output).")
rep("(expect 89 passed). Catalog Tools has never been built; it follows the same conventions and the same `.csproj` shape,\n"
    "so expect at most minor fixes. If a build fails",
    "(expect 89 passed). If a build fails")
rep("### 4.1 Quick: NETLOAD\n\n"
    "1. Copy the `bin\\Release\\` folder of the plugin to a share or the workstation (keep the JSON and `standard\\` next to the DLL).\n"
    "2. In AutoCAD Electrical type `NETLOAD`, pick the DLL. If a security dialog appears choose **Load** (or **Always load**).\n"
    "3. Type the plugin `...SETTINGS` command, then its `...DIAG` command (section 5).\n\n"
    "NETLOAD lasts for the session only. Add the DLL folder to `TRUSTEDPATHS` or use a bundle for a permanent load.\n",
    "### 4.1 Quick: NETLOAD\n\n"
    "**All five at once** (from the NETLOAD-READY zip extracted to `C:\\LSE-Plugins-NETLOAD`, section 3.0):\n\n"
    "1. Start AutoCAD Electrical 2026 and drag `C:\\LSE-Plugins-NETLOAD\\LSE-NETLOAD-ALL.lsp` into the drawing area (or `APPLOAD` it).\n"
    "   The command line shows one `NETLOAD` line per plugin and then `LSE plugins: 5 of 5 NETLOADed`.\n"
    "2. If the security dialog appears, choose **Always Load** (it adds the folder to `TRUSTEDPATHS`); or add `C:\\LSE-Plugins-NETLOAD\\...`\n"
    "   to Options > Files > Trusted Locations first and no dialog appears.\n"
    "3. Type `LSELOAD` in any later session to load them again, or add the `.lsp` to the APPLOAD **Startup Suite** so it runs at start-up.\n"
    "   Extracted somewhere other than `C:\\`? Edit the one line `(setq *lse-netload-root* ...)` at the top of the `.lsp`.\n"
    "   `LSE-NETLOAD-ALL.scr` does the same through the `SCRIPT` command (fixed paths).\n"
    "4. Type each plugin's `...SETTINGS` command, then its `...DIAG` command (section 5).\n\n"
    "**One plugin by hand** (NETLOAD-READY folder or your own `bin\\Release\\` output):\n\n"
    "1. Keep the DLL together with its JSON and `standard\\` / `profiles\\` files (copy the whole folder to a share or the workstation).\n"
    "2. In AutoCAD Electrical type `NETLOAD`, pick the DLL. If a security dialog appears choose **Load** (or **Always load**).\n"
    "3. Type the plugin `...SETTINGS` command, then its `...DIAG` command (section 5).\n\n"
    "NETLOAD lasts for the session only. Add the DLL folder to `TRUSTEDPATHS` or use a bundle for a permanent load. A loaded DLL cannot\n"
    "be replaced while AutoCAD Electrical is running: close it completely before copying a newer build over the old one.\n")
rep("Each AutoCAD plugin ships a `bundle\\<Name>.bundle\\PackageContents.xml` (Catalog Tools does not; copy one of the\n"
    "others and change the names and commands, or use an `acad.lsp` entry).",
    "Each AutoCAD plugin ships a `bundle\\<Name>.bundle\\PackageContents.xml` (Catalog Tools since r6). The NETLOAD-READY zip carries\n"
    "all five bundles with `Contents\\` already filled, under `LSE-Plugins-NETLOAD\\ApplicationPlugins\\`: copy them as they are and skip step 2.")
rep("Bundle folder names: `LSE.ProjectTools.bundle`, `LSE_Doctor.bundle`, `LSE.Librarian.bundle`, `LSE_Env.bundle`.",
    "Bundle folder names: `LSE.CatalogTools.bundle`, `LSE.ProjectTools.bundle`, `LSE_Doctor.bundle`, `LSE.Librarian.bundle`, `LSE_Env.bundle`.")
rep("**Catalog Tools**\n- Never compiled. Build first, then `LSECATALOGDIAG`.",
    "**Catalog Tools**\n- First compiled in r6 (0 warnings) but, like the others, never loaded in AutoCAD. Run `LSECATALOGDIAG` first; every catalog call is late-bound ADODB and the Vault calls are reflection, neither run in the host yet.")
rep("| NETLOAD: \"could not load assembly\" or nothing happens | Zip was not unblocked (section 3.1); or the DLL was built for a different AutoCAD major version; or the JSON / `standard\\` files are not next to the DLL. |",
    "| NETLOAD: \"could not load assembly\" or nothing happens | Zip was not unblocked (section 3.0 / 3.1); or the DLL was built for a different AutoCAD major version; or the JSON / `standard\\` files are not next to the DLL. `LSELOAD` printing `MISSING` means the root path at the top of `LSE-NETLOAD-ALL.lsp` is not where you extracted the zip. |\n"
    "| Security dialog on every NETLOAD | Choose **Always Load** once, or add the plugin folder to `TRUSTEDPATHS` (Options > Files > Trusted Locations). |")
open(gp, "w", encoding="utf-8", newline="\n").write(g)
print("guide edits applied:", n, "of 13")

# ---- 3. ALL zip: keep every existing entry except the guide, add the new guide and the NETLOAD folder ------
ap = os.path.join(rel, f"LSE-Plugins-ALL-{tag}.zip")
buf = io.BytesIO()
with zipfile.ZipFile(ap) as old, zipfile.ZipFile(buf, "w", zipfile.ZIP_DEFLATED) as new:
    kept = 0
    for info in old.infolist():
        if info.filename == "LSE-Plugins-BUILD-AND-INSTALL-GUIDE.md" or info.filename.startswith(top + "/"): continue
        new.writestr(info, old.read(info.filename)); kept += 1
    new.write(gp, "LSE-Plugins-BUILD-AND-INSTALL-GUIDE.md")
    for full, arc in walk(pkg): new.write(full, arc)
open(ap, "wb").write(buf.getvalue())
names = zipfile.ZipFile(ap).namelist()
print("ALL", len(names), "entries (", kept, "kept );", sum(1 for x in names if x.startswith(top + "/")), "netload entries; bin/obj in source trees:",
      any(("/bin/" in x or "/obj/" in x) and not x.startswith(top + "/") for x in names))
print()
for f in sorted(os.listdir(rel)):
    fp = os.path.join(rel, f); print(hashlib.sha256(open(fp, "rb").read()).hexdigest()[:16], f"{os.path.getsize(fp):>9}", f)
