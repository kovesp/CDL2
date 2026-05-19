@echo off
REM Assumes CDL2v1.exe is on the PATH.
REM Compiles the source(s) given in --sources and generates the DB given as CDL2v1.lab.gz in the output directory.
REM Needs work to make it work like an actual compiler (e.g., remove the --parse-only option).
REM Usage: cdl2c.cmd [options]
setlocal
set cmd="%~dp0CDL2v1.exe" --sources Quicksort.cdl2 --output-dir . --allow-errors --messages Info --report-all --lab:false --parse-only -v0 -d0 %*
echo %cmd%
%cmd%
