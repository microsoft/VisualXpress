@ECHO OFF
SET SCRIPT_FOLDER=%~dp0
SET SCRIPT_FOLDER=%SCRIPT_FOLDER:~,-1%
CALL %SCRIPT_FOLDER%\Publish.bat RC
