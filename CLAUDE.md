# LSE AutoCAD Electrical Agents

## Google Drive release policy (enforced)

Drive folder "LSE AutoCAD Electrical Agents" is laid out like this:

| Folder | Holds | Drive id |
|---|---|---|
| Releases/Current | Exactly one release: the five plugin zips, the ALL zip, the build-and-install guide (.md + Google Doc). Folder description names the version. | 13Zi47pK_tlivdBP6EKWA6ZnxkjE4o2qM |
| Releases/Archive | Older releases, one subfolder per version. Filled only by MOVING files out of Current. | 1C0YTxordbJ8Ww4EFyxuqd4PGUjoirJ_i |
| Reference | Locations.txt, dat MENU.zip, CUSTOM PROJECT TEMPLATE 2.0.zip, LSE-ECAD USER PROFILE.arg | 1hPTslCO9wqa-HNPZzLAkp0l60oo01Su9 |
| Backups | Whole-folder download exports | 1VasA9o_HM7mPzoe-GFOgY6j7jVC9fv1J |

To publish a new release:
1. Ask the user first. Only one session uploads at a time; if another session is uploading, wait for it.
2. List Releases/Current. Create Releases/Archive/<version currently in Current> (the version is in Current's description).
3. Move every file in Current into that archive folder with update_file (parentId). Do not trash, do not copy.
4. Upload the new files into Releases/Current only. Never create a new dated release folder anywhere else.
5. Update Current's description to the new version, then list Current to confirm it holds one release and nothing twice.

A user-level PreToolUse hook (~/.claude/hooks/lse-drive-release-guard.py) blocks: release zips or guides uploaded anywhere but Releases/Current,
release folders created anywhere but Releases/Archive, and any loose zip in the top-level folder. If it blocks you, follow the steps above; do not work around it.
