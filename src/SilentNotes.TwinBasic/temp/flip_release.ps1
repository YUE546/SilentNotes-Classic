# flip_release.ps1 - release source transform for MainForm.twin.
# tB does not evaluate #If/#Const in class files (both branches always
# compile - verified 2026-11-01), so the release build physically strips
# the gate regions instead: lines between '#If Not CBool(RELEASE_BUILD)
# Then' and its '#End If' are DELETED, '#If CBool(RELEASE_BUILD) Then' /
# '#End If' pairs are unwrapped (body kept). The dev tree keeps the
# markers; On=1 backs up and transforms, On=0 restores the backup.
param([int]$On)
$ErrorActionPreference = 'Stop'
$src = 'D:\vibe coding\silentnotes\src\SilentNotes.TwinBasic\build\shellsrc\Sources\MainForm.twin'
$bak = 'D:\vibe coding\silentnotes\src\SilentNotes.TwinBasic\temp\MainForm.dev.twin'
$enc = [Text.Encoding]::ASCII
$dropOpen = $enc.GetBytes('#If Not CBool(RELEASE_BUILD) Then')
$unwrapOpen = $enc.GetBytes('#If CBool(RELEASE_BUILD) Then')
$endIf = $enc.GetBytes('#End If')
function LineStarts([byte[]]$line, [byte[]]$pat) {
    if ($line.Length -lt $pat.Length) { return $false }
    for ($j = 0; $j -lt $pat.Length; $j++) {
        if ($line[$j] -ne $pat[$j]) { return $false }
    }
    return $true
}
if ($On -eq 1) {
    Copy-Item $src $bak -Force
    $b = [IO.File]::ReadAllBytes($src)
    # split on CRLF at byte level (encoding-agnostic)
    $lines = New-Object System.Collections.Generic.List[byte[]]
    $cur = New-Object System.Collections.Generic.List[byte]
    foreach ($x in $b) {
        if ($x -eq 13) { continue }
        if ($x -eq 10) { $lines.Add($cur.ToArray()); $cur = New-Object System.Collections.Generic.List[byte] }
        else { $cur.Add($x) }
    }
    if ($cur.Count -gt 0) { $lines.Add($cur.ToArray()) }
    $out = New-Object System.Collections.Generic.List[byte[]]
    $mode = 0   # 0 keep, 1 drop, 2 unwrap
    $dropped = 0
    $elsePat = $enc.GetBytes('#Else')
    foreach ($ln in $lines) {
        if ($mode -eq 0) {
            if (LineStarts $ln $dropOpen) { $mode = 1; $dropped++; continue }
            if (LineStarts $ln $unwrapOpen) { $mode = 2; continue }
            $out.Add($ln)
        } else {
            if (LineStarts $ln $elsePat) { throw '#Else inside a transform region - restructure into two #If blocks' }
            if (LineStarts $ln $endIf) { $mode = 0; continue }
            if ($mode -eq 2) { $out.Add($ln) }
        }
    }
    if ($mode -ne 0) { throw 'unterminated #If block during transform' }
    $sep = [byte[]](13, 10)
    $outBytes = New-Object System.Collections.Generic.List[byte]
    $first = $true
    foreach ($ln in $out) {
        if (-not $first) { foreach ($x in $sep) { $outBytes.Add($x) } }
        foreach ($x in $ln) { $outBytes.Add($x) }
        $first = $false
    }
    [IO.File]::WriteAllBytes($src, $outBytes.ToArray())
    Write-Output ('release transform applied: ' + $dropped + ' gate regions stripped')
} else {
    if (Test-Path $bak) {
        Copy-Item $bak $src -Force
        Start-Sleep -Milliseconds 200
        $chk = Select-String -Path $src -Pattern '#If Not CBool' -SimpleMatch
        if ($null -eq $chk) { throw 'restore verification failed - dev markers missing, backup kept' }
        Remove-Item $bak -Force
        Write-Output 'dev source restored from backup (verified)'
    } else {
        Write-Output 'no dev backup to restore (nothing transformed?)'
    }
}
