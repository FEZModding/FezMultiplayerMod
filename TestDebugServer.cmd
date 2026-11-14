
@echo off
for /r "%~dp0FezMultiplayerDedicatedServer\bin\Debug\" %%f in (FezMultiplayerDedicatedServer.exe) do (
    if exist "%%f" (
        start "" /d "%%~dpf" "%%~nxf" %*
        goto :eof
    )
)