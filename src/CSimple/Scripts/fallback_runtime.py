#!/usr/bin/env python3
"""
Fallback Runtime Script for C-Simple

This script provides a minimal interface for AI model inferencing without requiring 
additional Python packages. It can interact with models through REST APIs instead of local execution.

Usage:
  python fallback_runtime.py --mode api --model_id "<model_id>" --input "<input_text>"
"""

import argparse
import json
import sys
import urllib.request
import urllib.parse
import urllib.error
import ssl
import os
import base64
from datetime import datetime
import socket
import time

def parse_arguments():
    parser = argparse.ArgumentParser(description="C-Simple Fallback Runtime")
    parser.add_argument("--mode", choices=["api", "info", "check"], default="api", 
                      help="Mode: api (use REST API), info (get system info), or check (check dependencies)")
    parser.add_argument("--model_id", type=str, help="Model ID to use")
    parser.add_argument("--input", type=str, help="Input text or data")
    parser.add_argument("--api_key", type=str, help="API key for external services")
    parser.add_argument("--timeout", type=int, default=30, help="Timeout in seconds")
    return parser.parse_args()

def get_system_info():
    """Get system and Python information for diagnostics"""
    info = {
        "python_version": sys.version,
        "platform": sys.platform,
        "executable": sys.executable,
        "timestamp": datetime.now().isoformat(),
    }
    
    # Check for common packages
    packages = ["transformers", "torch", "numpy", "requests"]
    available_packages = {}
    
    for pkg in packages:
        try:
            module = __import__(pkg)
            available_packages[pkg] = getattr(module, "__version__", "unknown")
        except ImportError:
            available_packages[pkg] = None
    
    info["available_packages"] = available_packages
    
    # Check internet connectivity
    try:
        socket.create_connection(("www.google.com", 80), timeout=3)
        info["internet_connectivity"] = True
    except (socket.timeout, socket.error):
        info["internet_connectivity"] = False
    
    return info

def check_dependencies():
    """Check if required packages are installed"""
    needed_packages = ["transformers", "torch"]
    missing = []
    
    for pkg in needed_packages:
        try:
            __import__(pkg)
        except ImportError:
            missing.append(pkg)
    
    return {
        "success": len(missing) == 0,
        "missing_packages": missing
    }

def call_huggingface_api(model_id, inputs, api_key=None, timeout=30):
    """Call the Hugging Face Inference API"""
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
    args = parse_arguments()
    
    if args.mode == "info":
        info = get_system_info()
        print(json.dumps(info, indent=2))
        return 0
    
    elif args.mode == "check":
        result = check_dependencies()
        print(json.dumps(result, indent=2))
        return 0 if result["success"] else 1
    
    elif args.mode == "api":
        if not args.model_id or not args.input:
            print("Error: model_id and input are required for API mode", file=sys.stderr)
            return 1
            
        try:
            # Try to use the Hugging Face Inference API
            print(f"Calling Hugging Face API for model: {args.model_id}", file=sys.stderr)
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
    
    return 0

if __name__ == "__main__":
    sys.exit(main())
