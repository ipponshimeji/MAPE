@echo off

call ClearOutput.bat

setlocal

del /S "*.user"
rmdir /S /Q ".vs"

endlocal
