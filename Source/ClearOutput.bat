@echo off

setlocal
set RMDIR=rmdir /S /Q

%RMDIR% "__Bin"
%RMDIR% "Core\bin"
%RMDIR% "Core\obj"
%RMDIR% "Windows\bin"
%RMDIR% "Windows\obj"
%RMDIR% "CLI\bin"
%RMDIR% "CLI\obj"
%RMDIR% "TestResults"

endlocal
