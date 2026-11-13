"""Builds config/LseNamingStandard.json from the LSE ELECTRICAL PARTS CATALOG MANAGER workbook.

Usage (run with -I so nothing is imported from the data folders):
    python -I extract_naming_standard.py "<workbook.xlsx>" "<output.json>" ["<default_cat.mdb>"]

Reads the "PROPOSED ... NAMING & DESCRIPTION STANDARD" block at the top of every "LSE ..." tab:
  - Part number pattern  [Category 2][Mfr code 3][Type 2][Sequence 7]
  - Category codes (column C of the Category row), Type codes (column C of the Type row)
  - Manufacturer | Code table (to the right of the segment table; its column varies per tab)
If a catalog MDB is given, it is opened READ-ONLY and the MANUFACTURER value most often seen with
each 3-letter code (USER1 chars 3-5) is recorded as acadeManufacturer, plus family-table hints per
2-digit category. The workbook and MDB are never modified.
"""
import sys, re, json, datetime, collections
import openpyxl

PN_RE = re.compile(r"\d{2}[A-Z]{3}[A-Z]{2}\d{7}")
CODE_RE = re.compile(r"[A-Z]{3}(\s*/\s*[A-Z]{3})*")


def main():
    wb_path, out_path = sys.argv[1], sys.argv[2]
    mdb = sys.argv[3] if len(sys.argv) > 3 else None
    wb = openpyxl.load_workbook(wb_path, read_only=True, data_only=True)
    tabs, mfr = [], {}
    for ws in wb.worksheets:
        if not ws.title.startswith("LSE "):
            continue
        rows = []
        for i, row in enumerate(ws.iter_rows(values_only=True)):
            if i >= 40:
                break
            rows.append(["" if v is None else str(v).strip() for v in row] + [""] * 8)
        tab = {"name": ws.title, "categoryCodes": [], "types": {}, "manufacturers": {}, "descriptionPattern": ""}
        in_seg, name_col, code_col = False, 5, 6
        for r in rows:
            if r[0].lower().startswith("description") and r[1]:
                tab["descriptionPattern"] = r[1]
            if r[0] == "Part # Segment":
                in_seg = True
                if "Manufacturer" in r[1:] and "Code" in r[1:]:
                    name_col, code_col = r.index("Manufacturer", 1), r.index("Code", 1)
                continue
            if r[0] == "Description Field":
                in_seg = False
            if not in_seg:
                continue
            if r[0] == "Category":
                tab["categoryCodes"] = sorted(set(re.findall(r"\b(\d{2})\b", r[2])))
            if r[0] == "Type":
                for m in re.finditer(r"\b([A-Z]{2})\s*=\s*([^;]+)", r[2]):
                    tab["types"][m.group(1)] = m.group(2).strip()
            name, code = r[name_col], r[code_col]
            if code and name and name != "Manufacturer" and CODE_RE.fullmatch(code):
                names, codes = re.split(r"\s*/\s*", name), re.split(r"\s*/\s*", code)
                pairs = list(zip(names, codes)) if len(names) == len(codes) else [(name, codes[0])]
                for n, c in pairs:
                    tab["manufacturers"][c] = n
                    mfr.setdefault(c, {"name": n, "acadeManufacturer": ""})
        if tab["types"]:
            tabs.append(tab)

    hints = {}
    if mdb:
        import pyodbc
        conn = pyodbc.connect(r"Driver={Microsoft Access Driver (*.mdb, *.accdb)};Dbq=%s;ReadOnly=1;Exclusive=0;" % mdb, readonly=True, autocommit=True)
        cur = conn.cursor()
        code2m, cat2fam = collections.defaultdict(collections.Counter), collections.defaultdict(collections.Counter)
        for t in [x.table_name for x in cur.tables(tableType="TABLE") if not x.table_name.startswith(("MSys", "_"))]:
            try:
                data = cur.execute(f"SELECT USER1,MANUFACTURER FROM [{t}] WHERE USER1 IS NOT NULL").fetchall()
            except Exception:
                continue
            for u, m in data:
                u = (u or "").strip().upper()
                if PN_RE.fullmatch(u):
                    if m and m.strip():
                        code2m[u[2:5]][m.strip().upper()] += 1
                    cat2fam[u[:2]][t] += 1
        conn.close()
        for code, counter in code2m.items():
            top = counter.most_common(1)[0][0]
            mfr.setdefault(code, {"name": top.title(), "acadeManufacturer": ""})
            mfr[code]["acadeManufacturer"] = top
        hints = {k: [t for t, _ in v.most_common(3)] for k, v in sorted(cat2fam.items())}

    out = {
        "source": f"{wb_path.replace(chr(92), '/').split('/')[-1]} (generated {datetime.date.today().isoformat()} by tools/extract_naming_standard.py)",
        "format": {"categoryChars": 2, "manufacturerChars": 3, "typeChars": 2, "sequenceChars": 7},
        "tabs": tabs,
        "manufacturers": dict(sorted(mfr.items())),
        "familyHints": hints,
    }
    with open(out_path, "w", encoding="utf-8") as f:
        json.dump(out, f, indent=1, ensure_ascii=False)
    print(f"tabs={len(tabs)} manufacturers={len(mfr)} familyHints={len(hints)} -> {out_path}")


if __name__ == "__main__":
    main()
