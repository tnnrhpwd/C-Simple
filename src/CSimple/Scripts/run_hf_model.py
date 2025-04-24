#!/usr/bin/env python3
"""
HuggingFace Model Execution Script for C-Simple

This script loads a model from HuggingFace and runs inference with the provided input.
It's designed to be called from C-Simple's .NET application.

Usage:
  python run_hf_model.py --model_id "<huggingface_model_id>" --input "<input_text>"

Example:
  python run_hf_model.py --model_id "gpt2" --input "Hello, world!"
"""

import argparse
import sys
import traceback
import os
import subprocess
from typing import Dict, Any, Optional
import importlib.util


def parse_arguments() -> argparse.Namespace:
    """Parse command line arguments."""
    parser = argparse.ArgumentParser(description="Run inference using HuggingFace models")
    parser.add_argument("--model_id", type=str, required=True, help="HuggingFace model ID (e.g., 'gpt2')")
    parser.add_argument("--input", type=str, required=True, help="Input text for the model")
    parser.add_argument("--max_length", type=int, default=100, help="Maximum length of the generated text")
    parser.add_argument("--temperature", type=float, default=0.7, help="Temperature for sampling")
    parser.add_argument("--top_p", type=float, default=0.9, help="Top-p sampling parameter")
    return parser.parse_args()


def check_package(package_name: str) -> bool:
    """Check if a package is installed."""
    return importlib.util.find_spec(package_name) is not None


def setup_environment() -> bool:
    """Set up the environment and check for required packages."""
    required_packages = ["transformers", "torch"]
    missing_packages = [pkg for pkg in required_packages if not check_package(pkg)]
    
    if not missing_packages:
        try:
            # Set up logging even if packages are already installed
            import transformers
            import logging
            transformers.logging.set_verbosity_error()
            logging.getLogger().setLevel(logging.ERROR)
            return True
        except:
            # Fall through to installation if imports fail
            pass
    
    print("Missing required packages. Attempting to install...", file=sys.stderr)
    
    # First look for setup_environment.py in the same directory
    script_dir = os.path.dirname(os.path.abspath(__file__))
    setup_script = os.path.join(script_dir, "setup_environment.py")
    
    if os.path.exists(setup_script):
        print(f"Running setup script: {setup_script}", file=sys.stderr)
        try:
            # Run the setup script
            result = subprocess.run(
                [sys.executable, setup_script],
                capture_output=True,
                text=True,
                check=False  # Don't raise an exception if it fails
            )
            
            if result.returncode == 0:
                print("Setup completed successfully. Packages installed.", file=sys.stderr)
                
                # Try again to import and configure
                import transformers
                import logging
                transformers.logging.set_verbosity_error()
                logging.getLogger().setLevel(logging.ERROR)
                
                return True
            else:
                print(f"Setup script failed: {result.stderr}", file=sys.stderr)
        except Exception as e:
            print(f"Error running setup script: {str(e)}", file=sys.stderr)
    else:
        # If setup script not found, try pip directly
        try:
            print("Setup script not found. Trying direct pip installation...", file=sys.stderr)
            for package in missing_packages:
                print(f"Installing {package}...", file=sys.stderr)
                subprocess.check_call([sys.executable, "-m", "pip", "install", package])
            
            # Try again to import and configure
            import transformers
            import logging
            transformers.logging.set_verbosity_error()
            logging.getLogger().setLevel(logging.ERROR)
            
            return True
        except Exception as e:
            print(f"Error installing packages: {str(e)}", file=sys.stderr)
    
    print("Failed to set up environment. Please install required packages manually:", file=sys.stderr)
    print("pip install transformers torch", file=sys.stderr)
    return False


def detect_model_type(model_id: str) -> str:
    """Detect the type of model based on the model ID."""
    model_id_lower = model_id.lower()
    
    if any(name in model_id_lower for name in ["gpt2", "gpt-2", "llama", "mistral", "falcon", "bloom", "opt", "phi"]):
        return "text-generation"
    elif any(name in model_id_lower for name in ["t5", "bart", "pegasus"]):
        return "text2text-generation"
    elif any(name in model_id_lower for name in ["bert", "roberta", "albert"]):
        return "fill-mask"
    elif any(name in model_id_lower for name in ["whisper", "wav2vec"]):
        return "automatic-speech-recognition"
    
    # Default to text generation if unknown
    return "text-generation"


def run_text_generation(model_id: str, input_text: str, params: Dict[str, Any]) -> str:
    """Run text generation using the specified model."""
    from transformers import AutoModelForCausalLM, AutoTokenizer
    import torch
    
    try:
        # Load tokenizer and model
        print("Loading tokenizer and model...", file=sys.stderr)
        tokenizer = AutoTokenizer.from_pretrained(model_id)
        model = AutoModelForCausalLM.from_pretrained(
            model_id,
            torch_dtype=torch.float16 if torch.cuda.is_available() else torch.float32,
            device_map="auto"
        )
        
        # Prepare input
        print("Tokenizing input...", file=sys.stderr)
        inputs = tokenizer(input_text, return_tensors="pt")
        
        # Move inputs to the same device as model
        inputs = {k: v.to(model.device) for k, v in inputs.items()}
        
        # Generate
        print("Generating response...", file=sys.stderr)
        with torch.no_grad():
            outputs = model.generate(
                **inputs,
                max_length=params.get("max_length", 100),
                temperature=params.get("temperature", 0.7),
                top_p=params.get("top_p", 0.9),
                num_return_sequences=1,
                pad_token_id=tokenizer.eos_token_id
            )
        
        # Decode and return the generated text
        generated_text = tokenizer.decode(outputs[0], skip_special_tokens=True)
        return generated_text
    except Exception as e:
        print(f"Error during text generation: {str(e)}", file=sys.stderr)
        traceback.print_exc(file=sys.stderr)
        return f"Error: {str(e)}"


def run_text2text_generation(model_id: str, input_text: str, params: Dict[str, Any]) -> str:
    """Run text-to-text generation (like T5, BART)."""
    from transformers import AutoModelForSeq2SeqLM, AutoTokenizer
    
    try:
        tokenizer = AutoTokenizer.from_pretrained(model_id)
        model = AutoModelForSeq2SeqLM.from_pretrained(model_id)
        
        inputs = tokenizer(input_text, return_tensors="pt")
        outputs = model.generate(
            **inputs,
            max_length=params.get("max_length", 100)
        )
        
        return tokenizer.decode(outputs[0], skip_special_tokens=True)
    except Exception as e:
        print(f"Error during text2text generation: {str(e)}", file=sys.stderr)
        traceback.print_exc(file=sys.stderr)
        return f"Error: {str(e)}"


def run_model(args: argparse.Namespace) -> Optional[str]:
    """Run the model with the given arguments."""
    try:
        # Detect model type
        model_type = detect_model_type(args.model_id)
        print(f"Detected model type: {model_type}", file=sys.stderr)
        
        # Extract parameters
        params = {
            "max_length": args.max_length,
            "temperature": args.temperature,
            "top_p": args.top_p
        }
        
        # Run appropriate model type
        if model_type == "text-generation":
            return run_text_generation(args.model_id, args.input, params)
        elif model_type == "text2text-generation":
            return run_text2text_generation(args.model_id, args.input, params)
        else:
            print(f"Unsupported model type: {model_type}", file=sys.stderr)
            return f"Unsupported model type: {model_type}. Currently only text-generation and text2text-generation are supported."
    
    except Exception as e:
        print(f"Error running model: {str(e)}", file=sys.stderr)
        traceback.print_exc(file=sys.stderr)
        return None


def main() -> int:
    """Main entry point for the script."""
    try:
        # Parse arguments
        args = parse_arguments()
        
        # Set up environment
        if not setup_environment():
            return 1
        
        # Print arguments to stderr for debugging
        print(f"Model ID: {args.model_id}", file=sys.stderr)
        print(f"Input: {args.input}", file=sys.stderr)
        
        # Run model
        output = run_model(args)
        if output is None:
            return 1
        
        # Print output to stdout (for .NET to capture)
        print(output)
        return 0
    
    except Exception as e:
        print(f"Unhandled error: {str(e)}", file=sys.stderr)
        traceback.print_exc(file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
