#!/usr/bin/env python3
"""
Fallback script for HuggingFace model inference.
This simplified script will attempt to run even without a full virtual environment.
"""

import sys
import argparse
import json
import traceback

# Fix Windows console encoding issues for Unicode characters
if sys.platform.startswith('win'):
    import codecs
    # Ensure stdout can handle UTF-8 encoding
    if hasattr(sys.stdout, 'reconfigure'):
        try:
            sys.stdout.reconfigure(encoding='utf-8', errors='replace')
        except:
            pass
    elif hasattr(sys.stdout, 'buffer'):
        try:
            sys.stdout = codecs.getwriter('utf-8')(sys.stdout.buffer, errors='replace')
        except:
            pass

def main():
    try:
        # Parse command line arguments
        parser = argparse.ArgumentParser(description="Fallback HuggingFace inference")
        parser.add_argument("--model_id", type=str, required=True, help="HuggingFace model ID")
        parser.add_argument("--input", type=str, required=True, help="Input text for the model")
        parser.add_argument("--max_length", type=int, default=250, help="Maximum token length (default: 250)")
        args = parser.parse_args()
        
        # Try to import required modules
        try:
            import transformers
            import torch
        except ImportError:
            print("Required packages not installed. Please install manually:", file=sys.stderr)
            print("pip install transformers torch", file=sys.stderr)
            print("ERROR: Python dependencies not available")
            return 1
        
        print(f"Running inference on model: {args.model_id}", file=sys.stderr)
        print(f"Input: {args.input}", file=sys.stderr)
        
        # Set up logging
        transformers.logging.set_verbosity_error()
        
        # Load tokenizer and model
        print("Loading tokenizer and model...", file=sys.stderr)
        tokenizer = transformers.AutoTokenizer.from_pretrained(args.model_id)
        model = transformers.AutoModelForCausalLM.from_pretrained(
            args.model_id,
            torch_dtype=torch.float16 if torch.cuda.is_available() else torch.float32,
            device_map="auto"
        )
        
        # Tokenize input
        print("Processing input...", file=sys.stderr)
        inputs = tokenizer(args.input, return_tensors="pt")
        inputs = {k: v.to(model.device) for k, v in inputs.items()}
        
        # Generate
        print("Generating response...", file=sys.stderr)
        with torch.no_grad():
            outputs = model.generate(
                **inputs,
                max_length=args.max_length,
                temperature=0.7,
                top_p=0.9,
                num_return_sequences=1,
                pad_token_id=tokenizer.eos_token_id
            )
        
        # Decode and return the generated text
        generated_text = tokenizer.decode(outputs[0], skip_special_tokens=True)
        
        # Handle Unicode encoding issues on Windows by encoding safely
        try:
            # Try to encode to detect and handle Unicode issues
            encoded_result = generated_text.encode('utf-8', errors='replace').decode('utf-8')
            # Remove or replace problematic Unicode characters for Windows console compatibility
            safe_result = encoded_result.encode('ascii', errors='replace').decode('ascii')
            print(safe_result)
        except UnicodeError:
            # Fallback: Remove all non-ASCII characters
            safe_result = ''.join(char for char in generated_text if ord(char) < 128)
            print(safe_result)
        return 0
    
    except Exception as e:
        print(f"Error in fallback script: {str(e)}", file=sys.stderr)
        traceback.print_exc(file=sys.stderr)
        print(f"ERROR: {str(e)}")
        return 1

if __name__ == "__main__":
    sys.exit(main())
