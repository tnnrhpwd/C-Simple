#!/usr/bin/env python3
"""
Test script to validate the path configuration and basic functionality.
"""

import os
import sys

def test_script_path():
    """Test if the script can be found at the expected location."""
    script_path = r"c:\Users\tanne\Documents\Github\C-Simple\src\CSimple\Scripts\run_hf_model.py"
    
    if os.path.exists(script_path):
        print(f"✓ Script found at: {script_path}")
        return True
    else:
        print(f"✗ Script not found at: {script_path}")
        return False

def test_python_imports():
    """Test if required Python packages are available."""
    try:
        import torch
        print("✓ PyTorch available")
    except ImportError:
        print("✗ PyTorch not available")
    
    try:
        import transformers
        print("✓ Transformers library available")
    except ImportError:
        print("✗ Transformers library not available")
    
    try:
        from PIL import Image
        print("✓ PIL/Pillow available")
    except ImportError:
        print("✗ PIL/Pillow not available")

if __name__ == "__main__":
    print("Testing C-Simple Python environment...")
    test_script_path()
    test_python_imports()
    print("Test completed.")
