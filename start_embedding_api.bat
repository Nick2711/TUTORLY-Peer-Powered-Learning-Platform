@echo off
echo Starting Python Embedding API...
echo.

REM Check if Python is available
python --version >nul 2>&1
if %errorlevel% neq 0 (
    echo Python is not installed or not in PATH
    echo Please install Python and ensure it's in your PATH
    pause
    exit /b 1
)

REM Check if required packages are installed
echo Checking required packages...
python -c "import fastapi, sentence_transformers, uvicorn" >nul 2>&1
if %errorlevel% neq 0 (
    echo Installing required packages...
    pip install fastapi sentence-transformers uvicorn
)

REM Start the embedding API
echo Starting embedding API on port 8000...
cd /d "%~dp0Shared"
python embedding_api.py

pause
