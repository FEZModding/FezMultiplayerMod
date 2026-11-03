@REM echo should only be one for debugging this script
@echo off
@setlocal EnableDelayedExpansion

@REM Note: this file has the @ symbol at every line because it makes debugging easier, as it lets us silence lines we know work

@goto main

@REM Start of custom subroutines block

:normalize_fezpath
@REM This label is for future expansion
@call :replace_doublebacks
@goto :eof

:replace_doublebacks
@REM Replace all instances of \\ with \
@set "temp=%fezpath%"
@set "fezpath=%fezpath:\\=\%"
@if "%temp%"=="%fezpath%" @goto :eof
@goto replace_doublebacks

@REM End of custom subroutines block

:main

call PackRelease-NET10.cmd -y

@set projectpath=%cd%
@set projectpathbin=%cd%\PackedRelease\FezMultiplayerMod.zip

@set fezpath=
@REM Find the location of the FEZ game folder

@set FezSteamAppId=224760
@REM Get Steam library folders
@FOR /F "tokens=2* skip=2" %%a in ('reg query "HKCU\SOFTWARE\Valve\Steam" /v "SteamPath"') do @set "steamlibs=%%b\steamapps\libraryfolders.vdf"

@if defined steamlibs (
	@echo Searching Steam libraries for FEZ...
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
	if defined fezpath @(
		@set "fezpath=!fezpath:"=!\steamapps\common\FEZ"
		@call :normalize_fezpath
		@echo Found FEZ at "!fezpath!"
	)
) else (
	@REM TODO add support for other locations?
)

@if not defined fezpath (
	@echo Warning: FEZ game path not found. Attempting default path
	@set "fezpath=%ProgramFiles(x86)%\Steam\steamapps\common\FEZ"
)

@REM these set statements don't work in IF blocks
@set "modpath=%fezpath%\Mods\"
@REM find MONOMODDED_FEZ
@set mmfez=FEZ.exe

echo FEZ path: "%fezpath%"
echo Mod path: "%modpath%"

@if EXIST "%fezpath%\" (
	@if EXIST "%modpath%" (
		@REM echo Mod directory already exists
		@REM TODO?
	) ELSE (
		@mkdir "%modpath%"
		echo Created mod directory
	)
	@cd /d "%modpath%"
	
	@if ERRORLEVEL 0 (
		@REM 
		@echo Copying files...
		copy "%projectpathbin%"
		
		cd "%fezpath%"
		
		@if NOT EXIST "%mmfez%" @(
			@echo Failed to find FEZ.exe
			@REM echo Exit Code is %errorlevel%
		)
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
