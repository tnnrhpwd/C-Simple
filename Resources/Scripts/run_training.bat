@echo off
REM Model Training and Alignment Batch Script
REM Usage: run_training.bat [config_file] [model_path] [dataset_path] [output_path] [architecture_spec]

setlocal enabledelayedexpansion

REM Set script directory
set SCRIPT_DIR=%~dp0

REM Default values
set CONFIG_FILE=%SCRIPT_DIR%example_config.json
set DATASET_PATH=""
set OUTPUT_PATH=""
set MODEL_PATH=""
set ARCHITECTURE_SPEC=""

REM Parse command line arguments
if not "%1"=="" set CONFIG_FILE=%1
if not "%2"=="" set MODEL_PATH=%2
if not "%3"=="" set DATASET_PATH=%3
if not "%4"=="" set OUTPUT_PATH=%4
if not "%5"=="" set ARCHITECTURE_SPEC=%5

REM Validate required parameters
if %DATASET_PATH%=="" (
    echo Error: Dataset path is required
    echo Usage: run_training.bat [config_file] [model_path] [dataset_path] [output_path] [architecture_spec]
    exit /b 1
)

if %OUTPUT_PATH%=="" (
    echo Error: Output path is required
    echo Usage: run_training.bat [config_file] [model_path] [dataset_path] [output_path] [architecture_spec]
    exit /b 1
)

REM Check if Python is available
python --version >nul 2>&1
if errorlevel 1 (
    echo Error: Python is not installed or not in PATH
    exit /b 1
)

REM Check if required packages are installed
echo Checking Python dependencies...
python -c "import torch; import transformers; print('Dependencies OK')" >nul 2>&1
if errorlevel 1 (
    echo Warning: Some required packages may not be installed
    echo Installing requirements...
    pip install -r "%SCRIPT_DIR%training_requirements.txt"
    if errorlevel 1 (
        echo Error: Failed to install requirements
        exit /b 1
    )
)

REM Build command
set PYTHON_CMD=python "%SCRIPT_DIR%model_training_alignment.py" --config "%CONFIG_FILE%" --dataset_path %DATASET_PATH% --output_path %OUTPUT_PATH%

REM Add model path if provided
if not %MODEL_PATH%=="" (
    set PYTHON_CMD=!PYTHON_CMD! --model_path %MODEL_PATH%
)

REM Add architecture spec if provided
if not %ARCHITECTURE_SPEC%=="" (
    set PYTHON_CMD=!PYTHON_CMD! --architecture_spec %ARCHITECTURE_SPEC%
)

REM Display configuration
echo.
echo ========================================
echo Model Training and Alignment
echo ========================================
echo Config File: %CONFIG_FILE%
echo Dataset Path: %DATASET_PATH%
echo Output Path: %OUTPUT_PATH%
if not %MODEL_PATH%=="" echo Model Path: %MODEL_PATH%
if not %ARCHITECTURE_SPEC%=="" echo Architecture Spec: %ARCHITECTURE_SPEC%
echo.
echo Command: !PYTHON_CMD!
echo ========================================
echo.

REM Run the training script
echo Starting training...
!PYTHON_CMD!

REM Check result
if errorlevel 1 (
    echo.
    echo Training failed with error code %errorlevel%
    echo Check training.log for details
    exit /b %errorlevel%
) else (
    echo.
    echo Training completed successfully!
    echo Model saved to: %OUTPUT_PATH%
    echo Check training.log for details
)

endlocal
