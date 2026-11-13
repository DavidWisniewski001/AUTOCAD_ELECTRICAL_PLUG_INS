# LSE guard rail hook. PreToolUse hook for the per-plugin guardian agents.
#
# Reads the hook JSON from stdin, decides whether the tool call stays inside the
# agent's territory, and blocks it (exit 2 + message on stderr) when it does not.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File agents/guardrails/lse-guard.ps1 -Root acade-librarian
#
# -Root        the plugin folder the agent owns, relative to the project root.
# -ReadOnly    extra sub-paths (relative to the project root) that may never be
#              written even inside the territory. Comma separated.
#
# Rules enforced here (the agent prompt carries the rest):
#   W1  Edit / Write / NotebookEdit / MultiEdit may only touch files under <Root>\
#       or under the scratchpad (CLAUDE_SCRATCHPAD_DIR or %TEMP%\claude).
#   W2  Nothing under <Root>\bin, <Root>\obj or any -ReadOnly path is written.
#   W3  Bash / PowerShell commands must not contain destructive verbs
#       (rm -r, Remove-Item -Recurse, rmdir, del, format, git push, git reset --hard, git clean).
#   W4  Bash / PowerShell commands that write (>, >>, sed -i, tee, mv, cp, Copy-Item,
#       Move-Item, Set-Content, Out-File, New-Item) must only name paths inside
#       <Root>\ or the scratchpad when they name a path inside the project at all.
#   W5  Any command or write that names a sibling plugin folder, reference\,
#       releases\, AutoCAD_Electrical_2026\, README.docx or STYLE-GUIDE.docx with a
#       write verb is blocked.

param(
    [Parameter(Mandatory = $true)][string]$Root,
    [string]$ReadOnly = ""
)

$ErrorActionPreference = "Stop"

function Deny([string]$why) {
    [Console]::Error.WriteLine("LSE GUARD RAIL: $why")
    exit 2
}

$raw = [Console]::In.ReadToEnd()
if ([string]::IsNullOrWhiteSpace($raw)) { exit 0 }
try { $hook = $raw | ConvertFrom-Json } catch { Deny "hook input could not be parsed; refusing to let an unchecked call through." }

$tool = [string]$hook.tool_name
$projectDir = $env:CLAUDE_PROJECT_DIR
if ([string]::IsNullOrWhiteSpace($projectDir)) { $projectDir = [string]$hook.cwd }
if ([string]::IsNullOrWhiteSpace($projectDir)) { $projectDir = (Get-Location).Path }
$projectDir = [IO.Path]::GetFullPath($projectDir).TrimEnd('\')
$rootDir = [IO.Path]::GetFullPath((Join-Path $projectDir $Root)).TrimEnd('\')

$scratch = $env:CLAUDE_SCRATCHPAD_DIR
if ([string]::IsNullOrWhiteSpace($scratch)) { $scratch = Join-Path $env:TEMP "claude" }
$scratch = [IO.Path]::GetFullPath($scratch).TrimEnd('\')

$neverWrite = @("bin", "obj")
if ($ReadOnly -ne "") { $neverWrite += ($ReadOnly -split ",") | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne "" } }
$neverWriteFull = $neverWrite | ForEach-Object {
    $p = $_
    if ([IO.Path]::IsPathRooted($p)) { [IO.Path]::GetFullPath($p).TrimEnd('\') }
    elseif ($p -like "$Root\*" -or $p -like "$Root/*") { [IO.Path]::GetFullPath((Join-Path $projectDir $p)).TrimEnd('\') }
    else { [IO.Path]::GetFullPath((Join-Path $rootDir $p)).TrimEnd('\') }
}

$protectedTop = @("acade-catalog-tools", "acade-project-creator", "acade-project-doctor", "acade-project-env", "acade-librarian", "acade-wire-tofrom", "inventor-addin",
                  "reference", "releases", "agents", "AutoCAD_Electrical_2026", "README.docx", "STYLE-GUIDE.docx", "README.md") |
                Where-Object { $_ -ne $Root }

function Under([string]$path, [string]$dir) {
    $p = $path.TrimEnd('\')
    return ($p -ieq $dir) -or ($p.StartsWith($dir + "\", [StringComparison]::OrdinalIgnoreCase))
}

function Resolve-Any([string]$p) {
    $p = $p.Trim('"', "'", ' ')
    if ($p -eq "") { return $null }
    $p = $p -replace '/', '\'
    try {
        if ([IO.Path]::IsPathRooted($p)) { return [IO.Path]::GetFullPath($p) }
        return [IO.Path]::GetFullPath((Join-Path $projectDir $p))
    } catch { return $null }
}

function Check-WritePath([string]$path, [string]$what) {
    $full = Resolve-Any $path
    if ($null -eq $full) { return }
    if (Under $full $scratch) { return }
    if (-not (Under $full $rootDir)) {
        Deny "$what '$path' is outside your territory '$Root\'. This agent only changes files under $Root\. Ask the owner of that folder, or the user."
    }
    foreach ($nw in $neverWriteFull) {
        if (Under $full $nw) { Deny "$what '$path' is a read-only area ($nw). It must not be changed by this agent." }
    }
}

switch -Regex ($tool) {
    '^(Edit|Write|MultiEdit|NotebookEdit)$' {
        $fp = [string]$hook.tool_input.file_path
        if ($fp -eq "") { $fp = [string]$hook.tool_input.notebook_path }
        Check-WritePath $fp "Write target"
        exit 0
    }
    '^(Bash|PowerShell)$' {
        $cmd = [string]$hook.tool_input.command
        if ($cmd -eq "") { exit 0 }

        # W3 destructive verbs
        $destructive = @(
            '(^|[\s;&|(])rm\s+(-[a-zA-Z]*r[a-zA-Z]*f?|-[a-zA-Z]*f[a-zA-Z]*r)\b',
            '(^|[\s;&|(])rm\s+-r\b',
            'Remove-Item[^\n;|]*-Recurse',
            '(^|[\s;&|(])rmdir\b',
            '(^|[\s;&|(])rd\s+/s\b',
            '(^|[\s;&|(])del\s+(/[a-z]\s+)*[^\s]',
            '(^|[\s;&|(])format\s+[a-zA-Z]:',
            'git\s+push',
            'git\s+reset\s+--hard',
            'git\s+clean',
            'git\s+checkout\s+--\s',
            '(^|[\s;&|(])mkfs\b'
        )
        foreach ($d in $destructive) {
            if ($cmd -match $d) { Deny "command contains a destructive operation ($($matches[0].Trim())). Guardian agents never delete, reset or push. Explain what you need removed and let the user do it." }
        }

        # W5 protected areas named together with a write verb
        $writeVerb = '(>>?|\bsed\s+-i|\btee\b|\bmv\b|\bcp\b|Copy-Item|Move-Item|Set-Content|Add-Content|Out-File|New-Item|\bpython[^\n|;]*\bopen\(|\bdotnet\s+new\b|\bunzip\b|Expand-Archive|\bzip\b|Compress-Archive|\btouch\b|\bmkdir\b|\bmd\b)'
        if ($cmd -match $writeVerb) {
            foreach ($pt in $protectedTop) {
                $esc = [regex]::Escape($pt)
                if ($cmd -match "(^|[\s""'=\\/])$esc([\\/""'\s]|$)") {
                    Deny "command writes and names a protected area '$pt'. This agent only changes files under $Root\."
                }
            }
            # W4 every path-like token inside the project must be inside the territory
            $tokens = [regex]::Matches($cmd, '"[^"]+"|''[^'']+''|[^\s;&|<>()]+') | ForEach-Object { $_.Value.Trim('"', "'") }
            foreach ($t in $tokens) {
                if ($t -notmatch '[\\/]' -and $t -notmatch '^[A-Za-z]:') { continue }
                if ($t -match '^(https?|git|ssh)://') { continue }
                if ($t -match '^-' ) { continue }
                $full = Resolve-Any $t
                if ($null -eq $full) { continue }
                if (-not (Under $full $projectDir)) { continue }      # outside the project: not ours to police here
                if (Under $full $scratch) { continue }
                # ignore tokens that only look like paths (sed scripts, URLs without scheme): the first
                # segment under the project root must exist on disk for the token to count as a path
                $rel = $full.Substring($projectDir.Length).TrimStart([char]92)
                $first = $rel.Split([char]92)[0]
                if ($first -eq "" -or -not (Test-Path -LiteralPath (Join-Path $projectDir $first))) { continue }
                if (-not (Under $full $rootDir)) {
                    Deny "command writes and names '$t', which is outside your territory '$Root\'."
                }
                foreach ($nw in $neverWriteFull) {
                    if (Under $full $nw) { Deny "command writes into a read-only area ($nw)." }
                }
            }
        }
        exit 0
    }
    default { exit 0 }
}
