#!/usr/bin/env python3
"""
Simple test script to verify the Python environment is working correctly.
This can be run independently to test the environment before using the main application.
"""

import sys
import os

def test_basic_imports():
    """Test basic library imports"""
    print("Testing basic imports...")
    
    try:
        import numpy as np
        print(f"✓ NumPy {np.__version__} imported successfully")
    except ImportError as e:
        print(f"✗ NumPy import failed: {e}")
        return False
    
    try:
        import torch
        print(f"✓ PyTorch {torch.__version__} imported successfully")
        print(f"  CUDA available: {torch.cuda.is_available()}")
        if torch.cuda.is_available():
            print(f"  CUDA device count: {torch.cuda.device_count()}")
            print(f"  Current device: {torch.cuda.current_device()}")
    except ImportError as e:
        print(f"✗ PyTorch import failed: {e}")
        return False
    
    try:
        # Set minimal verbosity to avoid hanging
        os.environ["TRANSFORMERS_VERBOSITY"] = "error"
        os.environ["HF_HUB_DISABLE_SYMLINKS_WARNING"] = "1"
        
        import transformers
        print(f"✓ Transformers {transformers.__version__} imported successfully")
        
        # Quick test of a simple model
        from transformers import AutoTokenizer
        print("✓ AutoTokenizer import successful")
        
    except ImportError as e:
        print(f"✗ Transformers import failed: {e}")
        return False
    except Exception as e:
        print(f"✗ Transformers test failed: {e}")
        return False
    
    return True

def test_tensor_creation():
    """Test tensor creation with padding"""
    print("\nTesting tensor creation...")
    
    try:
        import torch
        
        # Test basic tensor creation
        x = torch.tensor([1, 2, 3])
        print(f"✓ Basic tensor creation successful: {x}")
        
        # Test tensor with padding (the specific error you encountered)
        from transformers import AutoTokenizer
        
        # Use a lightweight tokenizer for testing
        tokenizer = AutoTokenizer.from_pretrained("gpt2")
        
        # Test tokenization with padding
        texts = ["Hello world", "This is a longer sentence for testing"]
        tokens = tokenizer(texts, padding=True, return_tensors="pt")
        print(f"✓ Tokenization with padding successful")
        print(f"  Input IDs shape: {tokens['input_ids'].shape}")
        
    except Exception as e:
        print(f"✗ Tensor creation test failed: {e}")
        return False
    
    return True

def main():
    print("Python Environment Test")
    print("=" * 40)
    print(f"Python version: {sys.version}")
    print(f"Python executable: {sys.executable}")
    print(f"Virtual environment: {'Yes' if hasattr(sys, 'real_prefix') or (hasattr(sys, 'base_prefix') and sys.base_prefix != sys.prefix) else 'No'}")
    print()
    
    success = True
    success &= test_basic_imports()
    success &= test_tensor_creation()
    
    print()
    if success:
        print("🎉 All tests passed! Your Python environment is ready.")
        return 0
    else:
        print("❌ Some tests failed. Check the errors above.")
        return 1

if __name__ == "__main__":
    sys.exit(main())
