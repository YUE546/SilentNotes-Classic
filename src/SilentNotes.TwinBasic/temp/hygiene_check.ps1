# hygiene_check.ps1 - P6 round hygiene: originals untouched, registry intact.
$ErrorActionPreference = 'Continue'
$src = 'D:\Documents\silentnotes\silentnotes_repository_dev.silentnotes'
if (Test-Path $src) {
  $f = Get-Item $src
  Write-Output ("original repo: len=" + $f.Length + " mtime=" + $f.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss'))
} else {
  Write-Output "original repo MISSING"
}
$crash = 'D:\Documents\silentnotes\crash.log'
if (Test-Path $crash) {
  $f2 = Get-Item $crash
  Write-Output ("crash.log: len=" + $f2.Length + " mtime=" + $f2.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss'))
} else {
  Write-Output "crash.log: absent (untouched)"
}
$reg = Get-ItemProperty -Path 'HKCU:\Software\SilentNotes' -Name DataDirectory -ErrorAction SilentlyContinue
if ($reg -ne $null) { Write-Output ("registry DataDirectory=" + $reg.DataDirectory) } else { Write-Output "registry DataDirectory: not set" }
$work = Get-Item 'D:\vibe coding\silentnotes\src\SilentNotes.TwinBasic\test_repo.silentnotes'
Write-Output ("test repo: len=" + $work.Length + " mtime=" + $work.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss'))
$exe = Get-Item 'D:\vibe coding\silentnotes\src\SilentNotes.TwinBasic\build\Build\SilentNotesTbP3_win32.exe'
Write-Output ("exe: len=" + $exe.Length + " mtime=" + $exe.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss'))
