@echo off
echo Testing ChatBot Configuration...
echo.

echo 1. Checking if embedding_api.py exists...
if exist embedding_api.py (
    echo ✓ embedding_api.py found
) else (
    echo ✗ embedding_api.py NOT FOUND
    pause
    exit /b 1
)

echo.
echo 2. Checking if Python is available...
python --version >nul 2>&1
if %errorlevel% equ 0 (
    echo ✓ Python is installed
    python --version
) else (
    echo ✗ Python NOT FOUND - Please install Python
    pause
    exit /b 1
)

echo.
echo 3. Checking Python dependencies...
pip show fastapi >nul 2>&1
if %errorlevel% equ 0 (
    echo ✓ FastAPI is installed
) else (
    echo ✗ FastAPI NOT INSTALLED - Run: pip install -r requirements.txt
)

pip show uvicorn >nul 2>&1
if %errorlevel% equ 0 (
    echo ✓ Uvicorn is installed
) else (
    echo ✗ Uvicorn NOT INSTALLED - Run: pip install -r requirements.txt
)

echo.
echo 4. Testing embedding API startup...
echo Starting embedding API for 5 seconds...
start /B python embedding_api.py
timeout /t 3 /nobreak >nul

echo Testing API endpoint...
curl -s http://localhost:8000/health >nul 2>&1
if %errorlevel% equ 0 (
    echo ✓ Embedding API is responding
) else (
    echo ✗ Embedding API is NOT responding
)

echo.
echo 5. Configuration Check:
echo - Check Server/appsettings.json for:
echo   * HuggingFace:ApiKey
echo   * AzureBlobStorage:ConnectionString  
echo   * AzureBlobStorage:BlobContainerName
echo.

echo Test completed!
pause
