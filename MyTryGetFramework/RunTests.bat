@echo off
echo Running Unity Tests...
if "%UNITY_PATH%"=="" set "UNITY_PATH=C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.exe"
set "PROJECT_PATH=%~dp0"
set "RESULTS_DIR=%PROJECT_PATH%TestLogResults"
if not exist "%RESULTS_DIR%" mkdir "%RESULTS_DIR%"
set TIMESTAMP=%date:~0,4%%date:~5,2%%date:~8,2%_%time:~0,2%%time:~3,2%%time:~6,2%
set TIMESTAMP=%TIMESTAMP: =0%
set "RESULTS_FILE=%RESULTS_DIR%\TestResults_%TIMESTAMP%.xml"
set "LOG_FILE=%RESULTS_DIR%\test_log_%TIMESTAMP%.txt"

"%UNITY_PATH%" -runTests -batchmode -quit -projectPath "%PROJECT_PATH%" -testResults "%RESULTS_FILE%" -testPlatform EditMode -logFile "%LOG_FILE%"

echo.
echo Tests completed. Results saved to: %RESULTS_FILE%
echo Log saved to: %LOG_FILE%
echo.
echo Checking for failures...
findstr /C:"failed=\"0\"" "%RESULTS_FILE%" >nul
if %ERRORLEVEL% EQU 0 (
    echo SUCCESS: All tests passed!
) else (
    echo FAILURE: Some tests failed. Check %RESULTS_FILE% for details.
    findstr /C:"result=\"Failed\"" "%RESULTS_FILE%" | findstr /C:"test-case"
)

pause
