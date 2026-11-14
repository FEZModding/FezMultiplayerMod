
@REM This file builds the project and packs the files into zip archives 
@REM that are ready to be uploaded to GitHub releases.
@REM It also spits out the ISO 8601 timestamp that gets put in changelog.txt

@REM shared vars with BuildReleases
@set "projectpath=%~dp0"
@set "dirserve=%projectpath%FezMultiplayerDedicatedServer\"
@set "dirclient=%projectpath%FezMultiplayerMod\"
@set "pubserve=%dirserve%publish"
@set "pubclient=%dirclient%publish"

@set "packedpath=PackedRelease"

@IF [%1]==[/nobuild] GOTO AFTERBUILD
@IF [%1]==[/timeonly] GOTO PRINTBUILDTIME

@echo Publishing project...

@call BuildReleases.cmd

@cd /d %projectpath%

:AFTERBUILD
@echo Packing projects...

@echo Copying Metadata.xml ...
@copy Metadata.xml FezMultiplayerMod\publish\Metadata.xml
@IF NOT EXIST %packedpath% @mkdir %packedpath%

@echo Packing the files that are in %pubserve% into %packedpath%\FezMultiplayerDedicatedServer.zip ...
@powershell -command "Compress-Archive -Path '%pubserve%\*' -DestinationPath '%packedpath%\FezMultiplayerDedicatedServer.zip' -Force"
@echo Packing the files that are in %pubclient% into %packedpath%\FezMultiplayerMod.zip ...
@powershell -command "Compress-Archive -Path '%pubclient%\*' -DestinationPath '%packedpath%\FezMultiplayerMod.zip' -Force"

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

