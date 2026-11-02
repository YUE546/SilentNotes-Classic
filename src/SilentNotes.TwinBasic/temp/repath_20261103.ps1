# repath_20261103.ps1 - one-off path rewrite for the 2026-11-03 restructure.
# build\ layout, temp\ scripts+shots+logs, sync\{mock,backups,probe}, exe de-P3.
$ErrorActionPreference = 'Stop'
$root = 'D:\vibe coding\silentnotes\src\SilentNotes.TwinBasic'

$edits = @(
  @{ f = "$root\build\shellsrc\Sources\MainForm.twin"; r = @(
    @{ o = '\SilentNotes.TwinBasic\p3_result.txt'; n = '\SilentNotes.TwinBasic\temp\p3_result.txt' },
    @{ o = '\SilentNotes.TwinBasic\shot.png'; n = '\SilentNotes.TwinBasic\temp\shot.png' },
    @{ o = '\SilentNotes.TwinBasic\sync_backups'; n = '\SilentNotes.TwinBasic\sync\backups' },
    @{ o = 'P3Dir & "\syncprobe\'; n = 'P3Dir & "\sync\probe\' },
    @{ o = 'SilentNotesTbP3_win32.exe'; n = 'SilentNotesTb_win32.exe' },
    @{ o = '=== SilentNotesTbP3 start '; n = '=== SilentNotesTb start ' },
    @{ o = '"SilentNotesTbP3: "'; n = '"SilentNotesTb: "' }
  )},
  @{ f = "$root\build\shellsrc\Sources\TbPureGw.twin"; r = @(
    @{ o = '\SilentNotes.TwinBasic\pure_gw_debug.log'; n = '\SilentNotes.TwinBasic\temp\pure_gw_debug.log' }
  )},
  @{ f = "$root\temp\build.cmd"; r = @(
    @{ o = '\SilentNotes.TwinBasic\tbb2.ps1'; n = '\SilentNotes.TwinBasic\temp\tbb2.ps1' },
    @{ o = '%P3%\flip_release.ps1'; n = '%P3%\temp\flip_release.ps1' },
    @{ o = '%P3%\patch_exe_icon.ps1'; n = '%P3%\temp\patch_exe_icon.ps1' },
    @{ o = '%P3%\shell1\SilentNotesTbP3.twinproj'; n = '%P3%\build\SilentNotesTb.twinproj' },
    @{ o = '"%P3%\shellsrc"'; n = '"%P3%\build\shellsrc"' },
    @{ o = '%P3%\shell1'; n = '%P3%\build' },
    @{ o = 'shell1\Build\SilentNotesTbP3_win32.exe'; n = 'build\Build\SilentNotesTb_win32.exe' }
  )},
  @{ f = "$root\temp\patch_exe_icon.ps1"; r = @(
    @{ o = 'shell1\Build\SilentNotesTbP3_win32.exe'; n = 'build\Build\SilentNotesTb_win32.exe' },
    @{ o = 'shellsrc\Resources\ICON\app.ico'; n = 'build\shellsrc\Resources\ICON\app.ico' },
    @{ o = "'SilentNotesTbP3_win32'"; n = "'SilentNotesTb_win32'" }
  )},
  @{ f = "$root\temp\run_gates.ps1"; r = @(
    @{ o = "shell1\Build\SilentNotesTbP3_win32.exe"; n = "build\Build\SilentNotesTb_win32.exe" },
    @{ o = "'SilentNotesTbP3_win32'"; n = "'SilentNotesTb_win32'" },
    @{ o = 'shotfile=shot_gate_tmp.png'; n = 'shotfile=temp\shot_gate_tmp.png' },
    @{ o = "Join-Path `$p3 'shot_gate_tmp.png'"; n = "Join-Path `$p3 'temp\shot_gate_tmp.png'" },
    @{ o = "Join-Path `$p3 'p3_result.txt'"; n = "Join-Path `$p3 'temp\p3_result.txt'" }
  )},
  @{ f = "$root\temp\run_sync_gates.ps1"; r = @(
    @{ o = "shell1\Build\SilentNotesTbP3_win32.exe"; n = "build\Build\SilentNotesTb_win32.exe" },
    @{ o = '\SilentNotes.TwinBasic\mock\start-webdav.ps1'; n = '\SilentNotes.TwinBasic\sync\mock\start-webdav.ps1' },
    @{ o = "'mock\webdav_root\silentnotes_repository.silentnotes'"; n = "'sync\mock\webdav_root\silentnotes_repository.silentnotes'" },
    @{ o = 'shotfile=shot_sync.png'; n = 'shotfile=temp\shot_sync.png' },
    @{ o = "Join-Path `$basic 'p3_result.txt'"; n = "Join-Path `$basic 'temp\p3_result.txt'" },
    @{ o = '# Starts mock\start-webdav.ps1'; n = '# Starts sync\mock\start-webdav.ps1' }
  )},
  @{ f = "$root\temp\hygiene_check.ps1"; r = @(
    @{ o = 'shell1\Build\SilentNotesTbP3_win32.exe'; n = 'build\Build\SilentNotesTb_win32.exe' }
  )},
  @{ f = "$root\temp\flip_release.ps1"; r = @(
    @{ o = 'shellsrc\Sources\MainForm.twin'; n = 'build\shellsrc\Sources\MainForm.twin' }
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
