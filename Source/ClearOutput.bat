@echo off

setlocal
set RMDIR=rmdir /S /Q

%RMDIR% "__Bin"
%RMDIR% "Core\bin"
%RMDIR% "Core\obj"
%RMDIR% "Windows\Windows\bin"
%RMDIR% "Windows\Windows\obj"
%RMDIR% "Windows\CLI\bin"
%RMDIR% "Windows\CLI\obj"
%RMDIR% "TestResults"

endlocal
