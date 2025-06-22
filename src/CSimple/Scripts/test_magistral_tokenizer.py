#!/usr/bin/env python3
"""
Test script to isolate the Magistral tokenizer issue
"""

import sys
import os

# Set environment variables before importing torch
os.environ["HF_HUB_DISABLE_SYMLINKS_WARNING"] = "1"
os.environ["TRANSFORMERS_CACHE"] = "C:\\Users\\tanne\\Documents\\CSimple\\Resources\\HFModels"
os.environ["HF_HOME"] = "C:\\Users\\tanne\\Documents\\CSimple\\Resources\\HFModels"

def test_magistral_tokenizer():
    try:
        from transformers import AutoTokenizer
        import traceback
        
        print("Testing Magistral tokenizer loading...", file=sys.stderr)
        
        model_id = "mistralai/Magistral-Small-2506"
        
        print(f"Attempting to load tokenizer for {model_id}...", file=sys.stderr)
        
        # Try different loading approaches
        attempts = [
            {"use_fast": True, "trust_remote_code": True, "desc": "fast with remote code"},
            {"use_fast": False, "trust_remote_code": True, "desc": "slow with remote code"},
            {"use_fast": True, "trust_remote_code": False, "desc": "fast without remote code"},
            {"use_fast": False, "trust_remote_code": False, "desc": "slow without remote code"},
        ]
        
        for i, config in enumerate(attempts, 1):
            try:
                print(f"\nAttempt {i}: {config['desc']}", file=sys.stderr)
                tokenizer = AutoTokenizer.from_pretrained(
                    model_id,
                    use_fast=config["use_fast"],
                    trust_remote_code=config["trust_remote_code"],
                    cache_dir="C:\\Users\\tanne\\Documents\\CSimple\\Resources\\HFModels"
                )
                print(f"✓ SUCCESS: Tokenizer loaded with {config['desc']}", file=sys.stderr)
                print(f"Tokenizer type: {type(tokenizer).__name__}", file=sys.stderr)
                print(f"Vocab size: {tokenizer.vocab_size}", file=sys.stderr)
                
                # Test basic tokenization
                test_text = "Hello world"
                tokens = tokenizer(test_text)
                print(f"Test tokenization of '{test_text}': {tokens}", file=sys.stderr)
                
                return 0
                
            except Exception as e:
                print(f"✗ FAILED: {config['desc']} - {type(e).__name__}: {e}", file=sys.stderr)
                print(f"Full traceback:", file=sys.stderr)
                traceback.print_exc(file=sys.stderr)
        
        print("All tokenizer loading attempts failed", file=sys.stderr)
        return 1
        
    except Exception as e:
        print(f"Error: {e}", file=sys.stderr)
        traceback.print_exc(file=sys.stderr)
        return 1

if __name__ == "__main__":
    sys.exit(test_magistral_tokenizer())
