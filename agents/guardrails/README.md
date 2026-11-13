# LSE guard rails for the guardian agents

Maintained by DCW. Last reviewed 2026-11-12.

Each plugin has one guardian agent that is the only agent allowed to change it. The agent prompt carries the plugin's invariants (transaction order, file formats, style, what must be asked before doing). This folder carries the part that is enforced by the harness rather than by the prompt: a PreToolUse hook that blocks a tool call before it runs.

## The agents

| Agent | Territory (the only folder it writes) | Extra read-only areas inside it |
|---|---|---|
| `lse-catalog-tools-guardian` | `acade-catalog-tools\` | none |
| `lse-project-tools-guardian` | `acade-project-creator\` | `bundle\` |
| `lse-ecad-doctor-guardian` | `acade-project-doctor\` | `config\standard\`, `bundle\` |
| `lse-env-guardian` | `acade-project-env\` | `bundle\` |
| `lse-librarian-guardian` | `acade-librarian\` | `bundle\` |
| `lse-tofrom-guardian` | `acade-wire-tofrom\` | `bundle\` |
| `lse-footprint-tools-guardian` | `inventor-addin\` | `LSE.Inventor.FootprintTools.addin` |

All seven are in `..\` (this `agents\` folder, the source of truth) and installed in `..\..\.claude\agents\` so Claude Code picks them up in this project. Edit the copy in `agents\` and copy it over; do not let the two drift.

The four older agents (`lse-csharp-dev`, `lse-code-reviewer`, `lse-catalog-data`, `lse-documenter`) are helpers the guardians read as rulebooks. They have no hook and no territory, so prefer a guardian for any change to a plugin.

## What the hook enforces

`lse-guard.ps1` reads the hook JSON on stdin and exits 2 with a `LSE GUARD RAIL:` message to block the call. It is declared in each guardian's frontmatter, so it runs only while that guardian is active:

```yaml
hooks:
  PreToolUse:
    - matcher: "Edit|Write|MultiEdit|NotebookEdit|Bash|PowerShell"
      hooks:
        - type: command
          command: "powershell -NoProfile -ExecutionPolicy Bypass -File agents/guardrails/lse-guard.ps1 -Root acade-librarian -ReadOnly bundle"
```

| Rule | What is blocked |
|---|---|
| W1 | Edit, Write, MultiEdit or NotebookEdit to any path outside `<Root>\` (the session scratchpad is allowed). |
| W2 | Writes into `<Root>\bin`, `<Root>\obj` or any `-ReadOnly` path. |
| W3 | Bash or PowerShell commands containing `rm -r`, `rm -rf`, `Remove-Item -Recurse`, `rmdir`, `rd /s`, `del`, `format X:`, `git push`, `git reset --hard`, `git clean`, `git checkout --`, `mkfs`. |
| W4 | Bash or PowerShell commands with a write verb (`>`, `>>`, `sed -i`, `tee`, `mv`, `cp`, `Copy-Item`, `Move-Item`, `Set-Content`, `Add-Content`, `Out-File`, `New-Item`, `mkdir`, `touch`, zip and unzip, `dotnet new`) that name a project path outside `<Root>\`. |
| W5 | Any write-verb command that names a sibling plugin folder, `reference\`, `releases\`, `agents\`, `AutoCAD_Electrical_2026\`, `README.md`, `README.docx` or `STYLE-GUIDE.docx`. |
| W6 | Hook input that cannot be parsed. The guard fails closed. |

Reads (`cat`, `grep`, `Read`, `Grep`, `Glob`, `dotnet build` inside the territory) are not blocked, so a guardian can study its siblings.

What the hook does not do: it does not inspect Python or other interpreters' file writes beyond a simple `open(` match, it does not follow symlinks or junctions, and it cannot see a path that is built at run time inside a script. The prompt rules cover those cases; the hook is the backstop, not the whole fence.

## Caveats

- Frontmatter hooks in project-level agents only run after the workspace trust dialog for this folder has been accepted. If a guardian edits outside its folder without a `LSE GUARD RAIL` message, check that first.
- Hooks run with the project root as the working directory, which is why the command uses the relative path `agents/guardrails/lse-guard.ps1`. Moving this folder breaks all six hooks.
- The guard uses `CLAUDE_PROJECT_DIR` when set, else the `cwd` field of the hook input, to resolve relative paths.
- Windows PowerShell 5.1 is what `powershell` resolves to on this machine; the script uses nothing newer.

## Tests

```
python -I agents/guardrails/tests/run-tests.py
```

Runs 21 cases against the `acade-librarian` territory (with `config` read-only) and prints PASS or FAIL per case. All 21 passed on 2026-11-12 (the two newest check that `acade-wire-tofrom\` is fenced off from the other guardians, as the two before them do for `acade-project-env\`). Add a case whenever a rule is added or a false block is found.

## Adding a guardian

1. Copy the closest existing guardian file, change `name`, `description`, `color`, the `-Root` (and `-ReadOnly` list) in the hook command, and rewrite the territory table, the invariants and the "stop and ask" list from that plugin's `docs\ARCHITECTURE.md`.
2. Add the folder name to `$protectedTop` in `lse-guard.ps1` so the other guardians cannot write into it.
3. Add a test case, run the tests, copy the agent into `.claude\agents\`, and add a row to the table above and to the root `README.md`.
