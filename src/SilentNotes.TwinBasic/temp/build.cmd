@echo off
REM build.cmd - SilentNotesTbP3: mkdir -> import text tree -> headless build -> icon.
REM Text tree lives in build\shellsrc\, packed project lands in build\.
REM (the exe name comes from the twinproj-internal project.name - the container
REM  is length-prefixed, byte-renaming it corrupts the file: keep the P3 name)
REM CLOSE any running SilentNotesTbP3_win32.exe first (it locks the output).
REM The exe is an interactive UI app - launch it manually:
REM   build\Build\SilentNotesTbP3_win32.exe                 normal mode (real repo copy)
REM   build\Build\SilentNotesTbP3_win32.exe -screenshot -shotfile=x.png [-theme=graphite]
REM Result log: temp\p3_result.txt   Shots: temp\*.png
REM "build.cmd release" compiles the gate machinery OUT (RELEASE_BUILD=1,
REM source restored from the backup after the build); default = dev build.
setlocal
set REL=0
if /i "%~1"=="release" set REL=1
set P3=D:\vibe coding\silentnotes\src\SilentNotes.TwinBasic
set TBEXE=D:\tb1027\bin\twinBASIC_win32.exe
set BUILDPS=D:\vibe coding\silentnotes\src\SilentNotes.TwinBasic\temp\tbb2.ps1

echo [0/4] mode: dev
if "%REL%"=="1" echo [0/4] mode: RELEASE (gate machinery compiled out)
if "%REL%"=="1" powershell -NoProfile -ExecutionPolicy Bypass -File "%P3%\temp\flip_release.ps1" 1
if errorlevel 1 goto :failed

echo [1/4] mkdir build
if not exist "%P3%\build" mkdir "%P3%\build"
if errorlevel 1 goto :failed

echo [2/4] import text tree -^> SilentNotesTb.twinproj
"%TBEXE%" import "%P3%\build\SilentNotesTb.twinproj" "%P3%\build\shellsrc" --overwrite
if errorlevel 1 goto :failed

echo [3/4] headless build (tbb2.ps1, parallel-safe, prints diagnostics on failure)
powershell -NoProfile -ExecutionPolicy Bypass -File "%BUILDPS%" -Project "%P3%\build\SilentNotesTb.twinproj" -TimeoutSec 150
if errorlevel 1 goto :failed

echo [4/4] patch exe icon (app.ico frames - tB leaves its default; see RESULTS)
powershell -NoProfile -ExecutionPolicy Bypass -File "%P3%\temp\patch_exe_icon.ps1"
if errorlevel 1 goto :failed

if "%REL%"=="1" powershell -NoProfile -ExecutionPolicy Bypass -File "%P3%\temp\flip_release.ps1" 0
echo.
echo BUILD DONE. Artifact: %P3%\build\Build\SilentNotesTbP3_win32.exe
exit /b 0

:failed
if "%REL%"=="1" powershell -NoProfile -ExecutionPolicy Bypass -File "%P3%\temp\flip_release.ps1" 0
echo.
echo BUILD FAILED (see output above)
exit /b 1
