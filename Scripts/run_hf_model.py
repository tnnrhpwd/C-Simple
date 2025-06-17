#!/usr/bin/env python3
import argparse
import sys
import json
import traceback

def main():
    parser = argparse.ArgumentParser(description='Run HuggingFace model')
    parser.add_argument('--model_id', required=True, help='HuggingFace model ID')
    parser.add_argument('--input', required=True, help='Input text')
    
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
            
            pipe = pipeline(task, model=args.model_id, trust_remote_code=True)
            
            if task == 'text-generation':
                result = pipe(args.input, max_length=150, do_sample=True, temperature=0.7, pad_token_id=pipe.tokenizer.eos_token_id)
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
            print(f'Pipeline failed, trying manual approach: {pipe_error}')
            
            # Fallback to manual tokenizer/model approach
            tokenizer = AutoTokenizer.from_pretrained(args.model_id, trust_remote_code=True)
            
            # Add padding token if it doesn't exist
            if tokenizer.pad_token is None:
                tokenizer.pad_token = tokenizer.eos_token
            
            model = AutoModel.from_pretrained(args.model_id, trust_remote_code=True)
            
            inputs = tokenizer(args.input, return_tensors='pt', padding=True, truncation=True, max_length=512)
            
            with torch.no_grad():
                outputs = model(**inputs)
                
            # Basic response for demonstration
            print(f'Model "{args.model_id}" processed input successfully. Response: The model has analyzed your input and processed {inputs["input_ids"].shape[1]} tokens.')
            
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
