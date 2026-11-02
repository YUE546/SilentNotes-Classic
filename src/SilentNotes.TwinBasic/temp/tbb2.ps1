# tbb2.ps1 - twinBASIC CLI build with console error output (p2 variant).
# Same contract as p0\tbb.ps1, but the CDP scrape prefers the IDE's own page
# target and skips external/error pages (google.com etc.) so compile
# diagnostics land on stdout reliably.
# Usage: powershell -File tbb2.ps1 -Project <path.twinproj> [-Arch 32|64] [-Port 9333] [-TimeoutSec 150]
param(
    [Parameter(Mandatory=$true)][string]$Project,
    [string]$Arch = "32",
    [int]$Port = 9333,
    [int]$TimeoutSec = 150
)

$ErrorActionPreference = 'Continue'
$ide = 'D:\tb1027\twinBASIC.exe'
$projFull = [IO.Path]::GetFullPath($Project)
$buildDir = Join-Path (Split-Path $projFull -Parent) 'Build'
$startTick = Get-Date
if (-not (Test-Path $projFull)) { Write-Output '[tbb2] ERROR project not found'; exit 2 }

$env:WEBVIEW2_USER_DATA_FOLDER = Join-Path $env:TEMP ('tbbuild-cli-profile-' + $PID)
$env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS = '--remote-debugging-port=' + $Port
# embedded runtime of the space-free IDE copy (the old fixed runtime in
# archive\p0\comgw was deleted on user order, 2026-10-26)
$wv2 = 'D:\tb1027\WebView2'
if (Test-Path (Join-Path $wv2 'msedgewebview2.exe')) { $env:WEBVIEW2_BROWSER_EXECUTABLE_FOLDER = $wv2 }

Add-Type -Namespace NH2 -Name W2 -MemberDefinition '[DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow); [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);'

$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = $ide
$psi.Arguments = '--buildAndExit' + $Arch + ' "' + $projFull + '"'
$psi.WorkingDirectory = Split-Path $ide -Parent
        $psi.UseShellExecute = $true
$psi.WindowStyle = [System.Diagnostics.ProcessWindowStyle]::Hidden
$proc = [System.Diagnostics.Process]::Start($psi)
Write-Output ('[tbb2] pid ' + $proc.Id + ' win' + $Arch + ' CDP port ' + $Port)

# ---- CDP state ----
$ws = $null
$wsOpen = $false
$recvTask = $null
$recvBuf = New-Object byte[] 2097152
$msgAcc = ''
$nextId = 1
$evalId = 0
$lastDom = ''
$consoleLines = New-Object System.Collections.Generic.List[string]
$diagnostics = New-Object System.Collections.Generic.List[string]
$lastEvalSent = [DateTime]::MinValue

function Send-Cdp([string]$method, [string]$paramsJson) {
    if (-not $script:wsOpen) { return 0 }
    $id = $script:nextId
    $script:nextId = $id + 1
    $msg = '{"id":' + $id + ',"method":"' + $method + '","params":' + $paramsJson + '}'
    $b = [Text.Encoding]::UTF8.GetBytes($msg)
    try { $script:ws.SendAsync([ArraySegment[byte]]::new($b), [System.Net.WebSockets.WebSocketMessageType]::Text, $true, [Threading.CancellationToken]::None).Wait(2000) | Out-Null; return $id } catch { return 0 }
}

function Connect-Cdp {
    try {
        $targets = Invoke-RestMethod -Uri ('http://127.0.0.1:' + $Port + '/json') -TimeoutSec 2
        $pages = @($targets | Where-Object { $_.type -eq 'page' })
        if ($pages.Count -eq 0) { return }
        # prefer the IDE's own page: title says twinBASIC, then any real page
        # that is neither external nor a blank tab (about:blank pages carry
        # no diagnostics and scrape as the google timeout page instead)
        $page = $pages | Where-Object { $_.title -match 'twin' } | Select-Object -First 1
        if ($null -eq $page) { $page = $pages | Where-Object { $_.url -notmatch '^(https?|about):' } | Select-Object -First 1 }
        if ($null -eq $page) { $page = $pages | Where-Object { $_.url -notmatch '^https?://' -and $_.url -ne 'about:blank' } | Select-Object -First 1 }
        if ($null -eq $page) { $page = $pages[0] }
        $s = New-Object System.Net.WebSockets.ClientWebSocket
        $s.ConnectAsync([Uri]$page.webSocketDebuggerUrl, [Threading.CancellationToken]::None).Wait(3000) | Out-Null
        if ($s.State -ne [System.Net.WebSockets.WebSocketState]::Open) { return }
        $script:ws = $s
        $script:wsOpen = $true
        [void](Send-Cdp 'Runtime.enable' '{}')
        $script:recvTask = $script:ws.ReceiveAsync([ArraySegment[byte]]::new($script:recvBuf), [Threading.CancellationToken]::None)
        Write-Output ('[tbb2] CDP attached to ' + $page.url)
    } catch { }
}
function Handle-Message([string]$text) {
    try {
        $j = $text | ConvertFrom-Json
        if ($null -ne $j.id -and $j.id -eq $script:evalId -and $null -ne $j.result.result.value) {
            $script:lastDom = [string]$j.result.result.value
        }
        if ($j.method -eq 'Runtime.consoleAPICalled' -and $j.params.args) {
            $line = ($j.params.args | ForEach-Object { if ($_.value) { $_.value } elseif ($_.description) { $_.description } else { '' } }) -join ' '
            if ($line.Trim().Length -gt 0) { $script:consoleLines.Add($line.Trim()) }
        }
    } catch { }
}
function Parse-Dom([string]$dom) {
    if ($dom.Length -eq 0) { return }
    if ($dom -notmatch 'TB\d{4}') { return }
    $lines = $dom -split "`n"
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $t = $lines[$i].Trim()
        if ($t -match '^(TB\d{4})\s*(.*)$') {
            $code = $Matches[1]
            $msg = $Matches[2]
            $file = ''
            $pos = ''
            $j = $i - 1
            while ($j -ge 0 -and $j -ge $i - 8) {
                $p = $lines[$j].Trim()
                if ($p -like '*.twin' -or $p -like '*.tbform' -or $p -like '*.bas' -or $p -like '*.cls') { $file = $p; break }
                if ($p -match '^TB\d{4}' -or $p -match '^\[\d+,\d+\]$') { $j--; continue }
                break
            }
            if ($i + 1 -lt $lines.Count) { $next = $lines[$i+1].Trim(); if ($next -match '^\[\d+,\d+\]$') { $pos = $next } }
            $entry = ($file + ' ' + $pos).Trim() + ': ' + $code + ' ' + $msg
            if (-not $script:diagnostics.Contains($entry)) { $script:diagnostics.Add($entry) }
        }
    }
}

$sw = [Diagnostics.Stopwatch]::StartNew()
$timedOut = $false
while (-not $proc.HasExited) {
    Start-Sleep -Milliseconds 200
    if (-not $proc.HasExited) {
        $proc.Refresh()
        if ($proc.MainWindowHandle -ne [IntPtr]::Zero -and [NH2.W2]::IsWindowVisible($proc.MainWindowHandle)) {
            [NH2.W2]::ShowWindow($proc.MainWindowHandle, 0) | Out-Null
        }
    }
    if (-not $wsOpen -and $sw.Elapsed.TotalSeconds -gt 3) { Connect-Cdp }
    if ($wsOpen) {
        if (((Get-Date) - $lastEvalSent).TotalMilliseconds -ge 1500) {
            $newId = Send-Cdp 'Runtime.evaluate' '{"expression":"document.body.innerText","returnByValue":true}'
            if ($newId -gt 0) { $evalId = $newId; $lastEvalSent = Get-Date }
        }
        while ($null -ne $recvTask -and $recvTask.IsCompleted -and $wsOpen) {
            try {
                $r = $recvTask.Result
                if ($r.Count -gt 0) {
                    $script:msgAcc += [Text.Encoding]::UTF8.GetString($recvBuf, 0, $r.Count)
                }
                if ($r.EndOfMessage) {
                    $m = $script:msgAcc
                    $script:msgAcc = ''
                    Handle-Message $m
                }
            } catch { $wsOpen = $false }
            if ($wsOpen -and $ws.State -eq [System.Net.WebSockets.WebSocketState]::Open) {
                $recvTask = $ws.ReceiveAsync([ArraySegment[byte]]::new($script:recvBuf), [Threading.CancellationToken]::None)
            } else { $wsOpen = $false; break }
        }
        Parse-Dom $lastDom
    }
    if ($sw.Elapsed.TotalSeconds -gt $TimeoutSec) { $timedOut = $true; break }
}

if ($timedOut) {
    Write-Output ('[tbb2] TIMEOUT ' + $TimeoutSec + 's (build reached an error the IDE waits on) - killing')
    if ($wsOpen) {
        $newId = Send-Cdp 'Runtime.evaluate' '{"expression":"document.body.innerText","returnByValue":true}'
        if ($newId -gt 0) {
            $evalId = $newId
            $deadline = [DateTime]::Now.AddSeconds(4)
            while ([DateTime]::Now -lt $deadline -and $null -ne $recvTask) {
                if ($recvTask.Wait(300)) {
                    try {
                        $r = $recvTask.Result
                        if ($r.Count -gt 0) { $msgAcc += [Text.Encoding]::UTF8.GetString($recvBuf, 0, $r.Count) }
                        if ($r.EndOfMessage) { $m2 = $msgAcc; $msgAcc = ''; Handle-Message $m2 }
                    } catch { break }
                    if ($ws.State -eq [System.Net.WebSockets.WebSocketState]::Open) {
                        $recvTask = $ws.ReceiveAsync([ArraySegment[byte]]::new($recvBuf), [Threading.CancellationToken]::None)
                    } else { break }
                    if ($lastDom.Length -gt 0 -and [DateTime]::Now -gt $lastEvalSent.AddSeconds(1)) { break }
                }
            }
            Parse-Dom $lastDom
        }
    }
    Stop-Process -Id $proc.Id -Force
    Start-Sleep -Milliseconds 500
} else {
    Write-Output ('[tbb2] IDE exited, elapsed ' + [int]$sw.Elapsed.TotalSeconds + 's')
}

$artifact = $null
if (Test-Path $buildDir) {
    $artifact = Get-ChildItem $buildDir -Recurse -File | Where-Object { $_.LastWriteTime -gt $startTick } | Sort-Object LastWriteTime -Descending | Select-Object -First 1
}

if ($diagnostics.Count -gt 0) {
    Write-Output ('[tbb2] ---- diagnostics (' + $diagnostics.Count + ') ----')
    $diagnostics | ForEach-Object { Write-Output ('  ' + $_) }
}
if ($consoleLines.Count -gt 0) {
    Write-Output ('[tbb2] ---- console tail ----')
    $consoleLines | Select-Object -Last 10 | ForEach-Object { Write-Output ('  ' + $_) }
}

if ($null -ne $artifact) {
    Write-Output ('[tbb2] ARTIFACT: ' + $artifact.FullName + ' (' + $artifact.Length + ' bytes)')
    Write-Output '[tbb2] RESULT: BUILD OK'
    exit 0
} else {
    if ($timedOut) { Write-Output '[tbb2] RESULT: BUILD FAILED' } else { Write-Output '[tbb2] RESULT: BUILD FAILED (no artifact)' }
    exit 1
}
