import os, zipfile, sys, hashlib
root = sys.argv[1]; tag = "2026-11-12-r7"; old = "2026-11-12-r6"
out = os.path.join(root, "releases", "LSE-Plugins-" + tag); os.makedirs(out, exist_ok=True)
oldrel = os.path.join(root, "releases", "LSE-Plugins-" + old)
g = open(os.path.join(oldrel, "LSE-Plugins-BUILD-AND-INSTALL-GUIDE.md"), encoding="utf-8").read()
n = 0
def rep(a, b):
    global g, n
    if g.count(a) == 1: g = g.replace(a, b); n += 1
    else: print("MISSING or ambiguous:", g.count(a), a[:80])
rep("Release date: 2026-11-12, sixth cut (r6). Package: `LSE-Plugins-2026-11-12-r6` on Google Drive, folder \"LSE AutoCAD Electrical Agents\" > Releases > Current.\nThis supersedes `LSE-Plugins-2026-11-12-r5` (now in Releases > Archive). r6 is an **LSE_Env** cut: the `LSEENV` plan grid no longer",
    "Release date: 2026-11-12, seventh cut (r7). Package: `LSE-Plugins-2026-11-12-r7` on Google Drive, folder \"LSE AutoCAD Electrical Agents\" > Releases > Current.\n"
    "This supersedes `LSE-Plugins-2026-11-12-r6` (built locally on 2026-11-12 and never uploaded) and r5 (now in Releases > Archive). r7 is an\n"
    "**LSE_Doctor** cut answering the user's report that the check boxes in `LSEDOCTOR` did not work: the Fix column is now toggled by the\n"
    "form itself on a click or the space bar (`src\\GridService.cs`) instead of the `DataGridView` edit cycle; rows the doctor can only report on\n"
    "(a line the template lacks, a drawing open in AutoCAD, a missing title block, a wire layer still in use) show the word **note** instead of a\n"
    "dead box, and a click on one explains why on the status line; the scan grid shows **ok** / **review** / **error** the same way. Every\n"
    "LSE_Doctor window has a **Help** button and a tooltip on every control, the new `LSEDOCTORHELP` command shows the whole guide, the status\n"
    "line counts drawings skipped because they are open in AutoCAD, and every window title ends with the build tag `[2026-11-12-r7]`\n"
    "(`LSEDOCTORDIAG` prints it too). A new `acade-project-doctor\\tests\\LSE_Doctor.SmokeTest` runs 43 checks on a real `DataGridView`.\n"
    "**To update LSE_Doctor: close AutoCAD Electrical completely, replace the files in `%APPDATA%\\Autodesk\\ApplicationPlugins\\LSE_Doctor.bundle\\Contents\\`\n"
    "with the new `bin\\Release\\` output (and copy the new `PackageContents.xml`, which lists `LSEDOCTORHELP`), start AutoCAD Electrical, run\n"
    "`LSEDOCTOR` and check the title bar shows `[2026-11-12-r7]`.** r7 also carries the LSE_Env r6 content, which was never uploaded: the `LSEENV` plan grid no longer")
g = g.replace("-2026-11-12-r6.zip", "-2026-11-12-r7.zip")
rep("| `LSE_Doctor-2026-11-12-r7.zip` | AutoCAD Electrical 2026 | `acade-project-doctor\\` | `LSEDOCTOR`, `LSEDOCTORSCAN`, `LSEDOCTORSETTINGS`, `LSEDOCTORDIAG` | Renamed from ProjectDoctor in r2; adds WD_M wire-layer checks and repairs. Compiles Release, 0 warnings. 90-check smoke test against a copy of the real template passed. Not loaded in AutoCAD. |",
    "| `LSE_Doctor-2026-11-12-r7.zip` | AutoCAD Electrical 2026 | `acade-project-doctor\\` | `LSEDOCTOR`, `LSEDOCTORSCAN`, `LSEDOCTORSETTINGS`, `LSEDOCTORDIAG`, `LSEDOCTORHELP` | Renamed from ProjectDoctor in r2; adds WD_M wire-layer checks and repairs. r7: tick boxes handled by the form (click or space bar), `note` / `ok` / `review` / `error` words instead of dead boxes, Help button and tooltips in every window, `LSEDOCTORHELP`, open-drawing count on the status line, build tag `[2026-11-12-r7]` in every title. Compiles Release, 0 warnings. 90-check smoke test against a copy of the real template (2026-11-10) and the 43-check grid/help test (r7) passed. Opened once in AutoCAD Electrical by the user on 2026-11-12 (grid seen; tick boxes reported dead, hence r7); no repair run in the host yet. |")
rep("| LSE_Doctor | `LSEDOCTORSETTINGS` | template `.wdp` (pre-filled to the vault path),",
    "| LSE_Doctor | `LSEDOCTORSETTINGS` (every LSE_Doctor window has a **Help** button and tooltips; `LSEDOCTORHELP` shows the whole guide) | template `.wdp` (pre-filled to the vault path),")
rep("(expect 89 passed).", "(expect 89 passed). For LSE_Doctor: `dotnet build acade-project-doctor\\tests\\LSE_Doctor.SmokeTest\\LSE_Doctor.SmokeTest.csproj -c Release`, then run `acade-project-doctor\\tests\\LSE_Doctor.SmokeTest\\bin\\Release\\LSE_Doctor.SmokeTest.dll` (expect 43 passed).")
rep("   replace the files in `Contents\\`, then start it again. LSE_Env shows its build tag at the end of every window title (for example\n   `[2026-11-12-r6]`) and in `LSEENVDIAG`; an older tag means the previous DLL is still loaded.",
    "   replace the files in `Contents\\`, then start it again. LSE_Env and LSE_Doctor show their build tag at the end of every window title\n   (for example `[2026-11-12-r6]` for LSE_Env and `[2026-11-12-r7]` for LSE_Doctor) and in `LSEENVDIAG` / `LSEDOCTORDIAG`; an older tag means\n   the previous DLL is still loaded.")
# unverified list: first bullet under **LSE_Doctor**
i = g.find("**LSE_Doctor**")
if i < 0: print("MISSING: **LSE_Doctor** heading")
else:
    j = g.find("\n- ", i)
    g = g[:j] + "\n- That the Fix box in the findings grid responds to the first click inside AutoCAD's modal dialog. r7 handles the click itself\n  (`CellClick`, space bar) and paints `note` on report-only rows; proven on a real `DataGridView` outside AutoCAD, not yet inside it.\n  If a box still ignores clicks, first check the title bar shows `[2026-11-12-r7]`." + g[j:]
    n += 1
open(os.path.join(out, "LSE-Plugins-BUILD-AND-INSTALL-GUIDE.md"), "w", encoding="utf-8", newline="\n").write(g)
print("guide edits:", n)
plugins = {"acade-catalog-tools": "LSE.AutoCADElectrical.CatalogTools", "acade-project-creator": "LSE.AutoCADElectrical.ProjectTools", "acade-project-doctor": "LSE_Doctor", "acade-project-env": "LSE_Env", "acade-librarian": "LSE.AutoCADElectrical.Librarian", "inventor-addin": "LSE.Inventor.FootprintTools"}
REUSE = {"inventor-addin"}   # work in progress on disk: ship the r6 (= r5 = r4) content
SKIP = ("bin", "obj", ".claude", ".git")
def add_tree(z, folder):
    for dp, dn, fn in os.walk(os.path.join(root, folder)):
        dn[:] = [d for d in dn if d not in SKIP]
        for f in fn:
            full = os.path.join(dp, f); z.write(full, os.path.relpath(full, root).replace("\\", "/"))
def add_from_zip(z, src_zip, prefix):
    with zipfile.ZipFile(src_zip) as s:
        for name in s.namelist():
            if name.startswith(prefix + "/"): z.writestr(name, s.read(name))
def add_plugin(z, folder, asm):
    if folder in REUSE: add_from_zip(z, os.path.join(oldrel, f"{asm}-{old}.zip"), folder)
    else: add_tree(z, folder)
for folder, asm in plugins.items():
    p = os.path.join(out, f"{asm}-{tag}.zip")
    with zipfile.ZipFile(p, "w", zipfile.ZIP_DEFLATED) as z: add_plugin(z, folder, asm)
    print(os.path.basename(p), len(zipfile.ZipFile(p).namelist()), "entries", "(r6 content reused)" if folder in REUSE else "")
p = os.path.join(out, f"LSE-Plugins-ALL-{tag}.zip")
with zipfile.ZipFile(p, "w", zipfile.ZIP_DEFLATED) as z:
    for folder, asm in plugins.items(): add_plugin(z, folder, asm)
    add_tree(z, "agents")
    for f in ["README.docx", "STYLE-GUIDE.docx", "README.md"]: z.write(os.path.join(root, f), f)
    z.write(os.path.join(root, "reference", "Locations.txt"), "Locations.txt")
    z.write(os.path.join(out, "LSE-Plugins-BUILD-AND-INSTALL-GUIDE.md"), "LSE-Plugins-BUILD-AND-INSTALL-GUIDE.md")
names = zipfile.ZipFile(p).namelist(); print("ALL", len(names), "entries; bin/obj:", any("/bin/" in x or "/obj/" in x for x in names), "; pending docs:", any("docs/pending" in x for x in names))
doc = zipfile.ZipFile(os.path.join(out, f"LSE_Doctor-{tag}.zip"))
print("LSE_Doctor zip has GridService:", "acade-project-doctor/src/GridService.cs" in doc.namelist(), "| HelpText:", "acade-project-doctor/src/HelpText.cs" in doc.namelist(), "| BuildInfo r7:", b"2026-11-12-r7" in doc.read("acade-project-doctor/src/BuildInfo.cs"), "| test:", "acade-project-doctor/tests/LSE_Doctor.SmokeTest/Program.cs" in doc.namelist(), "| LSEDOCTORHELP in bundle:", b"LSEDOCTORHELP" in doc.read("acade-project-doctor/bundle/LSE_Doctor.bundle/PackageContents.xml"))
env = zipfile.ZipFile(os.path.join(out, f"LSE_Env-{tag}.zip"))
print("LSE_Env zip BuildInfo r6:", b"2026-11-12-r6" in env.read("acade-project-env/src/BuildInfo.cs"))
for f in sorted(os.listdir(out)):
    fp = os.path.join(out, f); print(hashlib.sha256(open(fp, "rb").read()).hexdigest()[:16], f)
