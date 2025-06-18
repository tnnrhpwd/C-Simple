# Python Virtual Environment Setup

C-Simple automatically manages Python dependencies using a virtual environment to avoid conflicts with your system Python installation.

## How it Works

1. **Automatic Detection**: The application automatically detects your system Python installation (Python 3.8-3.11 supported)

2. **Virtual Environment Creation**: On first run, C-Simple creates a virtual environment at:
   - Windows: `%LOCALAPPDATA%\CSimple\venv`
   - macOS/Linux: `~/.local/share/CSimple/venv`

3. **Dependency Installation**: Required ML packages (PyTorch, Transformers, etc.) are installed only in this virtual environment

4. **Isolated Execution**: All Python scripts run using the virtual environment, keeping your system Python clean

## Benefits

- **No System Pollution**: Your system Python remains unchanged
- **Dependency Isolation**: Prevents conflicts with other Python projects
- **Automatic Management**: No manual setup required
- **Git Friendly**: Virtual environment is excluded from version control

## Troubleshooting

If you encounter issues:

1. **Python Not Found**: Install Python 3.8-3.11 from python.org
2. **Permission Errors**: Run as administrator if needed
3. **Network Issues**: Check internet connection for package downloads
4. **Clear Cache**: Delete `%LOCALAPPDATA%\CSimple\venv` to recreate the environment

## Manual Setup (Advanced)

If you prefer manual control:

```bash
# Create virtual environment
python -m venv %LOCALAPPDATA%\CSimple\venv

# Activate (Windows)
%LOCALAPPDATA%\CSimple\venv\Scripts\activate

# Install dependencies
pip install -r requirements.txt
```

## Package List

The virtual environment includes:

- `torch` - PyTorch deep learning framework
- `transformers` - Hugging Face transformers library
- `accelerate` - Accelerated training and inference
- `tokenizers` - Fast tokenization library
- Additional supporting packages

All packages are installed with compatible versions to ensure stability.
