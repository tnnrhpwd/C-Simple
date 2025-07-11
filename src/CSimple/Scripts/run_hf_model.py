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

# Will be imported after environment setup
torch = None
from typing import Dict, Any, Optional
import importlib.util


def progress_callback(filename: str, current: int, total: int):
    """Progress callback for HuggingFace downloads."""
    if total > 0:
        percentage = (current / total) * 100
        # Only print every 10% to avoid spam
        if percentage % 10 < 1:
            print(f"Progress: {filename} - {percentage:.0f}% ({current}/{total} bytes)", file=sys.stderr)
    else:
        print(f"Progress: {filename} - {current} bytes downloaded", file=sys.stderr)


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
    parser.add_argument("--local_model_path", type=str, help="Local path to model directory (overrides model_id for loading)")
    return parser.parse_args()


def check_and_install_package(package_name: str) -> bool:
    """Check if a package is installed, and try to install it if not."""
    if importlib.util.find_spec(package_name) is not None:
        print(f"✓ {package_name} already installed", file=sys.stderr)
        return True
    
    print(f"Progress: Installing {package_name}...", file=sys.stderr)
    print(f"Installing {package_name}...", file=sys.stderr)
    try:
        # Redirect pip output to stderr to avoid mixing with model output
        result = subprocess.run([sys.executable, "-m", "pip", "install", package_name], 
                              capture_output=True, text=True, check=True)
        # Print pip output to stderr
        if result.stdout:
            print(result.stdout, file=sys.stderr, end='')
        if result.stderr:
            print(result.stderr, file=sys.stderr, end='')
        print(f"✓ {package_name} installed successfully", file=sys.stderr)
        return True
    except subprocess.CalledProcessError as e:
        print(f"Failed to install {package_name}", file=sys.stderr)
        if e.stdout:
            print(e.stdout, file=sys.stderr, end='')
        if e.stderr:
            print(e.stderr, file=sys.stderr, end='')
        return False


def setup_environment() -> bool:
    """Set up the environment with all required packages."""
    # Set up the cache directory BEFORE importing transformers
    cache_dir = "C:\\Users\\tanne\\Documents\\CSimple\\Resources\\HFModels"
    os.makedirs(cache_dir, exist_ok=True)
    os.environ["TRANSFORMERS_CACHE"] = cache_dir
    os.environ["HF_HOME"] = cache_dir
    os.environ["HF_HUB_DISABLE_SYMLINKS_WARNING"] = "1"
    # Enable progress bars for downloads
    os.environ["HF_HUB_ENABLE_HF_TRANSFER"] = "0"  # Disable for compatibility
    
    print(f"Set model cache directory to: {cache_dir}", file=sys.stderr)
    
    required_packages = {
        "transformers": "transformers",
        "torch": "torch", 
        "accelerate": "accelerate",  # Required for quantized models
        "protobuf": "protobuf",  # Required for many HuggingFace models
        "sentencepiece": "sentencepiece",  # Required for SentencePiece tokenizers
        "safetensors": "safetensors"  # Required for secure model loading
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
        
        # Import torch for model operations
        global torch
        import torch
        
        # Set environment variables to handle security warnings
        os.environ["HF_HUB_DISABLE_SYMLINKS_WARNING"] = "1"
        return True
    except Exception as e:
        print(f"Error configuring environment: {e}", file=sys.stderr)
        return False


def detect_model_type(model_id: str) -> str:
    """Detect the type of model based on the model ID."""
    model_id_lower = model_id.lower()
    
    # Audio/Speech models
    if "whisper" in model_id_lower:
        return "automatic-speech-recognition"
    if any(name in model_id_lower for name in ["wav2vec", "hubert", "speecht5_asr"]):
        return "automatic-speech-recognition"
    
    # Vision/Image models
    if "blip" in model_id_lower:
        return "image-to-text"
    if any(name in model_id_lower for name in ["vit", "clip", "detr", "deit"]):
        return "image-classification"
    if any(name in model_id_lower for name in ["stable-diffusion", "diffusion"]):
        return "text-to-image"
    
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


def run_text_generation(model_id: str, input_text: str, params: Dict[str, Any], local_model_path: Optional[str] = None) -> str:
    """Run text generation with enhanced error handling for quantized models."""
    try:
        from transformers import AutoTokenizer, AutoModelForCausalLM
        import torch
        
        # Determine the actual model path to use
        model_path_to_use = local_model_path if local_model_path and os.path.exists(local_model_path) else model_id
        
        if local_model_path and os.path.exists(local_model_path):
            print(f"Using local model path: {local_model_path}", file=sys.stderr)
        else:
            print(f"Loading tokenizer for {model_id}...", file=sys.stderr)
          # Load tokenizer with enhanced error handling for SentencePiece/Tiktoken issues
        try:
            print(f"Attempting fast tokenizer load for {model_path_to_use}...", file=sys.stderr)
            # First try with fast tokenizer (default)
            tokenizer = AutoTokenizer.from_pretrained(
                model_path_to_use, 
                trust_remote_code=params.get("trust_remote_code", True),
                cache_dir="C:\\Users\\tanne\\Documents\\CSimple\\Resources\\HFModels" if not local_model_path else None,
                resume_download=True if not local_model_path else False,
                local_files_only=bool(local_model_path)
            )
            print(f"✓ Tokenizer loaded successfully (fast tokenizer)", file=sys.stderr)
        except Exception as fast_error:
            print(f"Fast tokenizer failed: {type(fast_error).__name__}: {fast_error}", file=sys.stderr)
            print(f"Attempting to load slow tokenizer...", file=sys.stderr)
            
            try:
                # Fallback to slow tokenizer if fast tokenizer fails
                tokenizer = AutoTokenizer.from_pretrained(
                    model_path_to_use, 
                    trust_remote_code=params.get("trust_remote_code", True),
                    cache_dir="C:\\Users\\tanne\\Documents\\CSimple\\Resources\\HFModels" if not local_model_path else None,
                    resume_download=True if not local_model_path else False,
                    use_fast=False,  # Force slow tokenizer
                    local_files_only=bool(local_model_path)
                )
                print(f"✓ Tokenizer loaded successfully (slow tokenizer)", file=sys.stderr)
            except Exception as slow_error:
                print(f"Both fast and slow tokenizers failed", file=sys.stderr)
                print(f"Fast error: {type(fast_error).__name__}: {fast_error}", file=sys.stderr)
                print(f"Slow error: {type(slow_error).__name__}: {slow_error}", file=sys.stderr)
                
                # Check if it's a SentencePiece conversion error
                if "sentencepiece" in str(fast_error).lower() or "tiktoken" in str(fast_error).lower():
                    print(f"This appears to be a SentencePiece/Tiktoken tokenizer compatibility issue", file=sys.stderr)
                    print(f"Try installing required packages: pip install sentencepiece protobuf", file=sys.stderr)
                      # Try installing sentencepiece if not present
                    try:
                        import sentencepiece
                        print(f"SentencePiece is already installed", file=sys.stderr)
                    except ImportError:
                        print(f"Installing SentencePiece...", file=sys.stderr)
                        result = subprocess.run([sys.executable, "-m", "pip", "install", "sentencepiece"], 
                                              capture_output=True, text=True, check=True)
                        if result.stdout:
                            print(result.stdout, file=sys.stderr, end='')
                        if result.stderr:
                            print(result.stderr, file=sys.stderr, end='')
                        print(f"✓ SentencePiece installed", file=sys.stderr)
                        
                        # Try loading tokenizer again after installing sentencepiece
                        tokenizer = AutoTokenizer.from_pretrained(
                            model_id, 
                            trust_remote_code=params.get("trust_remote_code", True),
                            cache_dir="C:\\Users\\tanne\\Documents\\CSimple\\Resources\\HFModels",
                            resume_download=True,
                            use_fast=False
                        )
                        print(f"✓ Tokenizer loaded after installing SentencePiece", file=sys.stderr)
                else:
                    # If it's not a SentencePiece issue, try with additional fallback options
                    print(f"Trying final fallback tokenizer options...", file=sys.stderr)
                    try:
                        # Try with minimal options
                        tokenizer = AutoTokenizer.from_pretrained(
                            model_id,
                            use_fast=False,
                            trust_remote_code=False,  # Disable remote code as fallback
                            cache_dir="C:\\Users\\tanne\\Documents\\CSimple\\Resources\\HFModels"
                        )
                        print(f"✓ Tokenizer loaded with fallback options", file=sys.stderr)
                    except Exception as final_error:
                        print(f"All tokenizer loading attempts failed", file=sys.stderr)
                        print(f"Final error: {type(final_error).__name__}: {final_error}", file=sys.stderr)
                        raise final_error
        
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
            "cache_dir": "C:\\Users\\tanne\\Documents\\CSimple\\Resources\\HFModels" if not local_model_path else None,
            "resume_download": True if not local_model_path else False,
            "local_files_only": bool(local_model_path)
        }
        
        if device_map:
            model_kwargs["device_map"] = device_map
        
        if local_model_path and os.path.exists(local_model_path):
            print(f"Loading model from local path: {local_model_path}", file=sys.stderr)
        else:        print(f"Downloading/loading model files (this may take a while for first-time downloads)...", file=sys.stderr)
        print(f"Progress: Starting model download/load...", file=sys.stderr)
        
        # Only check HuggingFace cache if not using local model path
        if not local_model_path or not os.path.exists(local_model_path):
            # Check if model is already cached
            from huggingface_hub import try_to_load_from_cache
            try:
                # Try to check if model is already cached
                cached_file = try_to_load_from_cache(
                    model_id, 
                    "config.json",
                    cache_dir="C:\\Users\\tanne\\Documents\\CSimple\\Resources\\HFModels"
                )
                if cached_file:
                    print(f"Progress: Model found in cache, loading from disk...", file=sys.stderr)
                else:
                    print(f"Progress: Model not in cache, downloading from HuggingFace...", file=sys.stderr)
            except Exception:
                print(f"Progress: Checking cache status, downloading if needed...", file=sys.stderr)
        else:
            print(f"Progress: Loading model from local path: {local_model_path}", file=sys.stderr)
        
        # Load model with progress indication
        model = AutoModelForCausalLM.from_pretrained(model_path_to_use, **model_kwargs)
        
        print(f"✓ Model loaded successfully", file=sys.stderr)
        print(f"Progress: Model loading complete", file=sys.stderr)
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


def run_speech_recognition(model_id: str, input_text: str, params: Dict[str, Any], local_model_path: Optional[str] = None) -> str:
    """Run automatic speech recognition on audio files."""
    try:
        print(f"Processing speech recognition with model: {model_id}", file=sys.stderr)
        print(f"Raw input text received: {input_text}", file=sys.stderr)
        
        # Extract audio file path from input text
        audio_file_path = None
        
        # Handle multiple formats:
        # 1. Direct file path
        # 2. "audio file: [path]" format
        # 3. Combined ensemble format like "[Node Name]: C:\path\to\file.wav"
        
        if "audio file:" in input_text:
            # Extract the file path after "audio file:"
            parts = input_text.split("audio file:")
            if len(parts) > 1:
                audio_file_path = parts[1].strip()
        elif ".wav" in input_text or ".mp3" in input_text or ".m4a" in input_text or ".flac" in input_text:
            # Look for file paths in ensemble format [Node Name]: C:\path\to\file.ext
            import re
            # Find all file paths that end with audio extensions
            # Updated patterns to correctly handle Windows paths with drive letters
            audio_patterns = [
                r'\]:\s*([A-Z]:[^:]+\.(wav|mp3|m4a|flac|ogg|aac))',  # Match after ]: C:\path
                r':\s*([A-Z]:[^:\[\]]+\.(wav|mp3|m4a|flac|ogg|aac))',  # Match after : C:\path (but not inside brackets)
                r'([A-Z]:[^:\[\]]+\.(wav|mp3|m4a|flac|ogg|aac))'      # Direct match C:\path
            ]
            
            for pattern in audio_patterns:
                matches = re.findall(pattern, input_text, re.IGNORECASE)
                if matches:
                    # Take the first match (for ensemble, we use the first audio file)
                    if isinstance(matches[0], tuple):
                        audio_file_path = matches[0][0].strip()
                    else:
                        audio_file_path = matches[0].strip()
                    break
        else:
            # Try treating the entire input as a file path
            potential_path = input_text.strip()
            if os.path.exists(potential_path) and any(potential_path.lower().endswith(ext) for ext in ['.wav', '.mp3', '.m4a', '.flac', '.ogg', '.aac']):
                audio_file_path = potential_path
        
        print(f"Extracted audio file path: {audio_file_path}", file=sys.stderr)
        
        # If the extracted path doesn't exist, it might be a simulated segment path
        # Try to find the original audio file in the same directory
        if not audio_file_path or not os.path.exists(audio_file_path):
            if audio_file_path and "Segment_" in audio_file_path:
                print(f"Segment file not found, looking for original audio file in directory", file=sys.stderr)
                audio_dir = os.path.dirname(audio_file_path)
                if os.path.exists(audio_dir):
                    # Look for any .wav, .mp3, etc. files in the directory
                    for ext in ['.wav', '.mp3', '.m4a', '.flac', '.ogg', '.aac']:
                        for file in os.listdir(audio_dir):
                            if file.lower().endswith(ext) and not file.startswith("Segment_"):
                                fallback_path = os.path.join(audio_dir, file)
                                print(f"Found fallback audio file: {fallback_path}", file=sys.stderr)
                                audio_file_path = fallback_path
                                break
                        if audio_file_path and os.path.exists(audio_file_path):
                            break
            
            if not audio_file_path or not os.path.exists(audio_file_path):
                return f"ERROR: No valid audio file path found in input. Input received: {input_text}"
        
        print(f"Processing audio file: {audio_file_path}", file=sys.stderr)
        
        # Check if required audio processing libraries are available
        try:
            import librosa
            print("✓ librosa library available", file=sys.stderr)
        except ImportError:
            try:
                # Try installing librosa
                print("Installing librosa...", file=sys.stderr)
                subprocess.check_call([sys.executable, "-m", "pip", "install", "librosa"], 
                                    stdout=subprocess.DEVNULL, stderr=subprocess.PIPE)
                import librosa
                print("✓ librosa installed and imported", file=sys.stderr)
            except Exception as e:
                return f"ERROR: Failed to install/import librosa for audio processing: {e}"
        
        # Import transformers pipeline
        from transformers import pipeline
        
        # Determine model path
        model_path_to_use = local_model_path if local_model_path and os.path.exists(local_model_path) else model_id
        print(f"Using model path: {model_path_to_use}", file=sys.stderr)
        
        # Create speech recognition pipeline
        print("Creating speech recognition pipeline...", file=sys.stderr)
        
        # Configure pipeline arguments
        pipeline_kwargs = {
            "task": "automatic-speech-recognition",
            "model": model_path_to_use,
            "device": -1 if params.get("cpu_optimize", False) else 0  # Use CPU if cpu_optimize is True
        }
        
        # Only add trust_remote_code if it's not a local model path
        if not (local_model_path and os.path.exists(local_model_path)):
            pipeline_kwargs["trust_remote_code"] = params.get("trust_remote_code", True)
        
        pipe = pipeline(**pipeline_kwargs)
        
        print("Loading and processing audio file...", file=sys.stderr)
        
        # Load audio file
        try:
            audio_array, sampling_rate = librosa.load(audio_file_path, sr=16000)  # Whisper expects 16kHz
            print(f"Audio loaded: {len(audio_array)} samples at {sampling_rate}Hz", file=sys.stderr)
        except Exception as e:
            return f"ERROR: Failed to load audio file: {e}"
        
        # Process audio with the model
        print("Running speech recognition...", file=sys.stderr)
        result = pipe(audio_array)
        
        # Extract transcription text
        if isinstance(result, dict) and "text" in result:
            transcription = result["text"].strip()
        elif isinstance(result, list) and len(result) > 0 and "text" in result[0]:
            transcription = result[0]["text"].strip()
        else:
            transcription = str(result).strip()
        
        print(f"Transcription complete: {len(transcription)} characters", file=sys.stderr)
        
        if not transcription:
            return "No speech detected in the audio file."
        
        return f"Transcription: {transcription}"
        
    except Exception as e:
        error_msg = str(e)
        print(f"Error in speech recognition: {error_msg}", file=sys.stderr)
        
        if "librosa" in error_msg.lower():
            return "ERROR: Audio processing library not available. Please install librosa: pip install librosa"
        elif "cuda" in error_msg.lower() or "memory" in error_msg.lower():
            return "ERROR: Insufficient GPU memory for audio processing. Try using CPU mode."
        else:
            return f"ERROR: {error_msg}"


def run_image_to_text(model_id: str, input_text: str, params: Dict[str, Any], local_model_path: Optional[str] = None) -> str:
    """Run image-to-text processing on image files using BLIP and similar models."""
    try:
        print(f"Processing image-to-text with model: {model_id}", file=sys.stderr)
        print(f"Raw input text received: {input_text}", file=sys.stderr)
        
        # Extract image file path from input text
        image_file_path = None
        
        # Handle multiple formats:
        # 1. Direct file path
        # 2. "image file: [path]" format
        # 3. Combined ensemble format like "[Node Name]: C:\path\to\file.jpg"
        
        if "image file:" in input_text:
            # Extract the file path after "image file:"
            parts = input_text.split("image file:")
            if len(parts) > 1:
                image_file_path = parts[1].strip()
        elif any(ext in input_text.lower() for ext in ['.jpg', '.jpeg', '.png', '.bmp', '.gif', '.tiff', '.webp']):
            # Look for file paths in ensemble format [Node Name]: C:\path\to\file.ext
            import re
            # Find all file paths that end with image extensions
            # Updated patterns to correctly handle Windows paths with drive letters
            image_patterns = [
                r'\]:\s*([A-Z]:[^:]+\.(jpg|jpeg|png|bmp|gif|tiff|webp))',  # Match after ]: C:\path
                r':\s*([A-Z]:[^:\[\]]+\.(jpg|jpeg|png|bmp|gif|tiff|webp))',  # Match after : C:\path (but not inside brackets)
                r'([A-Z]:[^:\[\]]+\.(jpg|jpeg|png|bmp|gif|tiff|webp))'      # Direct match C:\path
            ]
            
            for pattern in image_patterns:
                matches = re.findall(pattern, input_text, re.IGNORECASE)
                if matches:
                    # Take the first match (for ensemble, we use the first image file)
                    if isinstance(matches[0], tuple):
                        image_file_path = matches[0][0].strip()
                    else:
                        image_file_path = matches[0].strip()
                    break
        else:
            # Try treating the entire input as a file path
            potential_path = input_text.strip()
            if os.path.exists(potential_path) and any(potential_path.lower().endswith(ext) for ext in ['.jpg', '.jpeg', '.png', '.bmp', '.gif', '.tiff', '.webp']):
                image_file_path = potential_path
        
        print(f"Extracted image file path: {image_file_path}", file=sys.stderr)
        
        if not image_file_path or not os.path.exists(image_file_path):
            return f"ERROR: No valid image file path found in input. Input received: {input_text}"
        
        print(f"Processing image file: {image_file_path}", file=sys.stderr)
        
        # Check if required image processing libraries are available
        try:
            from PIL import Image
            print("✓ PIL library available", file=sys.stderr)
        except ImportError:
            try:
                # Try installing Pillow
                print("Installing Pillow...", file=sys.stderr)
                subprocess.check_call([sys.executable, "-m", "pip", "install", "Pillow"], 
                                    stdout=subprocess.DEVNULL, stderr=subprocess.PIPE)
                from PIL import Image
                print("✓ Pillow installed and imported", file=sys.stderr)
            except Exception as e:
                return f"ERROR: Failed to install/import Pillow for image processing: {e}"
        
        # Import transformers components
        from transformers import AutoProcessor, BlipForConditionalGeneration
        
        # Determine model path
        model_path_to_use = local_model_path if local_model_path and os.path.exists(local_model_path) else model_id
        print(f"Using model path: {model_path_to_use}", file=sys.stderr)
        
        # Load processor and model
        print("Loading image processor and model...", file=sys.stderr)
        
        try:
            # Load processor
            processor = AutoProcessor.from_pretrained(
                model_path_to_use,
                local_files_only=bool(local_model_path and os.path.exists(local_model_path))
            )
            print("✓ Processor loaded", file=sys.stderr)
            
            # Load model with safetensors preference and fallback logic
            model_kwargs = {
                "torch_dtype": torch.float32 if params.get("cpu_optimize", False) else torch.float16,
                "device_map": "cpu" if params.get("cpu_optimize", False) else "auto",
                "local_files_only": bool(local_model_path and os.path.exists(local_model_path))
            }
            
            # Try loading with safetensors first for security
            try:
                print("Attempting to load model with safetensors...", file=sys.stderr)
                model_kwargs["use_safetensors"] = True
                model = BlipForConditionalGeneration.from_pretrained(model_path_to_use, **model_kwargs)
                print("✓ Model loaded with safetensors", file=sys.stderr)
            except Exception as safetensors_error:
                print(f"Safetensors loading failed: {safetensors_error}", file=sys.stderr)
                print("Attempting to load model with PyTorch format...", file=sys.stderr)
                
                # Fallback to PyTorch format if safetensors not available
                model_kwargs["use_safetensors"] = False
                try:
                    model = BlipForConditionalGeneration.from_pretrained(model_path_to_use, **model_kwargs)
                    print("✓ Model loaded with PyTorch format", file=sys.stderr)
                except Exception as pytorch_error:
                    # If both fail, provide helpful error message
                    error_msg = f"Failed to load model with both safetensors and PyTorch formats.\n"
                    error_msg += f"Safetensors error: {safetensors_error}\n"
                    error_msg += f"PyTorch error: {pytorch_error}\n"
                    error_msg += f"Consider upgrading PyTorch or ensuring the model files are compatible."
                    raise Exception(error_msg)
            
        except Exception as e:
            return f"ERROR: Failed to load model or processor: {e}"
        
        print("Loading and processing image file...", file=sys.stderr)
        
        # Load image file
        try:
            image = Image.open(image_file_path).convert("RGB")
            print(f"Image loaded: {image.size} pixels", file=sys.stderr)
        except Exception as e:
            return f"ERROR: Failed to load image file: {e}"
        
        # Process image with the model
        print("Running image captioning...", file=sys.stderr)
        
        # Prepare inputs
        inputs = processor(image, return_tensors="pt")
        
        # Generate caption
        with torch.no_grad():
            out = model.generate(**inputs, max_length=params.get("max_length", 50), num_beams=5)
        
        # Decode caption
        caption = processor.decode(out[0], skip_special_tokens=True)
        
        print(f"Caption generated: {len(caption)} characters", file=sys.stderr)
        
        if not caption:
            return "No caption could be generated for this image."
        
        return f"Image Caption: {caption}"
        
    except Exception as e:
        error_msg = str(e)
        print(f"Error in image-to-text processing: {error_msg}", file=sys.stderr)
        
        # Handle specific error types
        if "trust_remote_code" in error_msg.lower():
            return "ERROR: Model requires trust_remote_code=True but was blocked for security."
        elif "blip" in error_msg.lower():
            return f"ERROR: BLIP model processing failed: {error_msg}"
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
            valid_files = 0
            
            for model_dir in model_dirs:
                # Check snapshots directory for actual model files
                snapshots_dir = model_dir / "snapshots"
                if snapshots_dir.exists():
                    for snapshot_dir in snapshots_dir.iterdir():
                        if snapshot_dir.is_dir():
                            for file_path in snapshot_dir.iterdir():
                                if file_path.is_file():
                                    file_size = file_path.stat().st_size
                                    total_size += file_size
                                    file_count += 1
                                    if file_size > 0:  # Only count non-empty files
                                        valid_files += 1
                
                # Also check blobs directory for actual file content
                blobs_dir = model_dir / "blobs" 
                if blobs_dir.exists():
                    for blob_file in blobs_dir.iterdir():
                        if blob_file.is_file():
                            blob_size = blob_file.stat().st_size
                            if blob_size > 0:
                                valid_files += 1
            
            if valid_files > 0 and total_size > 1024:  # At least 1KB of actual data
                print(f"✓ Model '{model_id}' found in cache ({valid_files} valid files)", file=sys.stderr)
                print(f"  Cache size: {total_size / (1024*1024):.1f} MB", file=sys.stderr)
                return True
            else:
                print(f"⚠ Model '{model_id}' cache is corrupted or incomplete ({file_count} files, {valid_files} valid)", file=sys.stderr)
                print(f"  Will attempt fresh download...", file=sys.stderr)
                return False
        else:
            print(f"⬇ Model '{model_id}' not cached - will download from HuggingFace Hub", file=sys.stderr)
            return False

    except Exception as e:
        print(f"Note: Could not check cache status: {e}", file=sys.stderr)
        return False


def force_download_model(model_id: str) -> bool:
    """Force download a model, clearing any corrupted cache first"""
    try:
        from huggingface_hub import snapshot_download
        import shutil
        
        cache_dir = "C:\\Users\\tanne\\Documents\\CSimple\\Resources\\HFModels"
        model_hash = model_id.replace("/", "--")
        model_cache_path = Path(cache_dir) / f"models--{model_hash}"
        
        # Clear corrupted cache if it exists
        if model_cache_path.exists():
            print(f"Clearing corrupted cache for {model_id}...", file=sys.stderr)
            shutil.rmtree(model_cache_path, ignore_errors=True)
        
        print(f"Force downloading model {model_id}...", file=sys.stderr)
        print(f"Progress: Starting fresh download of {model_id}...", file=sys.stderr)
        
        # Download the model
        local_path = snapshot_download(
            repo_id=model_id,
            cache_dir=cache_dir,
            force_download=True,
            resume_download=False
        )
        
        print(f"✓ Model downloaded successfully to: {local_path}", file=sys.stderr)
        return True
        
    except Exception as e:
        print(f"Failed to download model {model_id}: {e}", file=sys.stderr)
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
        
        # Only check cache status and force download if no local model path is provided
        if not args.local_model_path or not os.path.exists(args.local_model_path):
            # Check model cache status
            cache_valid = check_model_cache_status(args.model_id)
            
            # If cache is invalid/corrupted, force download
            if not cache_valid:
                print(f"Model cache invalid, attempting fresh download...", file=sys.stderr)
                if not force_download_model(args.model_id):
                    print(f"ERROR: Failed to download model {args.model_id}", file=sys.stderr)
                    return 1
                print(f"Model download completed, proceeding with inference...", file=sys.stderr)
        else:
            print(f"Using provided local model path: {args.local_model_path}", file=sys.stderr)
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
            result = run_text_generation(args.model_id, args.input, params, args.local_model_path)
        elif model_type == "automatic-speech-recognition":
            result = run_speech_recognition(args.model_id, args.input, params, args.local_model_path)
        elif model_type == "image-to-text":
            result = run_image_to_text(args.model_id, args.input, params, args.local_model_path)
        else:
            result = f"Model type '{model_type}' not fully implemented yet. Basic response: Processed '{args.input}' with {args.model_id}"
        
        # Ensure clean output - strip any extra whitespace and ensure single line output for short responses
        clean_result = result.strip()
        
        # Print result to stdout with explicit flush to ensure it's not buffered
        print(clean_result, flush=True)
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
