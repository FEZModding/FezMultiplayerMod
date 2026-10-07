
@REM This file builds the project and packs the files into zip archives 
@REM that are ready to be uploaded to GitHub releases.
@REM It also spits out the ISO 8601 timestamp that gets put in changelog.txt

@set projectpath=%cd%
@set packedpath=PackedRelease
@set dirserve=%projectpath%\FezMultiplayerDedicatedServer
@set dirclient=%projectpath%\FezMultiplayerMod

@IF [%1]==[/nobuild] GOTO AFTERBUILD
@IF [%1]==[/timeonly] GOTO PRINTBUILDTIME

@echo Publishing project...

@REM this should be where dotnet.exe is located
@REM cd /d "C:\Program Files\dotnet"

@REM the following line should build all the projects in the solution
@REM dotnet publish "%dirserve%\FezMultiplayerDedicatedServer-NET10.csproj" -c Release -f net10.0
@REM dotnet publish "%dirclient%\FezMultiplayerMod-NET10.csproj" -c Release -f net10.0
dotnet publish "%projectpath%\FezMultiplayerMod-NET10.slnx" -c Release -f net10.0 -p:ArtifactsPath="." -p:ArtifactsPivots="" -p:ArtifactsProjectName=""
@echo Builds completed.

cd /d %projectpath%

:AFTERBUILD
@echo Packing projects...

@echo Copying Metadata.xml ...
@copy Metadata.xml FezMultiplayerMod\publish\Metadata.xml
@IF NOT EXIST %packedpath% @mkdir %packedpath%

@rem bin\x86\Debug\net10.0
@echo Packing the files that are in FezMultiplayerDedicatedServer\publish into %packedpath%\FezMultiplayerDedicatedServer.zip ...
@powershell -command "Compress-Archive -Path 'FezMultiplayerDedicatedServer\publish\*' -DestinationPath '%packedpath%\FezMultiplayerDedicatedServer.zip' -Force"
@echo Packing the files that are in FezMultiplayerMod\publish into %packedpath%\FezMultiplayerMod.zip ...
@powershell -command "Compress-Archive -Path 'FezMultiplayerMod\publish\*' -DestinationPath '%packedpath%\FezMultiplayerMod.zip' -Force"

@echo Finished packing projects. zip files are in %cd%\%packedpath%

:PRINTBUILDTIME
@rem Get the filemtime
@set filePath=FezMultiplayerMod\publish\FezMultiplayerMod.dll
@for /f "delims=" %%G in ('powershell -command "(Get-Item '%projectpath%\%filePath%').LastWriteTimeUtc.ToString('yyyy-MM-ddTHH:mm:ssZ')"') do @set "utcTime=%%G"

@REM this timestamp is what goes in changelog.txt
@echo Build time: %utcTime%
@echo Build time: %utcTime%>%packedpath%\buildtime.txt
@echo Done.
@pause
