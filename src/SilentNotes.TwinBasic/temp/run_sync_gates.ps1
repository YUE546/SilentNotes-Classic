# run_sync_gates.ps1 - sync integration gates against the local WebDAV mock.
# Starts sync\mock\start-webdav.ps1 as a Start-Job when 8790 is not listening
# (a hidden Start-Process powershell dies silently in some sessions),
# writes the gate settings ini (plain values - TbSyncSettings accepts
# non-DPAPI strings), then runs the exe once with -syncgate (auto-answers
# the conflict dialog with "cloud"). Prints the Sync.* gate lines from
# temp\p3_result.txt.
$ErrorActionPreference = 'Continue'

$basic = 'D:\vibe coding\silentnotes\src\SilentNotes.TwinBasic'
$exe = Join-Path $basic 'build\Build\SilentNotesTbP3_win32.exe'
# SPACE-FREE path: the tB Command$ parser splits on spaces without quote
# handling, so the -syncsettings= value must not contain any
$gateIni = Join-Path $env:TEMP 'sn_sync_gate.ini'

# ---- gate settings (mock server, plain credentials, fixed transfer code)
$ini = "[Synchronization]" + "`r`n" +
    "ServerUrl=http://127.0.0.1:8790" + "`r`n" +
    "Username=sn_test" + "`r`n" +
    "Password=pw_test" + "`r`n" +
    "TransferCode=TestGateCode2A3b" + "`r`n"
[IO.File]::WriteAllText($gateIni, $ini)

# ---- mock server up? (Start-Job spawn; skipped when 8790 already listens)
$listening = $false
try {
    $tcp = New-Object Net.Sockets.TcpClient
    $tcp.Connect('127.0.0.1', 8790)
    $listening = $tcp.Connected
    $tcp.Close()
} catch { }
$job = $null
if (-not $listening) {
    Write-Host 'starting mock webdav server (job)...'
    $job = Start-Job -ScriptBlock { & 'D:\vibe coding\silentnotes\src\SilentNotes.TwinBasic\sync\mock\start-webdav.ps1' }
    $deadline = (Get-Date).AddSeconds(20)
    while (-not $listening -and (Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 500
        try {
            $tcp = New-Object Net.Sockets.TcpClient
            $tcp.Connect('127.0.0.1', 8790)
            $listening = $tcp.Connected
            $tcp.Close()
        } catch { }
    }
    if (-not $listening) {
        Write-Host 'mock webdav server did not come up on 8790'
        Receive-Job $job -Keep -ErrorAction SilentlyContinue
        Stop-Job $job -ErrorAction SilentlyContinue
        Remove-Job $job -Force -ErrorAction SilentlyContinue
        exit 1
    }
}

# clean remote residue from earlier runs (the cloud file name follows the
# RELEASE convention since the real-server interop fix)
Remove-Item (Join-Path $basic 'sync\mock\webdav_root\silentnotes_repository.silentnotes') -Force -ErrorAction SilentlyContinue

# ---- run (self-exits after the startup gates incl. Sync.*)
$argList = @('-screenshot', '-syncgate', "-syncsettings=$gateIni", '-syncchoice=cloud', ('-shotfile=' + (Join-Path $basic 'temp\shot_sync.png')))
$waitMs = 150000
$p = Start-Process -FilePath $exe -ArgumentList $argList -PassThru
if (-not $p.WaitForExit($waitMs)) {
    Stop-Process -Id $p.Id -Force
    Write-Host 'TIMEOUT waiting for the sync-gate run'
    if ($job -ne $null) { Stop-Job $job -ErrorAction SilentlyContinue; Remove-Job $job -Force -ErrorAction SilentlyContinue }
    exit 1
}
Start-Sleep -Milliseconds 500

Write-Host '=== Sync.* gate lines ==='
$pattern = 'Sync\.|STARTUP|Safe\.|Recycle\.empty|Import\.backup|DataDir\.read'
Select-String -Path (Join-Path $basic 'temp\p3_result.txt') -Pattern $pattern | ForEach-Object { $_.Line }
$fails = Select-String -Path (Join-Path $basic 'temp\p3_result.txt') -Pattern 'FAIL '
if ($job -ne $null) { Stop-Job $job -ErrorAction SilentlyContinue; Remove-Job $job -Force -ErrorAction SilentlyContinue }
if ($fails -ne $null) { Write-Host 'HAS FAILURES'; exit 1 }
Write-Host 'ALL GREEN'
exit 0
