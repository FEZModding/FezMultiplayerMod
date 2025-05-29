
set "logDir=%APPDATA%\FEZ\Debug Logs"

@cd /D "%logDir%"
@for /f "delims=" %%i in ('dir /A-D-L /B /OD /T:C') do @(
	@set "LAST=%%i"
)
@if defined LAST @(
	start "" "%LAST%"
) else @(
	echo No files found in %logDir%
	pause;
)
