@echo off

REM ----------------------------------------
REM Script to start DetAct-develop.
REM
REM Author: Uwe Schmidt (2022-03-01)
REM ----------------------------------------

set PATH=..\..\DetAct\DetAct
set DOTNET="C:\Program Files\dotnet"

cd %~dp0%PATH%

echo.
echo RUN
echo ----------------------------------------
echo.

%DOTNET%\dotnet watch run