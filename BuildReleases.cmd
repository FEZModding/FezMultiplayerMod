
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

@set "packedpath=%~2"
@IF "%packedpath%"=="" @set "packedpath=%projectpath%PackedRelease"

@echo Building %buildConfig% FezMultiplayerMod.dll for %targetframework%
@dotnet publish "%dirclient%FezMultiplayerMod.csproj" -c %buildConfig% -f %targetframework% -o "%pubclient%"
@echo.

@IF /I [%1]==[/zip] IF NOT EXIST "%packedpath%" @mkdir "%packedpath%"

@for %%R in (win-x64 linux-x64 linux-arm64 osx-x64) do @(
    @echo Building %buildConfig% %serverExeName% for %targetframework% on %%R
    @dotnet publish "%dirserve%%serverExeName%.csproj" -c %buildConfig% -f %targetframework% -r %%R -p:AssemblyName=FezMultiplayerDedicatedServer-%%R -o "%pubserve%\%%R"
    @IF /I [%1]==[/zip] @(
        @echo Packing %%R into %packedpath%\%serverExeName%-%%R.zip ...
        @powershell -command "Compress-Archive -Path '%pubserve%\%%R\*' -DestinationPath '%packedpath%\%serverExeName%-%%R.zip' -Force"
    )
    @echo.
)

@echo Builds completed.

