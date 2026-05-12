@echo off
REM Assumes CDL2v1.exe is on the PATH and CDL2v1.lab.gz is in the current directory.
REM Note that in lab mode --sources are not used, but must be given.
REM Usage: cdl2-lab.cmd [options]
CDL2v1 --sources Quicksort.cdl2 --output-dir . --allow-errors --messages Warning --lab:true --program "c sort" --console -v0 -d0 $*
