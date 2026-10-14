@echo off
setlocal

set CONFIGURATION=%1
if "%CONFIGURATION%"=="" set CONFIGURATION=Debug

rem Restore NuGet packages
dotnet restore

rem Build the solution
dotnet build --configuration %CONFIGURATION% --no-restore

rem Run tests
dotnet test --configuration %CONFIGURATION% --no-build

echo Build and test completed.
endlocal
