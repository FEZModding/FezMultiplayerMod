
@REM reg query HKCU\SOFTWARE\Valve\Steam /v SteamPath

@set fezpath=

@REM Extrapolate the location of the FEZ game folder
@REM Note: does not check all Steam library locations; see steamapps/libraryfolders.vdf
@FOR /F "tokens=2* skip=2" %%a in ('reg query "HKCU\SOFTWARE\Valve\Steam" /v "SteamPath"') do set fezpath="%%b/steamapps/common/FEZ/Mods/FezMultiplayerMod"

mkdir %fezpath%
cd /d %fezpath%
@rem cd "%ProgramFiles(x86)%/Steam/steamapps/common/FEZ/"

@if ERRORLEVEL 0 (
	@REM 
	copy "D:\Github\FezMultiplayerMod\FezMultiplayerMod\bin\Debug\*"
	copy "D:\Github\FezMultiplayerMod\Metadata.xml"
	
	cd ..\..
	
	@REM MonoMod.exe FEZ.exe
	@echo Exit Code is %errorlevel%
	@if ERRORLEVEL 0 (
		start MONOMODDED_FEZ_HAT.exe
	) ELSE (
		@echo Did you leave FEZ open?
		@pause
	)
) ELSE (
	@echo Error: Could not locate FEZ game folder.
)
