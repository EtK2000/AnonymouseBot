@echo off

rem this script will always take you first of all to current directory
cd /d "%~dp0"

set pyexe=C:\python27\python.exe
rem check if python installed
if not exist %pyexe% (
    rem now we try with regular python
    where python > nul 2> nul
    if ERRORLEVEL 1 goto :nopython
    set pyexe=python
)

rem print usage if no params
if [%1]==[] goto usage
if [%2]==[] goto usage

set map=%3
if [%3]==[]  set map=%~dp0maps\default_map.map


if not exist %1 goto notexist1
if not exist %2 goto notexist2
set bot1=%~1
set bot2=%~2
rem remove trailing backslash
IF %bot1:~-1%==\ SET bot1=%bot1:~0,-1%
IF %bot2:~-1%==\ SET bot2=%bot2:~0,-1%

rem %pyexe% "%~dp0lib\playgame.py" --loadtime 10000 -e -E -d -O --debug_in_replay --log_dir "%~dp0lib\game_logs"--map_file "%map%" "%bot1%" "%bot2%"

if [%4]==[] %pyexe% "%~dp0lib\playgame.py" --loadtime 10000 -e -E -d -O --debug_in_replay --log_dir "%~dp0lib\game_logs" --map_file "%map%" "%bot1%" "%bot2%"
if not [%4]==[] %pyexe% "%~dp0lib\playgame.py" --loadtime 10000 -e -E -d -O --debug_in_replay --log_dir "%~dp0lib\game_logs" --map_file "%map%" "%bot1%" "%bot2%" "%4"

goto:EOF

:usage
@echo Usage: %0 ^<Player One Bot^> ^<Player Two Bot^> [Map]
exit /B 1

:nopython
@echo ERROR: Python is not installed OR Python not in PATH
exit /B 1

:notexist1
@echo ERROR: Bot #1 ^"%1^" does not exist!
exit /B 1

:notexist2
@echo ERROR: Bot #2 ^"%2^" does not exist!
exit /B 1