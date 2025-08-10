@echo off
echo Installing PyTorch with CUDA support for RTX 3090...
echo.

REM Check if we're in the virtual environment
if not defined VIRTUAL_ENV (
    echo Activating virtual environment...
    call "%LOCALAPPDATA%\CSimple\venv\Scripts\activate.bat"
)

echo Current PyTorch version:
python -c "import torch; print('PyTorch:', torch.__version__); print('CUDA available:', torch.cuda.is_available())"

echo.
echo Installing PyTorch with CUDA 12.1 support...
pip install torch torchvision torchaudio --index-url https://download.pytorch.org/whl/cu121

echo.
echo Verifying installation...
python -c "import torch; print('PyTorch:', torch.__version__); print('CUDA available:', torch.cuda.is_available()); print('GPU:', torch.cuda.get_device_name(0) if torch.cuda.is_available() else 'None')"

echo.
echo Installation complete!
pause
