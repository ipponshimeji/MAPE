@echo off

setlocal
set RMDIR=rmdir /S /Q

%RMDIR% "__Bin"
%RMDIR% "__Obj"
%RMDIR% "__Package"
%RMDIR% "Core\bin"
%RMDIR% "Core\obj"
%RMDIR% "Windows\Windows\bin"
%RMDIR% "Windows\Windows\obj"
%RMDIR% "Windows\CLI\bin"
%RMDIR% "Windows\CLI\obj"
%RMDIR% "Windows\GUI\bin"
%RMDIR% "Windows\GUI\obj"
%RMDIR% "_Packaging\bin"
%RMDIR% "_Packaging\obj"
%RMDIR% "TestResults"

endlocal
