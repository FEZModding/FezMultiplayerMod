
@REM This file builds the project and packs the files into zip archives 
@REM that are ready to be uploaded to GitHub releases.
@REM It also spits out the ISO 8601 timestamp that gets put in changelog.txt

@set projectpath=%cd%
@set packedpath=PackedRelease
@set releasebinpathserve=Release\net10.0
@set releasebinpathclient=x86\Release\net10.0

@IF [%1]==[/nobuild] GOTO AFTERBUILD
@IF [%1]==[/timeonly] GOTO PRINTBUILDTIME

@echo Building project...

@REM this should be where devenv.exe is located
cd /d "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE"

@REM devenv "%projectpath%\FezMultiplayerMod.sln" /build Release /project "%projectpath%\FezMultiplayerDedicatedServer\FezMultiplayerDedicatedServer.csproj" /projectconfig Release
@REM devenv "%projectpath%\FezMultiplayerMod.sln" /build Release /project "%projectpath%\FezMultiplayerMod\FezMultiplayerMod.csproj" /projectconfig Release

@REM the following line should build all the projects in the solution
devenv "%projectpath%\FezMultiplayerMod-NET10.slnx" /Build Release
@echo Builds completed.
cd /d %projectpath%

:AFTERBUILD
@echo Packing projects...

@echo Copying Metadata.xml ...
@copy Metadata.xml FezMultiplayerMod\bin\%releasebinpathclient%\Metadata.xml
@IF NOT EXIST %packedpath% @mkdir %packedpath%

@rem bin\x86\Debug\net10.0
@echo Packing the files that are in FezMultiplayerDedicatedServer\bin\%releasebinpathserve% into %packedpath%\FezMultiplayerDedicatedServer.zip ...
@powershell -command "Compress-Archive -Path 'FezMultiplayerDedicatedServer\bin\%releasebinpathserve%\*' -DestinationPath '%packedpath%\FezMultiplayerDedicatedServer.zip' -Force"
@echo Packing the files that are in FezMultiplayerMod\bin\%releasebinpathserve% into %packedpath%\FezMultiplayerMod.zip ...
@powershell -command "Compress-Archive -Path 'FezMultiplayerMod\bin\%releasebinpathclient%\*' -DestinationPath '%packedpath%\FezMultiplayerMod.zip' -Force"

@echo Finished packing projects. zip files are in %cd%\%packedpath%

:PRINTBUILDTIME
@rem Get the filemtime
@set filePath=FezMultiplayerMod\bin\%releasebinpathclient%\FezMultiplayerMod.dll
@for /f "delims=" %%G in ('powershell -command "(Get-Item '%projectpath%\%filePath%').LastWriteTimeUtc.ToString('yyyy-MM-ddTHH:mm:ssZ')"') do @set "utcTime=%%G"

@REM this timestamp is what goes in changelog.txt
@echo Build time: %utcTime%
@echo Build time: %utcTime%>%packedpath%\buildtime.txt
@echo Done.
@pause
