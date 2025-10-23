@echo off
echo Starting Tutorly Embedding API automatically...
echo.

REM Check if Python is available
python --version >nul 2>&1
if %errorlevel% neq 0 (
    echo ERROR: Python is not installed or not in PATH
    echo Please install Python from https://python.org
    pause
    exit /b 1
)

REM Check if requirements are installed
echo Checking Python dependencies...
pip show fastapi >nul 2>&1
if %errorlevel% neq 0 (
    echo Installing required dependencies...
    pip install -r requirements.txt
)

REM Start the embedding API
echo Starting embedding API on http://localhost:8000
echo Press Ctrl+C to stop
echo.
python embedding_api.py

pause
