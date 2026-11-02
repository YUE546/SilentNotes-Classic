# add_bom.ps1 - prepend a UTF-8 BOM to a script if missing (PowerShell 5.1
# reads BOM-less files as ANSI and breaks non-ASCII strings).
param([string]$Path)
$b = [IO.File]::ReadAllBytes($Path)
if ($b.Length -ge 3 -and $b[0] -eq 0xEF -and $b[1] -eq 0xBB -and $b[2] -eq 0xBF) {
  Write-Output 'BOM already there'
} else {
  [IO.File]::WriteAllBytes($Path, ([byte[]](0xEF, 0xBB, 0xBF) + $b))
  Write-Output ('BOM added: ' + $Path)
}
