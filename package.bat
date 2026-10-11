@echo off
rem Builds the plugin in Release and packages it as a Thunderstore zip in dist\.
rem Usage: package.bat [--skip-build]
setlocal

set "ROOT=%~dp0"
set "DLL=%ROOT%InstanceLootPlugin\bin\Release\netstandard2.1\InstanceLootPlugin.dll"

if /I not "%~1"=="--skip-build" (
    dotnet build "%ROOT%InstanceLootPlugin\InstanceLootPlugin.csproj" -c Release
    if errorlevel 1 (
        echo Build failed.
        exit /b 1
    )
)

for %%F in ("%ROOT%icon.png" "%DLL%" "%ROOT%manifest.json" "%ROOT%README.md" "%ROOT%CHANGELOG.md") do (
    if not exist "%%~F" (
        echo Missing file: %%~F
        exit /b 1
    )
)

for /f "usebackq delims=" %%N in (`powershell -NoProfile -Command "$m = Get-Content '%ROOT%manifest.json' -Raw | ConvertFrom-Json; $m.name + '-' + $m.version_number"`) do set "PACKAGE=%%N"

if not exist "%ROOT%dist" mkdir "%ROOT%dist"
set "ZIP=%ROOT%dist\%PACKAGE%.zip"
if exist "%ZIP%" del /f /q "%ZIP%"

powershell -NoProfile -Command "Compress-Archive -Path '%ROOT%icon.png','%DLL%','%ROOT%manifest.json','%ROOT%README.md','%ROOT%CHANGELOG.md' -DestinationPath '%ZIP%'"
if errorlevel 1 (
    echo Packaging failed.
    exit /b 1
)

echo Created %ZIP%
endlocal
