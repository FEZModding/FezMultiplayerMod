@echo off
@setlocal EnableDelayedExpansion

@set projectpath=%cd%
@set projectpathbin=%cd%\FezMultiplayerMod\bin\Debug

@set fezpath=
@REM Find the location of the FEZ game folder

@set FezSteamAppId=224760
@REM Get Steam library folders
@FOR /F "tokens=2* skip=2" %%a in ('reg query "HKCU\SOFTWARE\Valve\Steam" /v "SteamPath"') do @set "steamlibs=%%b\steamapps\libraryfolders.vdf"

@if defined steamlibs (
	
	@set curpath=
	
	REM look through steamapps\libraryfolders.vdf to find FEZ (steam app id 224760)
	@FOR /F "tokens=1*" %%L in ('FINDSTR /r /c:"path" /c:"\<%FezSteamAppId%\>" "%steamlibs%"') do @(
		@if %%L=="%FezSteamAppId%" (
			@set fezpath=!curpath!
		)
		@if %%L=="path" (
			@set curpath=%%M
		)
	)
	@REM Note: you must use delayed expansion when reading the values of variables set within an if statement
	if NOT "!fezpath!" == "" @(
		set "fezpath=!fezpath:"=!\steamapps\common\FEZ"
		call :replace_loop
	)
)
@goto done
:replace_loop
@set "temp=%fezpath%"
@set "fezpath=%fezpath:\\=\%"
@if "%temp%"=="%fezpath%" @goto :eof
@goto replace_loop
:done

if not defined fezpath (
	set "fezpath=%ProgramFiles(x86)%\Steam\steamapps\common\FEZ"
)

@REM these set statements don't work in IF blocks
@set "modpath=%fezpath%\Mods\FezMultiplayerMod"
@REM find MONOMODDED_FEZ
@set mmfez=MONOMODDED_FEZ.exe
@FOR /F %%a in ('dir "%fezpath%\MONOMODDED_FEZ*.exe" /B') do @set "mmfez=%%a"

echo "%fezpath%"
echo "%modpath%"

@if EXIST "%fezpath%\" (
	@mkdir "%modpath%"
	@cd /d "%modpath%"
	
	@if ERRORLEVEL 0 (
		@REM 
		copy "%projectpathbin%\*"
		copy "%projectpath%\Metadata.xml"
		
		cd "%fezpath%"
		
		@REM MonoMod.exe FEZ.exe
		@REM echo Exit Code is %errorlevel%
		@if ERRORLEVEL 0 (
			@REM launch MONOMODDED_FEZ
			@REM Note: MONOMODDED_FEZ must be launched from the FEZ game path, as FEZ uses the current working directory to locate game resources
			start %mmfez%
		) ELSE (
			@echo Did you leave FEZ open?
			@pause
		)
	) ELSE (
		@echo Error: Could not locate FEZ game folder.
	)
) ELSE (
	@echo Failed to locate FEZ game directory.
)
cd %projectpath%
