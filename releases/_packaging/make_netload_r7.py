import os, sys, zipfile, hashlib, io
root = sys.argv[1]; tag = "2026-11-12-r7"; old = "2026-11-12-r6"
rel = os.path.join(root, "releases", "LSE-Plugins-" + tag)
oldrel = os.path.join(root, "releases", "LSE-Plugins-" + old)
top = "LSE-Plugins-NETLOAD"
doc_out = os.path.join(root, "acade-project-doctor", "bin", "Release")
manifest = os.path.join(root, "acade-project-doctor", "bundle", "LSE_Doctor.bundle", "PackageContents.xml")
assert os.path.isfile(os.path.join(doc_out, "LSE_Doctor.dll")) and os.path.isfile(manifest)
dll = open(os.path.join(doc_out, "LSE_Doctor.dll"), "rb").read()
assert tag.encode("utf-16-le") in dll, "LSE_Doctor.dll does not carry the r7 build tag"
assert b"LSEDOCTORHELP" in open(manifest, "rb").read()

def doctor_files():
    for dp, dn, fn in os.walk(doc_out):
        for f in sorted(fn):
            full = os.path.join(dp, f)
            yield full, os.path.relpath(full, doc_out).replace("\\", "/")

skip_prefixes = (f"{top}/LSE_Doctor/", f"{top}/ApplicationPlugins/LSE_Doctor.bundle/")
src = zipfile.ZipFile(os.path.join(oldrel, f"LSE-Plugins-NETLOAD-READY-{old}.zip"))
entries = []  # (arcname, bytes)
for info in src.infolist():
    name = info.filename
    if name.startswith(skip_prefixes):
        continue
    data = src.read(name)
    if name == f"{top}/NETLOAD-README.md":
        t = data.decode("utf-8")
        t = t.replace(f"NETLOAD-ready binaries ({old})", f"NETLOAD-ready binaries ({tag})")
        t = t.replace(f"LSE-Plugins-NETLOAD-READY-{old}.zip", f"LSE-Plugins-NETLOAD-READY-{tag}.zip")
        a = "| `LSE_Doctor\\` | `LSE_Doctor.dll` + `LseDoctorRules.json` + `standard\\` | `LSEDOCTOR`, `LSEDOCTORSCAN`, `LSEDOCTORSETTINGS`, `LSEDOCTORDIAG` |"
        assert a in t
        t = t.replace(a, "| `LSE_Doctor\\` | `LSE_Doctor.dll` + `LseDoctorRules.json` + `standard\\` | `LSEDOCTOR`, `LSEDOCTORSCAN`, `LSEDOCTORSETTINGS`, `LSEDOCTORDIAG`, `LSEDOCTORHELP` (build `2026-11-12-r7`, shown in every window title) |")
        a = "## Status\n\n"
        assert a in t
        t = t.replace(a, "## Status\n\nLSE_Doctor is build `2026-11-12-r7` (tick boxes handled by the form, `note` rows, Help buttons, `LSEDOCTORHELP`); LSE_Env is build `2026-11-12-r6`; both print their tag at the end of every window title. Close AutoCAD Electrical completely before replacing a DLL that was already loaded.\n\n")
        data = t.encode("utf-8")
    elif name == f"{top}/LSE-NETLOAD-ALL.lsp":
        t = data.decode("utf-8")
        a = "; LSEDOCTOR LSEDOCTORSCAN LSEDOCTORSETTINGS LSEDOCTORDIAG\n"
        if a in t: t = t.replace(a, "; LSEDOCTOR LSEDOCTORSCAN LSEDOCTORSETTINGS LSEDOCTORDIAG LSEDOCTORHELP\n")
        else: print("note: LSP doctor comment line not found; left as is")
        data = t.encode("utf-8")
    entries.append((name, data))
for full, relp in doctor_files():
    b = open(full, "rb").read()
    entries.append((f"{top}/LSE_Doctor/{relp}", b))
    entries.append((f"{top}/ApplicationPlugins/LSE_Doctor.bundle/Contents/{relp}", b))
entries.append((f"{top}/ApplicationPlugins/LSE_Doctor.bundle/PackageContents.xml", open(manifest, "rb").read()))
entries.sort(key=lambda e: e[0])
zp = os.path.join(rel, f"LSE-Plugins-NETLOAD-READY-{tag}.zip")
with zipfile.ZipFile(zp, "w", zipfile.ZIP_DEFLATED) as z:
    for name, data in entries: z.writestr(name, data)
names = [n for n, _ in entries]
print(os.path.basename(zp), len(names), "entries,", sum(1 for n in names if n.endswith(".dll")), "dlls; doctor dll r7 in both places:",
      all(tag.encode("utf-16-le") in dict(entries)[n] for n in (f"{top}/LSE_Doctor/LSE_Doctor.dll", f"{top}/ApplicationPlugins/LSE_Doctor.bundle/Contents/LSE_Doctor.dll")))

# ---- guide header fixes (r6 is in Current now, so it goes to Archive) --------------------------------
gp = os.path.join(rel, "LSE-Plugins-BUILD-AND-INSTALL-GUIDE.md")
g = open(gp, encoding="utf-8").read(); n = 0
def rep(a, b):
    global g, n
    c = g.count(a)
    if c != 1: print(f"MISSING/AMBIGUOUS ({c}x):", a[:90]); return
    g = g.replace(a, b); n += 1
rep("This supersedes `LSE-Plugins-2026-11-12-r6` (built locally on 2026-11-12 and never uploaded) and r5 (now in Releases > Archive). r7 is an",
    "This supersedes `LSE-Plugins-2026-11-12-r6` (now in Releases > Archive). r7 is an")
rep("`LSEDOCTOR` and check the title bar shows `[2026-11-12-r7]`.** r7 also carries the LSE_Env r6 content, which was never uploaded: the `LSEENV` plan grid no longer",
    "`LSEDOCTOR` and check the title bar shows `[2026-11-12-r7]`.** LSE_Env is unchanged since r6 (build tag `[2026-11-12-r6]`): its `LSEENV` plan grid no longer")
rep("**r6 also ships ready-to-load binaries:**", "**r7 also ships ready-to-load binaries (first added in r6; the `LSE_Doctor\\` folder and bundle inside are the r7 build, the rest is the r6 build):**")
open(gp, "w", encoding="utf-8", newline="\n").write(g)
print("guide header edits:", n, "of 3")

# ---- ALL zip: existing entries minus the guide, plus the new guide and the NETLOAD folder --------------
ap = os.path.join(rel, f"LSE-Plugins-ALL-{tag}.zip")
buf = io.BytesIO()
with zipfile.ZipFile(ap) as oldz, zipfile.ZipFile(buf, "w", zipfile.ZIP_DEFLATED) as new:
    kept = 0
    for info in oldz.infolist():
        if info.filename == "LSE-Plugins-BUILD-AND-INSTALL-GUIDE.md" or info.filename.startswith(top + "/"): continue
        new.writestr(info, oldz.read(info.filename)); kept += 1
    new.write(gp, "LSE-Plugins-BUILD-AND-INSTALL-GUIDE.md")
    for name, data in entries: new.writestr(name, data)
open(ap, "wb").write(buf.getvalue())
an = zipfile.ZipFile(ap).namelist()
print("ALL", len(an), "entries (", kept, "kept );", sum(1 for x in an if x.startswith(top + "/")), "netload entries; stray bin/obj:",
      any(("/bin/" in x or "/obj/" in x) and not x.startswith(top + "/") for x in an))
print()
for f in sorted(os.listdir(rel)):
    fp = os.path.join(rel, f); print(hashlib.sha256(open(fp, "rb").read()).hexdigest()[:16], f"{os.path.getsize(fp):>9}", f)
