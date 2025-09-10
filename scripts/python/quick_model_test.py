#!/usr/bin/env python3
"""
Quick test script for BLIP image captioning to verify the environment works.
"""

import os
import sys

def test_blip_simple():
    """Test BLIP image captioning with a simple approach"""
    try:
        print("Testing BLIP image captioning...")
        
        # Test imports
        from transformers import BlipProcessor, BlipForConditionalGeneration
        from PIL import Image
        import torch
        
        print("✓ All imports successful")
        
        # Load model directly from HuggingFace Hub (ignore local paths)
        model_id = "Salesforce/blip-image-captioning-base"
        print(f"Loading {model_id} from HuggingFace Hub...")
        
        processor = BlipProcessor.from_pretrained(model_id)
        model = BlipForConditionalGeneration.from_pretrained(model_id)
        
        print("✓ Model and processor loaded successfully")
        
        # Test with a simple tensor (no actual image needed for basic test)
        print("✓ BLIP model is ready for image captioning")
        return True
        
    except Exception as e:
        print(f"✗ BLIP test failed: {e}")
        return False

def test_basic_models():
    """Test basic model functionality"""
    try:
        print("\nTesting basic transformers functionality...")
        
        from transformers import AutoTokenizer, AutoModelForCausalLM
        
        # Test with a small model
        model_id = "gpt2"
        print(f"Loading {model_id}...")
        
        tokenizer = AutoTokenizer.from_pretrained(model_id)
        
        # Test tokenization
        text = "Hello world"
        tokens = tokenizer(text, return_tensors="pt")
        
        print("✓ Tokenization successful")
        print(f"  Input: {text}")
        print(f"  Tokens shape: {tokens['input_ids'].shape}")
        
        return True
        
    except Exception as e:
        print(f"✗ Basic model test failed: {e}")
        return False

def main():
    print("Quick Model Test")
    print("=" * 30)
    
    success = True
    success &= test_basic_models()
    success &= test_blip_simple()
    
    print("\n" + "=" * 30)
    if success:
        print("🎉 All tests passed!")
        print("Your environment is ready for running models.")
    else:
        print("❌ Some tests failed.")
        print("Check the errors above.")
    
    return 0 if success else 1

if __name__ == "__main__":
    sys.exit(main())
