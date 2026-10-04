# VB10 red-line check: scan all .vb sources under this project for VB11+ syntax
# that the in-box vbc would silently accept. Exit 1 on any violation.
# Usage: powershell -NoProfile -ExecutionPolicy Bypass -File check-vb10-redlines.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$files = Get-ChildItem $root -Recurse -Filter *.vb |
    Where-Object { $_.FullName -notmatch '\\build-output\\|\\obj\\' }

$patterns = @(
    @{ Name = 'Async/Await (VB11)';  Regex = '(?m)^\s*(Async\s+Function|Async\s+Sub|.*\sAwait\s)' },
    @{ Name = 'InterpolatedString (VB14)'; Regex = '\$"' },
    @{ Name = 'NullConditional ?. (VB14)'; Regex = '\?\.' },
    @{ Name = 'nameof (VB14)';       Regex = '(?i)\bnameof\s*\(' }
)

$violations = 0
foreach ($f in $files) {
    $lines = Get-Content $f.FullName -Encoding UTF8
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        if ($line -match "^\s*'" -or $line -match "^\s*REM\s") { continue }   # skip comment-only lines
        foreach ($p in $patterns) {
            if ($line -match $p.Regex) {
                Write-Output ("{0}:{1}: [{2}] {3}" -f $f.Name, ($i + 1), $p.Name, $line.Trim())
                $violations++
            }
        }
    }
}
Write-Output ("Scanned {0} files, red-line violations: {1}" -f $files.Count, $violations)
if ($violations -gt 0) { exit 1 }
