
@REM This file builds the project 


@set "serverExeName=FezMultiplayerDedicatedServer"
@set "targetframework=net10.0"
@set "buildConfig=Release"

@REM shared vars with PackRelease
@set "projectpath=%~dp0"
@set "dirserve=%projectpath%%serverExeName%\"
@set "dirclient=%projectpath%FezMultiplayerMod\"
@set "pubserve=%dirserve%publish"
@set "pubclient=%dirclient%publish"

@echo Building %buildConfig% FezMultiplayerMod.dll for %targetframework%
@dotnet publish "%dirclient%FezMultiplayerMod-NET10.csproj" -c %buildConfig% -f %targetframework% -o "%pubclient%"
@echo.
@for %%R in (win-x64 linux-x64 osx-x64) do @(
    @echo Building %buildConfig% %serverExeName% for %targetframework% on %%R
    @dotnet publish "%dirserve%%serverExeName%-NET10.csproj" -c %buildConfig% -f %targetframework% -r %%R -p:AssemblyName=FezMultiplayerDedicatedServer-%%R -o "%pubserve%"
    @echo.
)

@echo Builds completed.

