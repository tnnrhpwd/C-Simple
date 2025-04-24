#!/usr/bin/env python3
"""
API-Only Runtime for C-Simple

A minimal script that uses only Python standard library to interact with 
Hugging Face Inference API without requiring any additional Python packages.

Usage:
  python api_runtime.py --model_id "<model_id>" --input "<input_text>"
"""

import argparse
import json
import sys
import urllib.request
import urllib.parse
import urllib.error
import ssl
import time
from datetime import datetime

def parse_arguments():
    parser = argparse.ArgumentParser(description="C-Simple API-Only Runtime")
    parser.add_argument("--model_id", type=str, required=True, help="HuggingFace model ID")
    parser.add_argument("--input", type=str, required=True, help="Input text or data")
    parser.add_argument("--api_key", type=str, help="Optional HuggingFace API key")
    parser.add_argument("--timeout", type=int, default=30, help="Request timeout in seconds")
    return parser.parse_args()

def call_huggingface_api(model_id, inputs, api_key=None, timeout=30):
    """Call the Hugging Face Inference API with just standard library"""
    api_url = f"https://api-inference.huggingface.co/models/{model_id}"
    
    payload = {"inputs": inputs}
    data = json.dumps(payload).encode('utf-8')
    
    headers = {
        'Content-Type': 'application/json'
    }
    
    if api_key:
        headers['Authorization'] = f'Bearer {api_key}'
    
    # Create a context that doesn't verify SSL (for environments with issues)
    ssl_context = ssl._create_unverified_context()
    
    try:
        req = urllib.request.Request(api_url, data=data, headers=headers, method='POST')
        with urllib.request.urlopen(req, timeout=timeout, context=ssl_context) as response:
            response_data = response.read().decode('utf-8')
            return json.loads(response_data)
    except urllib.error.HTTPError as e:
        if e.code == 429:
            # Model is loading, wait and retry
            print("Model is loading, waiting...", file=sys.stderr)
            time.sleep(20)
            return call_huggingface_api(model_id, inputs, api_key, timeout)
        else:
            error_body = e.read().decode('utf-8')
            print(f"HTTP Error: {e.code} - {error_body}", file=sys.stderr)
            return {"error": f"HTTP Error {e.code}", "details": error_body}
    except Exception as e:
        print(f"API request error: {str(e)}", file=sys.stderr)
        return {"error": str(e)}

def extract_text_from_api_response(response):
    """Extract generated text from API response based on response structure"""
    if isinstance(response, list) and len(response) > 0:
        # Case 1: List of results with generated_text field
        if isinstance(response[0], dict) and "generated_text" in response[0]:
            return response[0]["generated_text"]
        
    elif isinstance(response, dict):
        # Case 2: Single result with generated_text field
        if "generated_text" in response:
            return response["generated_text"]
            
    # Case 3: Other response structure, just convert to string
    return str(response)

def main():
    try:
        args = parse_arguments()
        print(f"Calling API for model: {args.model_id}", file=sys.stderr)
        
        # Make API call
        result = call_huggingface_api(args.model_id, args.input, args.api_key, args.timeout)
        
        if "error" in result:
            print(f"API Error: {result['error']}", file=sys.stderr)
            return 1
        
        # Extract and print the response text
        output_text = extract_text_from_api_response(result)
        print(output_text)
        return 0
    
    except Exception as e:
        print(f"Error: {str(e)}", file=sys.stderr)
        return 1

if __name__ == "__main__":
    sys.exit(main())
