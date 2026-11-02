# purge_real_sync_20261103.ps1 - MainForm: remove real-sync/check paths,
# gateway flag arms, empty-mode remnants, gate restore rework.
$ErrorActionPreference = 'Stop'
$f = 'D:\vibe coding\silentnotes\src\SilentNotes.TwinBasic\build\shellsrc\Sources\MainForm.twin'
$t = [IO.File]::ReadAllText($f)

# 1) delete SyncCheckRun + SyncRealRun + FindSmokeNoteId (comment .. End Function)
$rx1 = "    ' -synccheck: read-only cloud fingerprint comparison\..*?    ' -syncgate gates:"
$c1 = ([regex]::Matches($t, $rx1, [Text.RegularExpressions.RegexOptions]::Singleline)).Count
$t = [regex]::Replace($t, $rx1, "    ' -syncgate gates:", [Text.RegularExpressions.RegexOptions]::Singleline)
Write-Output ("synccheck/real/smoke block removed " + $c1 + "x")

# 2) ParseArgs: drop -gatewaymode/-puremode/-syncreal/-synccheck/-syncadd/-syncdel arms
$rx2 = "                ElseIf StrComp\(t, ""-gatewaymode"", vbTextCompare\) = 0 Then\r?\n                    m_gatewayMode = True\r?\n                ElseIf StrComp\(t, ""-puremode"", vbTextCompare\) = 0 Then\r?\n                    m_pureMode = True\r?\n"
$c2 = ([regex]::Matches($t, $rx2)).Count
$t = [regex]::Replace($t, $rx2, "")
Write-Output ("gatewaymode/puremode arms removed " + $c2 + "x")

$rx3 = "                ElseIf StrComp\(t, ""-synccheck"", vbTextCompare\) = 0 Then\r?\n                    m_syncCheck = True\r?\n                ElseIf StrComp\(t, ""-syncreal"", vbTextCompare\) = 0 Then\r?\n                    m_syncReal = True\r?\n                ElseIf StrComp\(t, ""-syncadd"", vbTextCompare\) = 0 Then\r?\n                    m_syncAdd = True\r?\n                ElseIf StrComp\(t, ""-syncdel"", vbTextCompare\) = 0 Then\r?\n                    m_syncDel = True\r?\n"
$c3 = ([regex]::Matches($t, $rx3)).Count
$t = [regex]::Replace($t, $rx3, "")
Write-Output ("synccheck/real/add/del arms removed " + $c3 + "x")

# 3) ParseArgs: drop /empty arm
$rx4 = "                ElseIf StrComp\(t, ""/empty"", vbTextCompare\) = 0 Then\r?\n                    m_emptyMode = True\r?\n"
$c4 = ([regex]::Matches($t, $rx4)).Count
$t = [regex]::Replace($t, $rx4, "")
Write-Output ("/empty arm removed " + $c4 + "x")

# 4) SyncLoadSettings: drop the original-ini import (real credentials off-limits now)
$rx5 = "        If Not m_SyncSettings\.Load\(SyncIniPathR\(\)\) Then\r?\n            ' first run: copy the original client's values verbatim \(the DPAPI\r?\n            ' blobs are user-bound and decrypt unchanged\) - read only\r?\n            If m_SyncSettings\.ImportFromOriginal\(OriginalSettingsIni\) Then\r?\n                WLog ""\[Sync\.settings\] imported from original settings \(read-only, not saved yet\)""\r?\n            End If\r?\n        End If"
$c5 = ([regex]::Matches($t, $rx5)).Count
$t = [regex]::Replace($t, $rx5, "        m_SyncSettings.Load SyncIniPathR()")
Write-Output ("original-ini import removed " + $c5 + "x")

# 5) OriginalSettingsIni const
$rx6 = "    Private Const OriginalSettingsIni As String = ""D:\\Documents\\silentnotes\\silentnotes_user_settings_dev\.ini""\r?\n"
$c6 = ([regex]::Matches($t, $rx6)).Count
$t = [regex]::Replace($t, $rx6, "")
Write-Output ("OriginalSettingsIni const removed " + $c6 + "x")

# 6) GateSyncRound: gateway COM instance -> TbPureGw
$rx7 = "        Dim gw2 As Object\r?\n        Set gw2 = CreateObject\(""SilentNotes\.ComGateway\.Gateway""\)"
$c7 = ([regex]::Matches($t, $rx7)).Count
$t = [regex]::Replace($t, $rx7, "        Dim gw2 As TbPureGw`r`n        Set gw2 = New TbPureGw")
Write-Output ("gw2 COM -> TbPureGw replaced " + $c7 + "x")

# 7) gateEnd: restore from the pre-gate snapshot instead of the (deleted) originals
$rx8 = "gateEnd:`r?\n        ' restore the pristine work repo \+ drop the remote \(next run starts\r?\n        ' clean\); the repo state feeds the screenshots afterwards\r?\n        On Error Resume Next\r?\n        wd\.DeleteFile fileUrl\r?\n        If Len\(Dir\$\(variantPath, vbNormal\)\) > 0 Then Kill variantPath\r?\n        m_Repo\.CloseRepo\r?\n        If Len\(Dir\$\(WorkRepo, vbNormal\)\) > 0 Then Kill WorkRepo\r?\n        FileCopy IIf\(m_emptyMode, EmptyRepo, RealRepo\), WorkRepo\r?\n        m_Repo\.OpenExisting WorkRepo"
$c8 = ([regex]::Matches($t, $rx8)).Count
$t = [regex]::Replace($t, $rx8, "gateEnd:`r`n        ' restore the pre-gate snapshot + drop the remote (next run starts`r`n        ' clean); the repo state feeds the screenshots afterwards`r`n        On Error Resume Next`r`n        wd.DeleteFile fileUrl`r`n        If Len(Dir`$(variantPath, vbNormal)) > 0 Then Kill variantPath`r`n        m_Repo.CloseRepo`r`n        If Len(Dir`$(snapPath, vbNormal)) > 0 Then`r`n            Kill WorkRepo`r`n            FileCopy snapPath, WorkRepo`r`n            Kill snapPath`r`n        End If`r`n        m_Repo.OpenExisting WorkRepo")
Write-Output ("gateEnd restore reworked " + $c8 + "x")

# 8) GateSyncRound start: snapshot the work repo before the gates mutate it
$rx9 = "    Private Sub GateSyncRound\(\)\r?\n        On Error GoTo fail\r?\n        WLog ""\[Sync\.gates\] begin"""
$c9 = ([regex]::Matches($t, $rx9)).Count
$t = [regex]::Replace($t, $rx9, "    Private Sub GateSyncRound()`r`n        On Error GoTo fail`r`n        WLog ""[Sync.gates] begin""`r`n        ' snapshot the work repo: the gates mutate it (G5 replaces it with`r`n        ' the id-patched empty fixture) - restored from this copy at gateEnd`r`n        Dim snapPath As String`r`n        snapPath = P3Dir & ""\sync\probe\pre_sync_gate.silentnotes""`r`n        If Len(Dir`$(snapPath, vbNormal)) > 0 Then Kill snapPath`r`n        FileCopy WorkRepo, snapPath")
Write-Output ("gate-start snapshot added " + $c9 + "x")

[IO.File]::WriteAllText($f, $t)
Write-Output 'DONE'
