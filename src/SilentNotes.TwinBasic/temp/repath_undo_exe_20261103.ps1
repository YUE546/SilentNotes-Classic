# repath_undo_exe_20261103.ps1 - revert exe-name strings to the P3 artifact
# name (the twinproj container cannot be byte-renamed internally).
$ErrorActionPreference = 'Stop'
$root = 'D:\vibe coding\silentnotes\src\SilentNotes.TwinBasic'

$edits = @(
  @{ f = "$root\build\shellsrc\Sources\MainForm.twin"; r = @(
    @{ o = 'SilentNotesTb_win32.exe'; n = 'SilentNotesTbP3_win32.exe' },
    @{ o = '=== SilentNotesTb start '; n = '=== SilentNotesTbP3 start ' },
    @{ o = '"SilentNotesTb: "'; n = '"SilentNotesTbP3: "' }
  )},
  @{ f = "$root\temp\patch_exe_icon.ps1"; r = @(
    @{ o = 'SilentNotesTb_win32.exe'; n = 'SilentNotesTbP3_win32.exe' },
    @{ o = "'SilentNotesTb_win32'"; n = "'SilentNotesTbP3_win32'" }
  )},
  @{ f = "$root\temp\run_gates.ps1"; r = @(
    @{ o = 'SilentNotesTb_win32.exe'; n = 'SilentNotesTbP3_win32.exe' },
    @{ o = "'SilentNotesTb_win32'"; n = "'SilentNotesTbP3_win32'" }
  )},
  @{ f = "$root\temp\run_sync_gates.ps1"; r = @(
    @{ o = 'SilentNotesTb_win32.exe'; n = 'SilentNotesTbP3_win32.exe' }
  )},
  @{ f = "$root\temp\hygiene_check.ps1"; r = @(
    @{ o = 'SilentNotesTb_win32.exe'; n = 'SilentNotesTbP3_win32.exe' }
  )},
  @{ f = "$root\temp\build.cmd"; r = @(
    @{ o = 'SilentNotesTb_win32.exe'; n = 'SilentNotesTbP3_win32.exe' },
    @{ o = 'REM build.cmd - SilentNotesTb:'; n = 'REM build.cmd - SilentNotesTbP3:' }
  )}
)

foreach ($e in $edits) {
  if (-not (Test-Path $e.f)) { Write-Output ("MISSING FILE: " + $e.f); continue }
  $t = [IO.File]::ReadAllText($e.f)
  foreach ($p in $e.r) {
    $c = ([regex]::Matches($t, [regex]::Escape($p.o))).Count
    if ($c -eq 0) { Write-Output ("NO MATCH  : " + $p.o + "  in  " + [IO.Path]::GetFileName($e.f)) }
    $t = $t.Replace($p.o, $p.n)
    Write-Output ("replaced " + $c + "x : " + $p.o)
  }
  [IO.File]::WriteAllText($e.f, $t)
}
Write-Output 'DONE'
