@echo off

REM ----------------------------------------
REM Script to start DetAct-publish.
REM 
REM Author: Uwe Schmidt (2022-03-01)
REM ----------------------------------------

set PATH=..\DetAct\DetAct\bin\Release\net6.0\publish\

cd %~dp0%PATH%

echo.
echo RUN
echo ----------------------------------------
echo.

start "DetAct" DetAct.exe