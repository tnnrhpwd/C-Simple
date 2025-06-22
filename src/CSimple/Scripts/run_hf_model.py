#!/usr/bin/env python3
"""
Enhanced HuggingFace Model Execution Script for C-Simple

This script loads and runs HuggingFace models with better error handling,
CPU/GPU optimization, and support for quantized models like DeepSeek-R1.
"""

import argparse
import sys
import traceback
import os
import subprocess
import urllib.request
import urllib.parse
import urllib.error
import ssl
import time
import json
from pathlib import Path
from typing import Dict, Any, Optional
import importlib.util


def parse_arguments() -> argparse.Namespace:
    """Parse command line arguments."""
    parser = argparse.ArgumentParser(description="Run inference using HuggingFace models")
    parser.add_argument("--model_id", type=str, required=True, help="HuggingFace model ID")
    parser.add_argument("--input", type=str, required=True, help="Input text for the model")
    parser.add_argument("--max_length", type=int, default=150, help="Maximum length of generated text")
    parser.add_argument("--temperature", type=float, default=0.7, help="Temperature for sampling")
    parser.add_argument("--top_p", type=float, default=0.9, help="Top-p sampling parameter")
    parser.add_argument("--trust_remote_code", action="store_true", default=True, help="Trust remote code")
    parser.add_argument("--cpu_optimize", action="store_true", help="Force CPU optimization mode")
    parser.add_argument("--offline_mode", action="store_true", help="Force offline mode (no API fallback)")
    return parser.parse_args()


def check_and_install_package(package_name: str) -> bool:
    """Check if a package is installed, and try to install it if not."""
    if importlib.util.find_spec(package_name) is not None:
        return True
    
    print(f"Installing {package_name}...", file=sys.stderr)
    try:
        subprocess.check_call([sys.executable, "-m", "pip", "install", package_name])
        return True
    except subprocess.CalledProcessError:
        print(f"Failed to install {package_name}", file=sys.stderr)
        return False


def setup_environment() -> bool:
    """Set up the environment with all required packages."""
    # Set up the cache directory BEFORE importing transformers
    cache_dir = "C:\\Users\\tanne\\Documents\\CSimple\\Resources\\HFModels"
    os.makedirs(cache_dir, exist_ok=True)
    os.environ["TRANSFORMERS_CACHE"] = cache_dir
    os.environ["HF_HOME"] = cache_dir
    print(f"Set model cache directory to: {cache_dir}", file=sys.stderr)
    
    required_packages = {
        "transformers": "transformers",
        "torch": "torch", 
        "accelerate": "accelerate"  # Required for quantized models
    }
    
    missing_packages = []
    for package_name, pip_name in required_packages.items():
        if not check_and_install_package(pip_name):
            missing_packages.append(package_name)
    
    if missing_packages:
        print(f"Failed to install required packages: {missing_packages}", file=sys.stderr)
        return False
    
    try:
        # Configure logging to reduce noise
        import transformers
        import logging
        transformers.logging.set_verbosity_error()
        logging.getLogger().setLevel(logging.ERROR)
        
        # Set environment variables to handle security warnings
        os.environ["HF_HUB_DISABLE_SYMLINKS_WARNING"] = "1"
        return True
    except Exception as e:
        print(f"Error configuring environment: {e}", file=sys.stderr)
        return False


def detect_model_type(model_id: str) -> str:
    """Detect the type of model based on the model ID."""
    model_id_lower = model_id.lower()
    
    # DeepSeek models
    if "deepseek" in model_id_lower:
        return "text-generation"
    
    # Other text generation models
    if any(name in model_id_lower for name in ["gpt", "llama", "mistral", "qwen", "phi"]):
        return "text-generation"
    
    # Encoder-decoder models
    if any(name in model_id_lower for name in ["t5", "bart", "pegasus"]):
        return "text2text-generation"
    
    # BERT-like models
    if any(name in model_id_lower for name in ["bert", "roberta", "albert"]):
        return "fill-mask"
    
    return "text-generation"  # Default


def run_text_generation(model_id: str, input_text: str, params: Dict[str, Any]) -> str:
    """Run text generation with enhanced error handling for quantized models."""
    try:
        from transformers import AutoTokenizer, AutoModelForCausalLM
        import torch
        
        print(f"Loading tokenizer for {model_id}...", file=sys.stderr)
        
        # Load tokenizer with proper cache settings
        tokenizer = AutoTokenizer.from_pretrained(
            model_id, 
            trust_remote_code=params.get("trust_remote_code", True),
            cache_dir="C:\\Users\\tanne\\Documents\\CSimple\\Resources\\HFModels"
        )
        
        print(f"Loading model {model_id}...", file=sys.stderr)
        
        # Check if CPU optimization is requested or if CUDA is not available
        force_cpu = params.get("cpu_optimize", False) or not torch.cuda.is_available()
        
        if force_cpu:
            print("Using CPU mode (GPU disabled or not available)", file=sys.stderr)
            # Force CPU execution
            device = "cpu"
            torch_dtype = torch.float32
            device_map = None
        else:
            print("Using GPU acceleration", file=sys.stderr)
            device = "auto"
            torch_dtype = torch.float16
            device_map = "auto"
        
        # Load model with appropriate device settings and proper cache
        model_kwargs = {
            "trust_remote_code": params.get("trust_remote_code", True),
            "torch_dtype": torch_dtype,
            "low_cpu_mem_usage": True,
            "cache_dir": "C:\\Users\\tanne\\Documents\\CSimple\\Resources\\HFModels"
        }
        
        if device_map:
            model_kwargs["device_map"] = device_map
        
        print(f"Downloading/loading model files (this may take a while for first-time downloads)...", file=sys.stderr)
        model = AutoModelForCausalLM.from_pretrained(model_id, **model_kwargs)
          # Move model to CPU if force_cpu is enabled
        if force_cpu and device_map is None:
            model = model.to("cpu")
        
        print("Tokenizing input...", file=sys.stderr)
        
        # Clean and validate input text
        clean_input = input_text.strip()
        if not clean_input:
            return "ERROR: Empty input provided"
        
        print(f"Clean input: '{clean_input}'", file=sys.stderr)
        
        # Handle tokenization
        if tokenizer.pad_token is None:
            tokenizer.pad_token = tokenizer.eos_token
        
        inputs = tokenizer(
            clean_input, 
            return_tensors="pt", 
            truncation=True, 
            max_length=512,
            add_special_tokens=True
        )
          # Move inputs to the same device as model
        if force_cpu:
            inputs = {k: v.to("cpu") for k, v in inputs.items()}
        else:
            device = next(model.parameters()).device
            inputs = {k: v.to(device) for k, v in inputs.items()}
        
        print("Generating response...", file=sys.stderr)
        
        # Validate inputs before generation
        print(f"Input shape: {inputs['input_ids'].shape}", file=sys.stderr)
        print(f"Input tokens: {inputs['input_ids'].tolist()}", file=sys.stderr)
          # Generate with improved parameters
        with torch.no_grad():
            outputs = model.generate(
                **inputs,
                max_new_tokens=min(params.get("max_length", 100), 50),  # Cap at 50 tokens for cleaner output
                temperature=0.8,  # Slightly higher temperature for more natural output
                top_p=0.9,
                top_k=50,  # Add top-k filtering
                do_sample=True,
                num_return_sequences=1,
                pad_token_id=tokenizer.eos_token_id,
                eos_token_id=tokenizer.eos_token_id,
                repetition_penalty=1.2,  # Stronger repetition penalty
                no_repeat_ngram_size=3,  # Prevent 3-gram repetition
                early_stopping=True  # Stop at natural endings
            )        # Decode the generated text
        generated_text = tokenizer.decode(outputs[0], skip_special_tokens=True)
        
        print(f"Raw generated text: '{generated_text}'", file=sys.stderr)
        
        # Remove the input text from the output if it's included
        if generated_text.startswith(clean_input):
            generated_text = generated_text[len(clean_input):].strip()
        
        print(f"Cleaned generated text: '{generated_text}'", file=sys.stderr)
        
        # Ensure we have a reasonable response
        if not generated_text or len(generated_text.strip()) == 0:
            return f"Model processed '{clean_input}' successfully but generated no additional text."
        
        # Check for repeated patterns that might indicate corruption
        words = generated_text.split()
        if len(set(words)) < 3 and len(words) > 5:
            print("Warning: Detected repetitive output, possibly corrupted", file=sys.stderr)
            return f"Model response may be corrupted. Original input: '{clean_input}'"
        
        # Check for technical/config file patterns that suggest corruption
        if any(pattern in generated_text.lower() for pattern in ['.cfg', 'kernel_', 'lib64', 'steam', 'program files']):
            print("Warning: Detected system file patterns, regenerating...", file=sys.stderr)
            return f"Model produced system file output for input '{clean_input}'. This may indicate a corrupted model cache."
        
        return generated_text
        
    except Exception as e:
        error_msg = str(e)
        print(f"Error in text generation: {error_msg}", file=sys.stderr)
        
        # Provide specific error messages
        if "accelerate" in error_msg.lower():
            return "ERROR: This model requires the 'accelerate' package. Please install it with: pip install accelerate"
        elif "cuda" in error_msg.lower() or "memory" in error_msg.lower():
            return "ERROR: Insufficient GPU memory. Try using a smaller model or running on CPU."
        elif "trust_remote_code" in error_msg.lower():
            return "ERROR: Model requires trust_remote_code=True but was blocked for security."
        else:
            return f"ERROR: {error_msg}"


def check_model_cache_status(model_id):
    """Check if model is already cached and report download status"""
    try:
        cache_dir = "C:\\Users\\tanne\\Documents\\CSimple\\Resources\\HFModels"
        
        # Check for model files in transformers cache format
        model_cache_path = Path(cache_dir)
        model_hash = model_id.replace("/", "--")
        
        # Look for the standard transformers cache structure
        model_dirs = []
        if model_cache_path.exists():
            # Look for models--{org}--{model_name} directory structure
            for item in model_cache_path.iterdir():
                if item.is_dir() and model_hash in item.name:
                    model_dirs.append(item)
        
        if model_dirs:
            total_size = 0
            file_count = 0
            for model_dir in model_dirs:
                for file_path in model_dir.rglob("*"):
                    if file_path.is_file():
                        total_size += file_path.stat().st_size
                        file_count += 1
            
            print(f"✓ Model '{model_id}' found in cache ({file_count} files)", file=sys.stderr)
            print(f"  Cache size: {total_size / (1024*1024):.1f} MB", file=sys.stderr)
            return True
        else:
            print(f"⬇ Model '{model_id}' not cached - will download from HuggingFace Hub", file=sys.stderr)
            return False

    except Exception as e:
        print(f"Note: Could not check cache status: {e}", file=sys.stderr)
        return False


def main() -> int:
    """Main entry point."""
    try:
        args = parse_arguments()
        
        print(f"Setting up environment for model: {args.model_id}", file=sys.stderr)
        
        if not setup_environment():
            print("ERROR: Failed to set up Python environment", file=sys.stderr)
            return 1
        
        print(f"Processing input: '{args.input}'", file=sys.stderr)
        
        # Check model cache status
        check_model_cache_status(args.model_id)
        
        # Detect model type and run appropriate function
        model_type = detect_model_type(args.model_id)
        print(f"Detected model type: {model_type}", file=sys.stderr)
        
        params = {
            "max_length": args.max_length,
            "temperature": args.temperature,
            "top_p": args.top_p,
            "trust_remote_code": args.trust_remote_code,
            "cpu_optimize": args.cpu_optimize,
            "offline_mode": args.offline_mode
        }
        
        if model_type == "text-generation":
            result = run_text_generation(args.model_id, args.input, params)
        else:
            result = f"Model type '{model_type}' not fully implemented yet. Basic response: Processed '{args.input}' with {args.model_id}"
        
        # Print result to stdout
        print(result)
        return 0
        
    except KeyboardInterrupt:
        print("ERROR: Operation cancelled by user", file=sys.stderr)
        return 1
    except Exception as e:
        print(f"ERROR: Unhandled exception: {str(e)}", file=sys.stderr)
        traceback.print_exc(file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
