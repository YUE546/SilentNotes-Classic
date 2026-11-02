# pure_backend_20261103.ps1 - TbRepo: drop the ComGateway backend, TbPureGw only.
$ErrorActionPreference = 'Stop'
$f = 'D:\vibe coding\silentnotes\src\SilentNotes.TwinBasic\build\shellsrc\Sources\TbRepo.twin'
$t = [IO.File]::ReadAllText($f)

function Rep([string]$o, [string]$n) {
  $script:c = ([regex]::Matches($script:t, [regex]::Escape($o))).Count
  if ($script:c -eq 0) { Write-Output ("NO MATCH  : " + $o.Substring(0, [Math]::Min(60, $o.Length))) }
  $script:t = $script:t.Replace($o, $n)
  Write-Output ("replaced " + $script:c + "x")
}

# header comments
Rep "' TbRepo - ComGateway wrapper + in-memory note table for the P2 shell." `
    "' TbRepo - pure-backend (TbPureGw) wrapper + in-memory note table for the P2 shell."
Rep "' Data safety: the repository is a WORKING COPY (OpenCopy copies src first,`n' overwriting the previous copy) - the original under D:\Documents is never`n' touched. AddNew persists through the gateway (AddNote + SaveRepository)." `
    "' Data safety: the debug repo is a fictional test repository opened in place`n' (OpenExisting) - no production data is involved. AddNew persists through the`n' backend (AddNote + SaveRepository)."

# field block
Rep "    ' Route B dual backend: False (default) = pure tB model stack (TbPureGw`n    ' shim, no .NET); True = the legacy ComGateway COM path (-gatewaymode).`n    Public GatewayMode As Boolean`n    Private m_pure As TbPureGw`n`n    Private m_gw As Object`n    Private m_open As Boolean" `
    "    ' Single backend: the pure tB model stack (TbPureGw shim over Chilkat).`n    Private m_pure As TbPureGw`n    Private m_open As Boolean"

# AttachCrypto
Rep "    ' share the shell's Chilkat/crypto pair with the pure backend (route B;`n    ' no-op for the gateway backend - the gateway has its own Core crypto)`n    Public Sub AttachCrypto(ck As TbChilkat, crypto As TbCrypto)`n        If GatewayMode Then Exit Sub`n        If m_pure Is Nothing Then Set m_pure = New TbPureGw" `
    "    ' share the shell's Chilkat/crypto pair with the pure backend`n    Public Sub AttachCrypto(ck As TbChilkat, crypto As TbCrypto)`n        If m_pure Is Nothing Then Set m_pure = New TbPureGw"

# routers header comment
Rep "    ' early-bound dual-backend dispatch (late binding on the pure shim was" `
    "    ' early-bound dispatch (late binding on the pure shim was"

# BeOpenRepository
Rep "    Private Function BeOpenRepository(ByVal path As String) As String`n        If GatewayMode Then`n            Set m_gw = CreateObject(""SilentNotes.ComGateway.Gateway"")`n            BeOpenRepository = m_gw.OpenRepository(path)`n        Else`n            If m_pure Is Nothing Then Set m_pure = New TbPureGw`n            Set m_gw = m_pure`n            BeOpenRepository = m_pure.OpenRepository(path)`n        End If`n    End Function" `
    "    Private Function BeOpenRepository(ByVal path As String) As String`n        If m_pure Is Nothing Then Set m_pure = New TbPureGw`n        BeOpenRepository = m_pure.OpenRepository(path)`n    End Function"

# uniform single-line routers: If GatewayMode Then BeX = m_gw.M(args) Else BeX = m_pure.M(args)
$rx = 'If GatewayMode Then (Be\w+) = m_gw\.(\w+\()([^)]*\)) Else \1 = m_pure\.\2\3'
$c = ([regex]::Matches($t, $rx)).Count
$t = [regex]::Replace($t, $rx, '$1 = m_pure.$2$3')
Write-Output ("regex single-line routers replaced " + $c + "x")

# BeIsSafeOpen (multi-line)
Rep "    Private Function BeIsSafeOpen(ByVal safeId As String) As Boolean`n        If GatewayMode Then`n            BeIsSafeOpen = (m_gw.IsSafeOpen(safeId) = ""1"")`n        Else`n            BeIsSafeOpen = m_pure.IsSafeOpen(safeId) = ""1""`n        End If`n    End Function" `
    "    Private Function BeIsSafeOpen(ByVal safeId As String) As Boolean`n        BeIsSafeOpen = m_pure.IsSafeOpen(safeId) = ""1""`n    End Function"

# OpenCopy: delete the whole function
Rep "    ' Copies srcPath over workPath (original stays untouched), opens the copy`n    ' through the gateway and loads every note into memory.`n    Public Function OpenCopy(ByVal srcPath As String, ByVal workPath As String) As Boolean`n        On Error GoTo fail`n        Dim t0 As Long`n        If Len(Dir`$(workPath, vbNormal)) > 0 Then Kill workPath`n        t0 = GetTickCountR()`n        FileCopy srcPath, workPath`n        CopyMs = GetTickCountR() - t0`n`n        t0 = GetTickCountR()`n        Dim r As String`n        If GatewayMode Then`n            Set m_gw = CreateObject(""SilentNotes.ComGateway.Gateway"")`n            r = m_gw.OpenRepository(workPath)`n        Else`n            If m_pure Is Nothing Then Set m_pure = New TbPureGw`n            Set m_gw = m_pure`n            r = m_pure.OpenRepository(workPath)`n        End If`n        OpenMs = GetTickCountR() - t0`n        If Left`$(r, 4) = ""ERR:"" Then`n            m_lastErr = ""OpenRepository: "" & r`n            Exit Function`n        End If`n        m_open = True`n        OpenCopy = LoadAll()`n        Exit Function`nfail:`n        m_lastErr = ""OpenCopy err="" & Err.Number & "" "" & Err.Description`n    End Function`n`n" `
    ""

# CopyMs field
Rep "    Public CopyMs As Long           ' OpenCopy: FileCopy`n" ""

# CloseRepo
Rep "        If m_open Then`n            If GatewayMode Then`n                m_gw.CloseRepository`n            ElseIf Not m_pure Is Nothing Then`n                m_pure.CloseRepository`n            End If`n            m_open = False`n        End If" `
    "        If m_open Then`n            If Not m_pure Is Nothing Then`n                m_pure.CloseRepository`n            End If`n            m_open = False`n        End If"

# OpenExisting
Rep "        If GatewayMode Then`n            Set m_gw = CreateObject(""SilentNotes.ComGateway.Gateway"")`n        Else`n            If m_pure Is Nothing Then Set m_pure = New TbPureGw`n            Set m_gw = m_pure`n        End If`n        Dim r As String`n        r = m_gw.OpenRepository(path)" `
    "        If m_pure Is Nothing Then Set m_pure = New TbPureGw`n        Dim t0 As Long`n        t0 = GetTickCountR()`n        Dim r As String`n        r = m_pure.OpenRepository(path)`n        OpenMs = GetTickCountR() - t0"

[IO.File]::WriteAllText($f, $t)
Write-Output 'DONE'
