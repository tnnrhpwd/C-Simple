#!/usr/bin/env python3
import argparse
import sys
import json
import traceback
import urllib.request
import urllib.parse
import urllib.error
import ssl
import time
import os
from pathlib import Path

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

def progress_callback():
    """Simple progress indicator for model downloads"""
    print(".", end="", flush=True, file=sys.stderr)

def setup_download_progress_monitoring():
    """Set up environment variables to show download progress"""
    # Enable HuggingFace Hub progress bars
    os.environ['HF_HUB_ENABLE_HF_TRANSFER'] = '0'  # Disable faster downloads to see progress
    os.environ['TRANSFORMERS_VERBOSITY'] = 'info'  # Show more detailed loading info

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
                print("CUDA compiled version:", torch.version.cuda, file=sys.stderr)
                print("This suggests CUDA is compiled but may not be properly configured.", file=sys.stderr)
            else:
                print("CUDA compiled version: None (CPU-only PyTorch)", file=sys.stderr)
                print("You have a CPU-only version of PyTorch installed.", file=sys.stderr)
            
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

def main():
    parser = argparse.ArgumentParser(description='Run HuggingFace model')
    parser.add_argument('--model_id', required=True, help='HuggingFace model ID')
    parser.add_argument('--input', required=True, help='Input text')
    parser.add_argument('--api_key', help='HuggingFace API key (optional, for gated models)')
    parser.add_argument('--cpu_optimize', action='store_true', help='Optimize for CPU execution')
    parser.add_argument('--max_length', type=int, default=150, help='Maximum length of generated text')
    parser.add_argument('--temperature', type=float, default=0.7, help='Sampling temperature (0.1-1.0)')
    parser.add_argument('--offline_mode', action='store_true', help='Disable API fallback, only run local models')
    
    args = parser.parse_args()
    
    try:
        # Try to import required libraries
        from transformers import AutoTokenizer, AutoModel, pipeline
        import torch
        
        print(f'Loading model: {args.model_id}')
        
        # Set up download progress monitoring
        setup_download_progress_monitoring()
        
        # Check if model is cached or needs to be downloaded
        is_cached = check_model_cache_status(args.model_id)
        if not is_cached:
            print(f"Downloading model '{args.model_id}' from HuggingFace Hub...", file=sys.stderr)
            print("This may take several minutes depending on model size and internet speed.", file=sys.stderr)
            print("Progress: ", end="", file=sys.stderr)          # Try to use pipeline first (simpler approach)
        try:
            # Determine task type based on model ID
            if 'gpt' in args.model_id.lower() or 'llama' in args.model_id.lower() or 'deepseek' in args.model_id.lower():
                task = 'text-generation'
            elif 'bert' in args.model_id.lower():
                task = 'fill-mask'
            elif 'whisper' in args.model_id.lower():
                task = 'automatic-speech-recognition'
            elif 'clip' in args.model_id.lower() or 'vit' in args.model_id.lower():
                task = 'image-classification'
            else:
                task = 'text-generation'  # Default                  # Detect available hardware and choose appropriate device
            device = "cpu"  # Default to CPU
            torch_dtype = torch.float32  # Default to float32 for CPU compatibility
              
            # Enhanced GPU detection with detailed diagnostics
            try:
                if torch.cuda.is_available():
                    device = "cuda"
                    torch_dtype = torch.float16  # Use float16 for GPU efficiency
                    gpu_name = torch.cuda.get_device_name(0)
                    compute_capability = torch.cuda.get_device_capability(0)
                    print(f"GPU detected: {gpu_name}", file=sys.stderr)
                    print(f"CUDA version: {torch.version.cuda}", file=sys.stderr)
                    print(f"Compute capability: {compute_capability[0]}.{compute_capability[1]}", file=sys.stderr)
                    print(f"Using GPU acceleration for model: {args.model_id}", file=sys.stderr)
                    
                    # Check for FP8 compatibility
                    if compute_capability[0] < 8 or (compute_capability[0] == 8 and compute_capability[1] < 9):
                        print(f"Note: GPU compute capability {compute_capability[0]}.{compute_capability[1]} < 8.9. FP8 quantization will be disabled.", file=sys.stderr)
                else:
                    print(f"No GPU detected, using CPU for model: {args.model_id}", file=sys.stderr)
                    print(f"PyTorch version: {torch.__version__}", file=sys.stderr)
                    print(f"CUDA compiled version: {torch.version.cuda if torch.version.cuda else 'None (CPU-only PyTorch)'}", file=sys.stderr)
                    print("Note: If you have an NVIDIA GPU, you may need to install PyTorch with CUDA support.", file=sys.stderr)
            except Exception as gpu_error:
                print(f"Error detecting GPU: {gpu_error}", file=sys.stderr)
                print(f"Falling back to CPU execution", file=sys.stderr)
            
            # Check if this is a GPU-preferred model running on CPU
            gpu_preferred_models = [
                'deepseek-ai/DeepSeek-R1',
                'deepseek-ai/deepseek-r1',
                'microsoft/DialoGPT-large',
                'facebook/opt-66b',
                'EleutherAI/gpt-j-6B'
            ]
            
            is_gpu_preferred = any(model.lower() in args.model_id.lower() for model in gpu_preferred_models)
            
            if is_gpu_preferred and device == "cpu":
                print(f"Warning: Model {args.model_id} prefers GPU acceleration but only CPU is available.", file=sys.stderr)
                print("Performance may be slower. Consider using a CPU-friendly model like 'gpt2' or 'distilgpt2'.", file=sys.stderr)              # Enhanced model configuration based on available hardware
            model_kwargs = {
                "trust_remote_code": True,
                "torch_dtype": torch_dtype,
                "use_safetensors": True,  # Prefer safetensors when available
            }
            
            # Configure device mapping based on available hardware
            if device == "cuda":
                model_kwargs["device_map"] = "auto"  # Let transformers handle GPU mapping
                # Disable FP8 quantization for GPUs with compute capability < 8.9 (like RTX 3090)
                model_kwargs["attn_implementation"] = "flash_attention_2" if torch.cuda.get_device_capability()[0] >= 8 else "eager"
                # Force disable quantization for older GPUs
                model_kwargs["quantization_config"] = None
            else:
                model_kwargs["device_map"] = None  # Don't use device mapping for CPU
            
            # Add CPU-specific optimizations if requested
            if args.cpu_optimize:
                model_kwargs.update({
                    "low_cpu_mem_usage": True,
                    "torch_dtype": torch.float32,  # Ensure float32 for CPU
                })
                print(f"CPU optimization enabled for model {args.model_id}", file=sys.stderr)
            else:
                model_kwargs["low_cpu_mem_usage"] = True# Try without quantization first
            try:
                if device == "cuda":
                    # Don't specify device when using device_map to avoid conflicts
                    print("Creating GPU pipeline...", file=sys.stderr)
                    pipe = pipeline(task, model=args.model_id, **model_kwargs)
                    print(f"✓ Model '{args.model_id}' loaded successfully on GPU", file=sys.stderr)
                else:
                    print("Creating CPU pipeline...", file=sys.stderr)
                    pipe = pipeline(task, model=args.model_id, device=-1, **model_kwargs)  # Use CPU (-1)
                    print(f"✓ Model '{args.model_id}' loaded successfully on CPU", file=sys.stderr)
                
                if not is_cached:
                    print(f"\n✓ Model download and caching completed for '{args.model_id}'", file=sys.stderr)
            except Exception as e:
                if "quantization" in str(e).lower() or "fp8" in str(e).lower() or "compute capability" in str(e).lower():
                    print(f"Quantization not supported on this GPU, trying without quantization features...", file=sys.stderr)                    # Try with minimal configuration for RTX 3090 compatibility
                    basic_kwargs = {
                        "trust_remote_code": True,
                        "torch_dtype": torch.float16 if device == "cuda" else torch.float32,
                        "device_map": "auto" if device == "cuda" else None,
                        "quantization_config": None,  # Explicitly disable quantization
                    }
                    if device == "cuda":
                        print("Retrying with basic GPU configuration...", file=sys.stderr)
                        pipe = pipeline(task, model=args.model_id, **basic_kwargs)
                        print(f"✓ Model '{args.model_id}' loaded successfully on GPU (basic config)", file=sys.stderr)
                    else:
                        print("Retrying with basic CPU configuration...", file=sys.stderr)
                        pipe = pipeline(task, model=args.model_id, device=-1, **basic_kwargs)
                        print(f"✓ Model '{args.model_id}' loaded successfully on CPU (basic config)", file=sys.stderr)
                    
                    if not is_cached:
                        print(f"\n✓ Model download and caching completed for '{args.model_id}'", file=sys.stderr)
                else:
                    raise            
            if task == 'text-generation':
                # Use dynamic parameters from command line arguments
                generation_kwargs = {
                    'max_length': min(args.max_length, len(args.input.split()) + args.max_length),
                    'do_sample': True,
                    'temperature': args.temperature,
                    'pad_token_id': pipe.tokenizer.eos_token_id,
                    'no_repeat_ngram_size': 2,
                    'truncation': True,  # Add explicit truncation
                }
                
                # Additional CPU optimization settings
                if args.cpu_optimize:
                    generation_kwargs.update({
                        'num_beams': 1,  # Faster generation on CPU
                        'max_new_tokens': min(100, args.max_length),  # Limit tokens for speed
                    })
                
                print("Generating text with pipeline...", file=sys.stderr)
                result = pipe(args.input, **generation_kwargs)
            else:
                result = pipe(args.input)
            
            if isinstance(result, list):
                if len(result) > 0 and isinstance(result[0], dict):
                    if 'generated_text' in result[0]:
                        output = result[0]['generated_text']
                        # Remove the input prompt from the output if it's included
                        if output.startswith(args.input):
                            output = output[len(args.input):].strip()
                        # Clean up extra whitespace and newlines
                        output = ' '.join(output.split())
                    else:
                        output = str(result[0])
                else:
                    output = str(result)
            else:
                output = str(result)
                  # Ensure we have meaningful output
            if output and len(output.strip()) > 0:
                print(output.strip())
            else:
                print("Model processed successfully but generated no text output.")
            
        except Exception as pipe_error:
            print(f'Pipeline failed, trying manual approach: {pipe_error}', file=sys.stderr)
            
            try:
                # Fallback to manual tokenizer/model approach with CPU-specific settings
                print("Trying manual tokenizer/model loading approach...", file=sys.stderr)
                tokenizer = AutoTokenizer.from_pretrained(args.model_id, trust_remote_code=True)
                print("✓ Tokenizer loaded successfully", file=sys.stderr)
                
                # Add padding token if it doesn't exist
                if tokenizer.pad_token is None:
                    tokenizer.pad_token = tokenizer.eos_token                # Try to load model with hardware-appropriate settings
                model_load_kwargs = {
                    "trust_remote_code": True,
                    "torch_dtype": torch_dtype,
                    "low_cpu_mem_usage": True,
                    "quantization_config": None,  # Explicitly disable quantization
                }
                
                # Configure device mapping based on available hardware
                if device == "cuda":
                    model_load_kwargs["device_map"] = "auto"
                    # Disable attention optimizations that might cause issues on RTX 3090
                    model_load_kwargs["attn_implementation"] = "eager"
                else:
                    model_load_kwargs["device_map"] = None                
                print("Loading model with manual approach...", file=sys.stderr)
                
                # For text generation models, we need to use AutoModelForCausalLM
                try:
                    from transformers import AutoModelForCausalLM
                    model = AutoModelForCausalLM.from_pretrained(args.model_id, **model_load_kwargs)
                    print("✓ Model loaded successfully (manual approach)", file=sys.stderr)
                    
                    if not is_cached:
                        print(f"\n✓ Model download and caching completed for '{args.model_id}'", file=sys.stderr)
                    
                    # Prepare inputs for text generation
                    inputs = tokenizer(args.input, return_tensors='pt', padding=True, truncation=True, max_length=512)
                    
                    # Move inputs to the same device as the model
                    if device == "cuda" and torch.cuda.is_available():
                        inputs = {k: v.to("cuda") for k, v in inputs.items()}
                    
                    # Generate text
                    print("Generating text...", file=sys.stderr)
                    with torch.no_grad():
                        # Use generate method for text generation
                        generated_ids = model.generate(
                            inputs["input_ids"],
                            attention_mask=inputs.get("attention_mask"),
                            max_length=min(args.max_length, len(args.input.split()) + args.max_length),
                            do_sample=True,
                            temperature=args.temperature,
                            pad_token_id=tokenizer.eos_token_id,
                            no_repeat_ngram_size=2,
                            num_return_sequences=1
                        )
                        
                        # Decode the generated text
                        generated_text = tokenizer.decode(generated_ids[0], skip_special_tokens=True)
                        
                        # Remove the input prompt from the output if it's included
                        if generated_text.startswith(args.input):
                            output = generated_text[len(args.input):].strip()
                        else:
                            output = generated_text.strip()
                        
                        # Clean up extra whitespace and newlines
                        output = ' '.join(output.split())
                        
                        if output and len(output.strip()) > 0:
                            print(output.strip())
                        else:
                            print("Model generated empty response. This may be normal for some inputs or model configurations.")
                            
                except ImportError:
                    # Fallback to AutoModel if AutoModelForCausalLM is not available
                    print("AutoModelForCausalLM not available, using AutoModel fallback...", file=sys.stderr)
                    model = AutoModel.from_pretrained(args.model_id, **model_load_kwargs)
                    print("✓ Model loaded successfully (manual approach)", file=sys.stderr)
                    
                    if not is_cached:
                        print(f"\n✓ Model download and caching completed for '{args.model_id}'", file=sys.stderr)
                    
                    inputs = tokenizer(args.input, return_tensors='pt', padding=True, truncation=True, max_length=512)
                    
                    # Move inputs to the same device as the model
                    if device == "cuda" and torch.cuda.is_available():
                        inputs = {k: v.to("cuda") for k, v in inputs.items()}
                    
                    with torch.no_grad():
                        outputs = model(**inputs)
                        
                    # Basic response for demonstration
                    print(f'Model "{args.model_id}" processed input successfully. Response: The model has analyzed your input and processed {inputs["input_ids"].shape[1]} tokens.')
                
            except Exception as manual_error:
                print(f'Manual approach also failed: {manual_error}', file=sys.stderr)
                
                if args.offline_mode:
                    print('ERROR: All local execution methods failed and offline mode is enabled (API fallback disabled).', file=sys.stderr)
                    print(f"Model '{args.model_id}' cannot be run locally on this system.", file=sys.stderr)
                      # Check if this is a GPU-related issue and provide specific guidance
                    is_gpu_preferred = any(model.lower() in args.model_id.lower() for model in [
                        'deepseek-ai/DeepSeek-R1', 'deepseek-ai/deepseek-r1',
                        'microsoft/DialoGPT-large', 'facebook/opt-66b', 'EleutherAI/gpt-j-6B'
                    ])
                    
                    if is_gpu_preferred and ("compute capability" in str(manual_error) or "FP8" in str(manual_error)):
                        print(f"\nDETECTED: {args.model_id} requires FP8 quantization (compute capability 8.9+)", file=sys.stderr)
                        print("Your GPU was detected but doesn't support FP8 quantization.", file=sys.stderr)
                        print("\nAlternative solutions:", file=sys.stderr)
                        print("1. Use the model via API (switch to online mode)", file=sys.stderr)
                        print("2. Try a different DeepSeek model variant without FP8 requirements", file=sys.stderr)
                        print("3. Use a CPU-friendly model like 'gpt2' or 'distilgpt2'", file=sys.stderr)
                    elif is_gpu_preferred and "No GPU or XPU found" in str(manual_error):
                        print(f"\nDETECTED: {args.model_id} requires GPU acceleration but no GPU was found.", file=sys.stderr)
                        check_and_suggest_cuda_setup()
                    else:
                        print(f"\nModel execution failed: {str(manual_error)[:200]}...", file=sys.stderr)
                    
                    print("\nTo resolve this issue:", file=sys.stderr)
                    print("1. Try a CPU-friendly model like 'gpt2' or 'distilgpt2'", file=sys.stderr)
                    print("2. Switch to online mode to enable API fallback", file=sys.stderr)
                    print("3. Install missing dependencies if any were reported", file=sys.stderr)
                    if is_gpu_preferred:
                        print("4. Install PyTorch with CUDA support (see instructions above)", file=sys.stderr)
                    sys.exit(1)
                else:
                    print('Falling back to HuggingFace API...', file=sys.stderr)
                
                    # Final fallback to HuggingFace API
                    api_result = call_huggingface_api(args.model_id, args.input, args.api_key)
                    if "error" not in api_result:
                        output = extract_text_from_api_response(api_result)
                        print(output)
                    else:
                        # If API also fails, provide specific guidance based on error type
                        error_msg = api_result.get('error', 'Unknown error')
                        error_type = api_result.get('error_type', '')
                        
                        if error_type == 'authentication':
                            print(f"AUTHENTICATION_REQUIRED: {error_msg}")
                            print(f"\nModel '{args.model_id}' requires a HuggingFace API key.")
                            print("This is likely a gated model that requires permission to access.")
                            print("\nTo resolve this:")
                            print("1. Visit: https://huggingface.co/settings/tokens")
                            print("2. Create a new token (read access is sufficient)")
                            print("3. Re-run with: --api_key YOUR_TOKEN")
                            print(f"4. Or try a public model like 'gpt2' or 'distilgpt2'")
                        else:
                            print(f"All execution methods failed. API error: {error_msg}")
                            
                            # Suggest CPU-friendly alternatives
                            cpu_friendly_models = [
                                "microsoft/DialoGPT-medium",
                                "gpt2",
                                "distilgpt2", 
                                "microsoft/DialoGPT-small",
                                "huggingface/CodeBERTa-small-v1"
                            ]
                            
                            print("\nSuggested CPU-friendly alternatives:")
                            for model in cpu_friendly_models:
                                print(f"  - {model}")
                            
                            print(f"\nTo use model '{args.model_id}':")
                            print("1. Get a HuggingFace API key from: https://huggingface.co/settings/tokens")
                            print("2. Run: python run_hf_model.py --model_id 'your-model' --input 'your-text' --api_key 'your-key'")
                        sys.exit(1)
            
    except ImportError as e:
        print(f'ERROR: Missing required packages. Please install with: pip install transformers torch')
        print(f'Details: {e}')
        sys.exit(1)
    except Exception as e:
        print(f'ERROR: {e}')
        traceback.print_exc()
        sys.exit(1)

if __name__ == '__main__':
    main()
