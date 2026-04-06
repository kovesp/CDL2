@echo off
call "C:\Program Files\Microsoft Visual Studio\18\Community\VC\Auxiliary\Build\vcvarsall.bat" x64
@echo on
REM Maximum optimization
cl.exe /O2 /GL /arch:AVX2 /fp:fast /Ot /Oi /Gy -std:c17 %* /link /STACK:16777216 /LTCG
