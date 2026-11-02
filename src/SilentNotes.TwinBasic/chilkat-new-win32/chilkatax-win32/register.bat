
@echo off
echo.Current User is '%USERNAME%'

cd %~dp0

REM Check if regsvr32.exe exists in SysWow64, otherwise default to System32
if exist "%systemroot%\syswow64\regsvr32.exe" (
    set "REGSVR=%systemroot%\syswow64\regsvr32.exe"
) else (
    set "REGSVR=%systemroot%\system32\regsvr32.exe"
)

set "filemask=ChilkatAx*.dll"
for %%A in (%filemask%) do "%REGSVR%" %%A || GOTO:EOF

ECHO.&PAUSE&GOTO:EOF
