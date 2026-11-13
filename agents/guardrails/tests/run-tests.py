"""Test matrix for lse-guard.ps1. Run from the project root:

    python -I agents/guardrails/tests/run-tests.py

Each case feeds a PreToolUse hook payload to the guard for the acade-librarian
territory (with config\\ read-only) and checks the exit code: 0 = allowed, 2 = blocked.
"""
import json, os, subprocess, sys

P = os.getcwd()
GUARD = os.path.join("agents", "guardrails", "lse-guard.ps1")

cases = [
    ("allow edit own",         {"tool_name": "Edit",  "tool_input": {"file_path": P + r"\acade-librarian\src\Models.cs"}}, 0),
    ("block edit sibling",     {"tool_name": "Edit",  "tool_input": {"file_path": P + r"\acade-project-doctor\src\Models.cs"}}, 2),
    ("block write bin",        {"tool_name": "Write", "tool_input": {"file_path": P + r"\acade-librarian\bin\x.txt"}}, 2),
    ("block write read-only",  {"tool_name": "Write", "tool_input": {"file_path": P + r"\acade-librarian\config\LseLibraryStandard.json"}}, 2),
    ("allow write docs",       {"tool_name": "Write", "tool_input": {"file_path": P + r"\acade-librarian\docs\ARCHITECTURE.md"}}, 0),
    ("block write root README",{"tool_name": "Write", "tool_input": {"file_path": P + r"\README.md"}}, 2),
    ("block rm -rf",           {"tool_name": "Bash",  "tool_input": {"command": "rm -rf acade-librarian/bin"}}, 2),
    ("block sed sibling",      {"tool_name": "Bash",  "tool_input": {"command": "sed -i s/a/b/ acade-project-creator/src/Plugin.cs"}}, 2),
    ("block sed env plugin",  {"tool_name": "Bash",  "tool_input": {"command": "sed -i s/a/b/ acade-project-env/src/Plugin.cs"}}, 2),
    ("block edit env plugin", {"tool_name": "Edit",  "tool_input": {"file_path": P + r"\acade-project-env\src\Models.cs"}}, 2),
    ("block sed tofrom plugin",{"tool_name": "Bash",  "tool_input": {"command": "sed -i s/a/b/ acade-wire-tofrom/src/Commands.cs"}}, 2),
    ("block edit tofrom plugin",{"tool_name": "Edit", "tool_input": {"file_path": P + r"\acade-wire-tofrom\src\Models.cs"}}, 2),
    ("allow sed own",          {"tool_name": "Bash",  "tool_input": {"command": "sed -i s/a/b/ acade-librarian/src/Plugin.cs"}}, 0),
    ("allow cat sibling",      {"tool_name": "Bash",  "tool_input": {"command": "cat acade-project-creator/src/Plugin.cs"}}, 0),
    ("allow build own",        {"tool_name": "Bash",  "tool_input": {"command": "cd acade-librarian && dotnet build -c Release > build.log"}}, 0),
    ("block echo reference",   {"tool_name": "Bash",  "tool_input": {"command": "echo hi > reference/x.txt"}}, 2),
    ("block git push",         {"tool_name": "Bash",  "tool_input": {"command": "git push origin main"}}, 2),
    ("block Remove-Item",      {"tool_name": "PowerShell", "tool_input": {"command": "Remove-Item -Recurse -Force acade-librarian\\obj"}}, 2),
    ("block abs sibling",      {"tool_name": "Bash",  "tool_input": {"command": 'cp x.cs "' + P + r'\inventor-addin\src\x.cs"'}}, 2),
    ("allow Grep",             {"tool_name": "Grep",  "tool_input": {"pattern": "x"}}, 0),
    ("block bad json",         None, 2),
]

fails = 0
for name, inp, expected in cases:
    raw = json.dumps({**inp, "cwd": P, "hook_event_name": "PreToolUse"}) if inp else "{not json"
    r = subprocess.run(
        ["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", GUARD, "-Root", "acade-librarian", "-ReadOnly", "config"],
        input=raw, capture_output=True, text=True, env={**os.environ, "CLAUDE_PROJECT_DIR": P})
    ok = r.returncode == expected
    fails += not ok
    print("PASS" if ok else "FAIL", name, r.returncode, r.stderr.strip()[:100])
print("failures:", fails)
sys.exit(1 if fails else 0)
