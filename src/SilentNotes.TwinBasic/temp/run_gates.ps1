# run_gates.ps1 - one gate run + verdict.
# Usage: run_gates.ps1
$ErrorActionPreference = 'Continue'
$p3 = 'D:\vibe coding\silentnotes\src\SilentNotes.TwinBasic'
$exe = Join-Path $p3 'build\Build\SilentNotesTbP3_win32.exe'

$running = Get-Process -Name 'SilentNotesTbP3_win32' -ErrorAction SilentlyContinue
if ($running -ne $null) { Stop-Process -Id $running.Id -Force; Start-Sleep -Milliseconds 500 }

Remove-Item (Join-Path $p3 'ui_settings.ini') -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $p3 'temp\p3_result.txt') -Force -ErrorAction SilentlyContinue

$args_ = @('-screenshot', ('-shotfile=' + (Join-Path $p3 'temp\shot_gate_tmp.png')))
$p = Start-Process -FilePath $exe -ArgumentList $args_ -PassThru
if (-not $p.WaitForExit(150000)) {
  Stop-Process -Id $p.Id -Force
  Write-Host 'TIMEOUT'
  exit 1
}
Start-Sleep -Milliseconds 500
Remove-Item (Join-Path $p3 'temp\shot_gate_tmp.png') -Force -ErrorAction SilentlyContinue

$res = Join-Path $p3 'temp\p3_result.txt'
Select-String -Path $res -Pattern 'STARTUP|FAIL' | ForEach-Object { $_.Line }
Write-Output '--- new gates ---'
Select-String -Path $res -Pattern 'Cue.notquery|Status.wired|Icon.embedded|Icon.form|Theme.editor_css' | ForEach-Object { $_.Line }
$fails = Select-String -Path $res -Pattern 'FAIL '
if ($fails -ne $null) { Write-Host 'HAS FAILURES'; exit 1 }
Write-Host 'ALL GREEN'
exit 0

