
@REM cleans up all the junk Visual Studio generates when building the projects

RMDIR /S /Q "%~dp0FezMultiplayerMod\bin\"
RMDIR /S /Q "%~dp0FezMultiplayerMod\obj\"
RMDIR /S /Q "%~dp0FezMultiplayerMod\publish\"

RMDIR /S /Q "%~dp0FezMultiplayerDedicatedServer\bin\"
RMDIR /S /Q "%~dp0FezMultiplayerDedicatedServer\obj\"
RMDIR /S /Q "%~dp0FezMultiplayerDedicatedServer\publish\"

