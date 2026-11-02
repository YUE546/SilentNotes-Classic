# Minimal WebDAV server for local P4 transport gates.
# HttpListener based. Store dir = script dir \webdav_root.
# Basic auth user/pass: sn_test / pw_test. Request log: webdav_log.txt
# Verbs: OPTIONS, PUT, GET, HEAD, DELETE, MKCOL, PROPFIND (Depth 0/1).
# Usage: powershell -File start-webdav.ps1 [port]   (stop: kill PID in webdav.pid)
param([int]$Port = 8790)

$ErrorActionPreference = 'Stop'
$root = Join-Path $PSScriptRoot 'webdav_root'
$logf = Join-Path $PSScriptRoot 'webdav_log.txt'
$pidf = Join-Path $PSScriptRoot 'webdav.pid'
if (-not (Test-Path $root)) { New-Item -ItemType Directory -Path $root | Out-Null }
$PID | Out-File -FilePath $pidf -Encoding ascii
"=== webdav mock start $((Get-Date).ToString('s')) port=$Port root=$root ===" | Out-File $logf -Encoding utf8

$listener = New-Object System.Net.HttpListener
$listener.Prefixes.Add("http://127.0.0.1:$Port/")
$listener.Prefixes.Add("http://localhost:$Port/")
$listener.Start()

function LogReq([string]$s) {
  "$((Get-Date).ToString('HH:mm:ss.fff')) $s" | Out-File $logf -Append -Encoding utf8
}

function CheckAuth($ctx) {
  $hdr = $ctx.Request.Headers['Authorization']
  if ($hdr -ne $null -and $hdr.StartsWith('Basic ')) {
    try {
      $b64 = $hdr.Substring(6)
      $bytes = [Convert]::FromBase64String($b64)
      $userpass = [Text.Encoding]::UTF8.GetString($bytes)
      if ($userpass -eq 'sn_test:pw_test') { return $true }
    } catch { }
  }
  $resp = $ctx.Response
  $resp.StatusCode = 401
  try { $resp.AddHeader('WWW-Authenticate', 'Basic realm="snmock"') } catch { }
  $resp.ContentLength64 = 0
  $resp.Close()
  return $false
}

# map url path -> fs path under root; block traversal
function MapPath([string]$rawUrl, [bool]$isDir) {
  $p = [Uri]::UnescapeDataString($rawUrl)
  $p = $p -replace '/','\'
  $p = $p.TrimStart('\')
  if ($p -eq '') { return $root }
  $full = Join-Path $root $p
  $norm = [IO.Path]::GetFullPath($full)
  if (-not $norm.StartsWith($root)) { return $null }
  return $norm
}

function XMLEscape([string]$s) {
  $t = $s -replace '&','&amp;'
  $t = $t -replace '<','&lt;'
  $t = $t -replace '>','&gt;'
  return $t
}

function PropEntry([string]$href, [string]$name, [bool]$isDir, [long]$len, [DateTime]$mt) {
  $sb = New-Object Text.StringBuilder
  [void]$sb.Append('<D:response><D:href>')
  [void]$sb.Append((XMLEscape $href))
  [void]$sb.Append('</D:href><D:propstat><D:prop>')
  [void]$sb.Append('<D:displayname>' + (XMLEscape $name) + '</D:displayname>')
  [void]$sb.Append('<D:getlastmodified>' + $mt.ToUniversalTime().ToString('R') + '</D:getlastmodified>')
  if ($isDir) {
    [void]$sb.Append('<D:resourcetype><D:collection/></D:resourcetype>')
  } else {
    [void]$sb.Append('<D:getcontentlength>' + $len + '</D:getcontentlength>')
    [void]$sb.Append('<D:resourcetype/>')
  }
  [void]$sb.Append('</D:prop><D:status>HTTP/1.1 200 OK</D:status></D:propstat></D:response>')
  return $sb.ToString()
}

function SendProps($ctx, [string]$body) {
  $bytes = [Text.Encoding]::UTF8.GetBytes('<?xml version="1.0" encoding="utf-8"?><D:multistatus xmlns:D="DAV:">' + $body + '</D:multistatus>')
  $resp = $ctx.Response
  $resp.StatusCode = 207
  $resp.ContentType = 'text/xml; charset="utf-8"'
  $resp.ContentLength64 = $bytes.Length
  $resp.OutputStream.Write($bytes, 0, $bytes.Length)
  $resp.Close()
}

while ($listener.IsListening) {
  $ctx = $listener.GetContext()
  try {
    $req = $ctx.Request
    $verb = $req.HttpMethod
    $rawUrl = $req.Url.AbsolutePath
    LogReq "$verb $rawUrl Depth=$($req.Headers['Depth'])"

    if ($verb -eq 'OPTIONS') {
      $resp = $ctx.Response
      $resp.Headers['DAV'] = '1, 2'
      $resp.Headers['Allow'] = 'OPTIONS, GET, HEAD, PUT, DELETE, MKCOL, PROPFIND'
      $resp.StatusCode = 200
      $resp.ContentLength64 = 0
      $resp.Close()
      continue
    }

    if (-not (CheckAuth $ctx)) { LogReq "$verb $rawUrl -> 401"; continue }

    if ($verb -eq 'PUT') {
      $fs = MapPath $rawUrl $false
      if ($fs -eq $null -or (Test-Path $fs -PathType Container)) {
        $ctx.Response.StatusCode = 409; $ctx.Response.ContentLength64 = 0; $ctx.Response.Close(); continue
      }
      $existed = Test-Path $fs -PathType Leaf
      $dir = Split-Path $fs -Parent
      if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
      $ms = New-Object IO.MemoryStream
      $req.InputStream.CopyTo($ms)
      [IO.File]::WriteAllBytes($fs, $ms.ToArray())
      $ctx.Response.StatusCode = $(if ($existed) { 204 } else { 201 })
      $ctx.Response.ContentLength64 = 0
      $ctx.Response.Close()
      LogReq "PUT $rawUrl -> $(if ($existed){204}else{201}) bytes=$($ms.Length)"
      continue
    }

    if ($verb -eq 'GET' -or $verb -eq 'HEAD') {
      $fs = MapPath $rawUrl $false
      if ($fs -eq $null -or -not (Test-Path $fs -PathType Leaf)) {
        $ctx.Response.StatusCode = 404; $ctx.Response.ContentLength64 = 0; $ctx.Response.Close(); LogReq "$verb $rawUrl -> 404"; continue
      }
      $bytes = [IO.File]::ReadAllBytes($fs)
      $ctx.Response.StatusCode = 200
      $ctx.Response.ContentType = 'application/octet-stream'
      $ctx.Response.ContentLength64 = $bytes.Length
      if ($verb -eq 'GET') { $ctx.Response.OutputStream.Write($bytes, 0, $bytes.Length) }
      $ctx.Response.Close()
      LogReq "$verb $rawUrl -> 200 bytes=$($bytes.Length)"
      continue
    }

    if ($verb -eq 'DELETE') {
      $fs = MapPath $rawUrl $false
      if ($fs -eq $null -or -not (Test-Path $fs)) {
        $ctx.Response.StatusCode = 404; $ctx.Response.ContentLength64 = 0; $ctx.Response.Close(); LogReq "DELETE $rawUrl -> 404"; continue
      }
      Remove-Item -LiteralPath $fs -Recurse -Force
      $ctx.Response.StatusCode = 204; $ctx.Response.ContentLength64 = 0; $ctx.Response.Close()
      LogReq "DELETE $rawUrl -> 204"
      continue
    }

    if ($verb -eq 'MKCOL') {
      $fs = MapPath $rawUrl $true
      if ($fs -eq $null) { $ctx.Response.StatusCode = 400; $ctx.Response.ContentLength64 = 0; $ctx.Response.Close(); continue }
      if (Test-Path $fs) {
        $ctx.Response.StatusCode = 405; $ctx.Response.ContentLength64 = 0; $ctx.Response.Close(); LogReq "MKCOL $rawUrl -> 405 exists"; continue
      }
      $parent = Split-Path $fs -Parent
      if (-not (Test-Path $parent)) {
        $ctx.Response.StatusCode = 409; $ctx.Response.ContentLength64 = 0; $ctx.Response.Close(); LogReq "MKCOL $rawUrl -> 409 no parent"; continue
      }
      New-Item -ItemType Directory -Path $fs | Out-Null
      $ctx.Response.StatusCode = 201; $ctx.Response.ContentLength64 = 0; $ctx.Response.Close()
      LogReq "MKCOL $rawUrl -> 201"
      continue
    }

    if ($verb -eq 'PROPFIND') {
      $fs = MapPath $rawUrl $false
      if ($fs -eq $null -or -not (Test-Path $fs)) {
        $ctx.Response.StatusCode = 404; $ctx.Response.ContentLength64 = 0; $ctx.Response.Close(); LogReq "PROPFIND $rawUrl -> 404"; continue
      }
      $depth = $req.Headers['Depth']
      $body = ''
      $isDir = Test-Path $fs -PathType Container
      $name = Split-Path $fs -Leaf
      $body += PropEntry $rawUrl $name $isDir $(if ($isDir) { 0 } else { (Get-Item $fs).Length }) $(Get-Item $fs).LastWriteTime
      if (($depth -ne '0') -and $isDir) {
        $children = Get-ChildItem -LiteralPath $fs | Sort-Object Name
        foreach ($c in $children) {
          $href = $rawUrl.TrimEnd('/') + '/' + $c.Name
          $body += PropEntry $href $c.Name ($c.PSIsContainer) $(if ($c.PSIsContainer) { 0 } else { $c.Length }) $c.LastWriteTime
        }
      }
      SendProps $ctx $body
      LogReq "PROPFIND $rawUrl depth=$depth -> 207 entries=$([regex]::Matches($body,'<D:response>').Count)"
      continue
    }

    $ctx.Response.StatusCode = 405
    $ctx.Response.ContentLength64 = 0
    $ctx.Response.Close()
    LogReq "$verb $rawUrl -> 405 unsupported"
  } catch {
    try {
      $ctx.Response.StatusCode = 500
      $ctx.Response.ContentLength64 = 0
      $ctx.Response.Close()
    } catch { }
    LogReq "ERR $($_.Exception.Message)"
  }
}
