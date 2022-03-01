@echo off

REM ----------------------------------------
REM Script to start the test-client.
REM 
REM Author: Uwe Schmidt (2022-03-01)
REM ----------------------------------------

set PATH=..\DetAct\TestClient\bin\Release\net6.0-windows
cd %~dp0%PATH%

start TestClient.exe