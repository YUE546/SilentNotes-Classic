
@echo off
echo.Current User is '%USERNAME%'

cd %~dp0

set "filemask=ChilkatAx*.dll"
for %%A in (%filemask%) do \windows\syswow64\regsvr32 /u %%A || GOTO:EOF

ECHO.&PAUSE&GOTO:EOF

