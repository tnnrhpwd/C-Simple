#!/usr/bin/env python3
"""
Simple test script to isolate the GPT-2 output issue
"""

import sys
import os

# Set environment variables before importing torch
os.environ["HF_HUB_DISABLE_SYMLINKS_WARNING"] = "1"
os.environ["TRANSFORMERS_CACHE"] = "C:\\Users\\tanne\\Documents\\CSimple\\Resources\\HFModels"

def test_gpt2():
    try:
        from transformers import AutoTokenizer, AutoModelForCausalLM
        import torch
        
        print("Testing GPT-2 generation...", file=sys.stderr)
        print("Progress: Starting GPT-2 test...", file=sys.stderr)
        
        model_id = "openai-community/gpt2"
        input_text = "test"
        
        # Force CPU mode
        print("Progress: Loading tokenizer...", file=sys.stderr)
        print("Loading tokenizer...", file=sys.stderr)
        tokenizer = AutoTokenizer.from_pretrained(model_id)
        print("Progress: ✓ Tokenizer loaded successfully", file=sys.stderr)
        
        print("Progress: Loading model...", file=sys.stderr)
        print("Loading model...", file=sys.stderr)
        model = AutoModelForCausalLM.from_pretrained(
            model_id,
            torch_dtype=torch.float32
        )
        model = model.to("cpu")
        print("Progress: ✓ Model loaded successfully", file=sys.stderr)
        
        print("Progress: Setting up tokenization...", file=sys.stderr)
        print("Setting up tokenization...", file=sys.stderr)
        if tokenizer.pad_token is None:
            tokenizer.pad_token = tokenizer.eos_token
        
        inputs = tokenizer(input_text, return_tensors="pt")
        inputs = {k: v.to("cpu") for k, v in inputs.items()}
        
        print("Progress: Generating text...", file=sys.stderr)
        print("Generating...", file=sys.stderr)
        with torch.no_grad():
            outputs = model.generate(
                **inputs,
                max_new_tokens=50,
                temperature=0.7,
                do_sample=True,
                pad_token_id=tokenizer.eos_token_id,
                eos_token_id=tokenizer.eos_token_id
            )
        
        generated_text = tokenizer.decode(outputs[0], skip_special_tokens=True)
        
        # Remove input from output
        if generated_text.startswith(input_text):
            generated_text = generated_text[len(input_text):].strip()
        
        print("Progress: ✓ Text generation complete", file=sys.stderr)
        print(f"Generated text: '{generated_text}'", file=sys.stderr)
        print(generated_text)  # Output to stdout
        
    except Exception as e:
        print(f"Error: {e}", file=sys.stderr)
        return 1
    
    return 0

if __name__ == "__main__":
    sys.exit(test_gpt2())
