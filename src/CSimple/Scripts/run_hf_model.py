#!/usr/bin/env python3
"""
Enhanced HuggingFace Model Execution Script for C-Simple

This script loads and runs HuggingFace models with better error handling
and support for quantized models like DeepSeek-R1.
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
import json  # Add missing import for JSON handling
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
        
        # Load tokenizer
        tokenizer = AutoTokenizer.from_pretrained(
            model_id, 
            trust_remote_code=params.get("trust_remote_code", True)
        )
        
        print(f"Loading model {model_id}...", file=sys.stderr)
        
        # Load model with device mapping for large models
        model = AutoModelForCausalLM.from_pretrained(
            model_id,
            trust_remote_code=params.get("trust_remote_code", True),
            torch_dtype=torch.float16 if torch.cuda.is_available() else torch.float32,
            device_map="auto",
            low_cpu_mem_usage=True
        )
        
        print("Tokenizing input...", file=sys.stderr)
        
        # Handle tokenization
        if tokenizer.pad_token is None:
            tokenizer.pad_token = tokenizer.eos_token
        
        inputs = tokenizer(
            input_text, 
            return_tensors="pt", 
            truncation=True, 
            max_length=512
        )
        
        # Move inputs to the same device as model
        device = next(model.parameters()).device
        inputs = {k: v.to(device) for k, v in inputs.items()}
        
        print("Generating response...", file=sys.stderr)
        
        # Generate with improved parameters
        with torch.no_grad():
            outputs = model.generate(
                **inputs,
                max_new_tokens=params.get("max_length", 100),
                temperature=params.get("temperature", 0.7),
                top_p=params.get("top_p", 0.9),
                do_sample=True,
                num_return_sequences=1,
                pad_token_id=tokenizer.eos_token_id,
                eos_token_id=tokenizer.eos_token_id
            )
        
        # Decode the generated text
        generated_text = tokenizer.decode(outputs[0], skip_special_tokens=True)
        
        # Remove the input text from the output if it's included
        if generated_text.startswith(input_text):
            generated_text = generated_text[len(input_text):].strip()
        
        return generated_text if generated_text else f"Model processed '{input_text}' successfully but generated no additional text."
        
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
        from transformers.utils import TRANSFORMERS_CACHE
        from huggingface_hub import HfApi

        # Get the default cache directory
        cache_dir = os.environ.get('TRANSFORMERS_CACHE', TRANSFORMERS_CACHE)
        if not cache_dir:
            cache_dir = os.path.join(os.path.expanduser("~"), ".cache", "huggingface", "transformers")

        # Check for model files in cache
        model_cache_path = Path(cache_dir)
        model_hash = model_id.replace("/", "--")

        # Look for any cached files related to this model
        cached_files = []
        if model_cache_path.exists():
            for item in model_cache_path.rglob("*"):
                if model_hash in item.name or model_id.split("/")[-1] in item.name:
                    cached_files.append(item)

        if cached_files:
            print(f"✓ Model '{model_id}' found in cache ({len(cached_files)} files)", file=sys.stderr)
            total_size = sum(f.stat().st_size for f in cached_files if f.is_file())
            print(f"  Cache size: {total_size / (1024*1024):.1f} MB", file=sys.stderr)
            return True
        else:
            print(f"⬇ Model '{model_id}' not cached - will download from HuggingFace Hub", file=sys.stderr)
            return False

    except Exception as e:
        print(f"Note: Could not check cache status: {e}", file=sys.stderr)
        return False


def call_huggingface_api(model_id, inputs, api_key=None, timeout=30):
    """Fallback to HuggingFace API when local execution fails"""
    api_url = f"https://api-inference.huggingface.co/models/{model_id}"
    payload = {"inputs": inputs}
    data = json.dumps(payload).encode('utf-8')
    headers = {'Content-Type': 'application/json'}

    # Add API key if provided
    if api_key:
        headers['Authorization'] = f'Bearer {api_key}'

    ssl_context = ssl._create_unverified_context()
    try:
        req = urllib.request.Request(api_url, data=data, headers=headers, method='POST')
        with urllib.request.urlopen(req, timeout=timeout, context=ssl_context) as response:
            response_data = response.read().decode('utf-8')
            return json.loads(response_data)
    except urllib.error.HTTPError as e:
        if e.code == 429:
            print("Model is loading on HuggingFace servers, waiting...", file=sys.stderr)
            time.sleep(20)
            return call_huggingface_api(model_id, inputs, api_key, timeout)
        elif e.code == 401:
            print(f"AUTHENTICATION_ERROR: Model '{model_id}' requires a HuggingFace API key for access.", file=sys.stderr)
            print("This model is either gated or requires authentication.", file=sys.stderr)
            print("Get a free API key from: https://huggingface.co/settings/tokens", file=sys.stderr)
            print("Then run with: --api_key YOUR_API_KEY", file=sys.stderr)
            return {"error": f"Authentication required for model {model_id}", "error_type": "authentication"}
        elif e.code == 404:
            print(f"Model '{model_id}' not found or not accessible via API.", file=sys.stderr)
            return {"error": f"Model {model_id} not found"}
        else:
            error_body = e.read().decode('utf-8')
            print(f"API Error: {e.code} - {error_body}", file=sys.stderr)
            return {"error": f"API Error {e.code}", "details": error_body}
    except Exception as e:
        print(f"API request error: {str(e)}", file=sys.stderr)
        return {"error": str(e)}


def extract_text_from_api_response(response):
    """Extract text from HuggingFace API response"""
    if isinstance(response, list) and len(response) > 0:
        if isinstance(response[0], dict) and "generated_text" in response[0]:
            return response[0]["generated_text"]
    elif isinstance(response, dict):
        if "generated_text" in response:
            return response["generated_text"]
        if "error" in response:
            return f"API Error: {response['error']}"
    return str(response)


def check_and_suggest_cuda_setup():
    """Check CUDA setup and provide installation instructions if needed"""
    try:
        import torch
        if not torch.cuda.is_available():
            print("\n" + "="*60, file=sys.stderr)
            print("CUDA SETUP REQUIRED FOR GPU ACCELERATION", file=sys.stderr)
            print("="*60, file=sys.stderr)
            print("Your system may have an NVIDIA GPU, but PyTorch can't access it.", file=sys.stderr)
            print("Current PyTorch version:", torch.__version__, file=sys.stderr)

            if torch.version.cuda:
                print("PyTorch is installed but CUDA is not configured.", file=sys.stderr)
            else:
                print("PyTorch is installed but does not support CUDA.", file=sys.stderr)

            print("\nTo enable GPU acceleration:", file=sys.stderr)
            print("1. Check your NVIDIA driver version with: nvidia-smi", file=sys.stderr)
            print("2. Install PyTorch with CUDA support:", file=sys.stderr)
            print("   pip3 install torch torchvision torchaudio --index-url https://download.pytorch.org/whl/cu121", file=sys.stderr)
            print("3. Or visit: https://pytorch.org/get-started/locally/", file=sys.stderr)
            print("="*60, file=sys.stderr)
            return False
        else:
            return True
    except Exception as e:
        print(f"Error checking CUDA setup: {e}", file=sys.stderr)
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
        
        # Detect model type and run appropriate function
        model_type = detect_model_type(args.model_id)
        print(f"Detected model type: {model_type}", file=sys.stderr)
        
        params = {
            "max_length": args.max_length,
            "temperature": args.temperature,
            "top_p": args.top_p,
            "trust_remote_code": args.trust_remote_code
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
