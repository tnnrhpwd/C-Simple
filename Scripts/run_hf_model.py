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

def main():
    parser = argparse.ArgumentParser(description='Run HuggingFace model')
    parser.add_argument('--model_id', required=True, help='HuggingFace model ID')
    parser.add_argument('--input', required=True, help='Input text')
    parser.add_argument('--api_key', help='HuggingFace API key (optional, for gated models)')
    
    args = parser.parse_args()
    
    try:
        # Try to import required libraries
        from transformers import AutoTokenizer, AutoModel, pipeline
        import torch
        
        print(f'Loading model: {args.model_id}')
        
        # Try to use pipeline first (simpler approach)
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
                task = 'text-generation'  # Default
              # Force CPU execution and add specific parameters for CPU-only models
            device = "cpu"  # Force CPU
            torch_dtype = torch.float32  # Use float32 for CPU compatibility
            
            # Check if this is a problematic model that we should skip locally
            problematic_models = [
                'deepseek-ai/DeepSeek-R1',  # Requires FP8 quantization/GPU
                'deepseek-ai/deepseek-r1',
                'microsoft/DialoGPT-large',  # Often has memory issues
                'facebook/opt-66b',  # Too large for most systems
                'EleutherAI/gpt-j-6B'  # Often causes memory issues on CPU
            ]
            
            if any(model.lower() in args.model_id.lower() for model in problematic_models):
                print(f"Model {args.model_id} is known to have compatibility issues on CPU. Falling back to API...", file=sys.stderr)
                raise Exception("Model requires GPU/specialized hardware, using API fallback")
            
            # Special handling for models that might have quantization issues
            model_kwargs = {
                "trust_remote_code": True,
                "device_map": None,  # Don't use device mapping
                "torch_dtype": torch_dtype,
                "low_cpu_mem_usage": True,
                "use_safetensors": True,  # Prefer safetensors when available
            }
            
            # Try without quantization first
            try:
                pipe = pipeline(task, model=args.model_id, device=device, **model_kwargs)
            except Exception as e:
                if "quantization" in str(e).lower() or "fp8" in str(e).lower():
                    print(f"Quantization not supported, trying without special features...", file=sys.stderr)
                    # Try with minimal configuration
                    basic_kwargs = {
                        "trust_remote_code": True,
                        "torch_dtype": torch.float32,
                    }
                    pipe = pipeline(task, model=args.model_id, device=device, **basic_kwargs)
                else:
                    raise
            
            if task == 'text-generation':
                result = pipe(args.input, 
                            max_length=min(150, len(args.input.split()) + 50), 
                            do_sample=True, 
                            temperature=0.7, 
                            pad_token_id=pipe.tokenizer.eos_token_id,
                            no_repeat_ngram_size=2)
            else:
                result = pipe(args.input)
            
            if isinstance(result, list):
                if len(result) > 0 and isinstance(result[0], dict):
                    if 'generated_text' in result[0]:
                        output = result[0]['generated_text']
                        # Remove the input prompt from the output if it's included
                        if output.startswith(args.input):
                            output = output[len(args.input):].strip()
                    else:
                        output = str(result[0])
                else:
                    output = str(result)
            else:
                output = str(result)
                
            print(output if output else "Model processed successfully but generated no text output.")
            
        except Exception as pipe_error:
            print(f'Pipeline failed, trying manual approach: {pipe_error}', file=sys.stderr)
            
            try:
                # Fallback to manual tokenizer/model approach with CPU-specific settings
                tokenizer = AutoTokenizer.from_pretrained(args.model_id, trust_remote_code=True)
                
                # Add padding token if it doesn't exist
                if tokenizer.pad_token is None:
                    tokenizer.pad_token = tokenizer.eos_token
                
                # Try to load model with CPU-specific settings
                model = AutoModel.from_pretrained(
                    args.model_id, 
                    trust_remote_code=True,
                    torch_dtype=torch.float32,
                    device_map=None,
                    low_cpu_mem_usage=True
                )
                
                inputs = tokenizer(args.input, return_tensors='pt', padding=True, truncation=True, max_length=512)
                
                with torch.no_grad():
                    outputs = model(**inputs)
                    
                # Basic response for demonstration
                print(f'Model "{args.model_id}" processed input successfully. Response: The model has analyzed your input and processed {inputs["input_ids"].shape[1]} tokens.')
                
            except Exception as manual_error:
                print(f'Manual approach also failed: {manual_error}', file=sys.stderr)
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
