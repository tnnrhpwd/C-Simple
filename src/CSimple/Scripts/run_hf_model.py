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
import re
import logging
from pathlib import Path
from typing import Dict, Any, Optional
import importlib.util

# Will be imported after environment setup
torch = None
transformers = None

# Global model cache to avoid reloading models
_model_cache = {}
_tokenizer_cache = {}

# Global environment setup flag to avoid repeated setup
_environment_setup_done = False

# Pre-compiled regex patterns for performance
_audio_patterns = [
    re.compile(r'\]:\s*([A-Z]:[^:]+\.(wav|mp3|m4a|flac|ogg|aac))', re.IGNORECASE),
    re.compile(r':\s*([A-Z]:[^:\[\]]+\.(wav|mp3|m4a|flac|ogg|aac))', re.IGNORECASE),
    re.compile(r'([A-Z]:[^:\[\]]+\.(wav|mp3|m4a|flac|ogg|aac))', re.IGNORECASE)
]


def progress_callback(filename: str, current: int, total: int):
    """Minimal progress callback for HuggingFace downloads."""
    # Minimal logging for performance - only log at completion
    if total > 0 and current >= total:
        print(f"✓ Downloaded: {filename}", file=sys.stderr)


def parse_arguments() -> argparse.Namespace:
    """Parse command line arguments."""
    parser = argparse.ArgumentParser(description="Run inference using HuggingFace models")
    parser.add_argument("--model_id", type=str, required=True, help="HuggingFace model ID")
    parser.add_argument("--input", type=str, required=True, help="Input text for the model")
    parser.add_argument("--max_length", type=int, default=100, help="Maximum length of generated text (capped at 100 tokens)")
    parser.add_argument("--temperature", type=float, default=0.7, help="Temperature for sampling")
    parser.add_argument("--top_p", type=float, default=0.9, help="Top-p sampling parameter")
    parser.add_argument("--trust_remote_code", action="store_true", default=True, help="Trust remote code")
    parser.add_argument("--cpu_optimize", action="store_true", help="Force CPU optimization mode")
    parser.add_argument("--offline_mode", action="store_true", help="Force offline mode (no API fallback)")
    parser.add_argument("--local_model_path", type=str, help="Local path to model directory (overrides model_id for loading)")
    parser.add_argument("--fast_mode", action="store_true", help="Enable fast mode with minimal output and optimizations")
    parser.add_argument("--preload_models", type=str, nargs="*", help="Pre-load models into cache for faster subsequent runs")
    parser.add_argument("--batch_size", type=int, default=1, help="Batch size for processing multiple inputs")
    return parser.parse_args()


def check_and_install_package(package_name: str) -> bool:
    """Check if a package is installed, and try to install it if not."""
    if importlib.util.find_spec(package_name) is not None:
        # Skip printing for speed - most packages should already be installed
        return True
    
    print(f"Installing {package_name}...", file=sys.stderr)
    try:
        # Use --quiet flag to reduce output
        result = subprocess.run([sys.executable, "-m", "pip", "install", "--quiet", package_name], 
                              capture_output=True, text=True, check=True)
        return True
    except subprocess.CalledProcessError as e:
        print(f"Failed to install {package_name}: {e.stderr}", file=sys.stderr)
        return False


def setup_environment() -> bool:
    """Set up the environment with all required packages - optimized for repeated calls."""
    global _environment_setup_done, torch, transformers
    
    # Skip setup if already done (for repeated model executions)
    if _environment_setup_done:
        return True
    
    # Set up the cache directory BEFORE importing transformers
    cache_dir = "C:\\Users\\tanne\\Documents\\CSimple\\Resources\\HFModels"
    os.makedirs(cache_dir, exist_ok=True)
    os.environ["TRANSFORMERS_CACHE"] = cache_dir
    os.environ["HF_HOME"] = cache_dir
    os.environ["HF_HUB_DISABLE_SYMLINKS_WARNING"] = "1"
    # Disable progress bars for faster loading
    os.environ["HF_HUB_ENABLE_HF_TRANSFER"] = "0"
    # Reduce verbosity for speed
    os.environ["TRANSFORMERS_VERBOSITY"] = "error"
    # Additional optimizations
    os.environ["TOKENIZERS_PARALLELISM"] = "false"  # Avoid threading overhead
    os.environ["HF_HUB_CACHE"] = cache_dir
    
    # Quick check for core packages (avoid slow imports if already available)
    required_packages = {
        "transformers": "transformers",
        "torch": "torch", 
        "accelerate": "accelerate",  # Required for quantized models
        "protobuf": "protobuf",  # Required for many HuggingFace models
        "sentencepiece": "sentencepiece",  # Required for SentencePiece tokenizers
        "safetensors": "safetensors"  # Required for secure model loading
    }
    
    # Fast package availability check
    missing_packages = []
    for package_name, pip_name in required_packages.items():
        if importlib.util.find_spec(package_name) is None:
            missing_packages.append(pip_name)
    
    # Only install missing packages
    if missing_packages:
        print(f"Installing missing packages: {missing_packages}", file=sys.stderr)
        for package in missing_packages:
            if not check_and_install_package(package):
                print(f"Failed to install required packages: {missing_packages}", file=sys.stderr)
                return False

    try:
        # Import core modules once
        import transformers as tf_module
        import torch as torch_module
        import logging
        
        # Set globals
        transformers = tf_module
        torch = torch_module
        
        # Configure logging to reduce noise (do this once)
        transformers.logging.set_verbosity_error()
        logging.getLogger().setLevel(logging.ERROR)
        
        # Optimize torch settings for inference speed (do this once)
        if torch.cuda.is_available():
            torch.backends.cudnn.benchmark = True
            torch.backends.cuda.matmul.allow_tf32 = True
            # Pre-warm CUDA context
            torch.cuda.empty_cache()
        
        # Mark setup as complete
        _environment_setup_done = True
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
    
    # Text-to-Speech models
    if any(name in model_id_lower for name in ["tts", "speecht5_tts", "mms-tts", "bark", "vibevoice"]):
        return "text-to-speech"
    
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
    """Run text generation with optimized performance and caching."""
    try:
        fast_mode = params.get("fast_mode", False)
        
        # Get cached or load model (optimized caching)
        model, tokenizer = get_or_load_model(model_id, params, local_model_path)
        
        # Minimal input validation for speed
        clean_input = input_text.strip()
        if not clean_input:
            return "ERROR: Empty input provided"
        
        # Optimize tokenization setup (do once)
        if tokenizer.pad_token is None:
            tokenizer.pad_token = tokenizer.eos_token
        
        # Highly optimized tokenization for maximum speed
        max_input_length = 128 if fast_mode else 256  # Even shorter for fast mode
        inputs = tokenizer(
            clean_input,
            return_tensors="pt",
            truncation=True,
            max_length=max_input_length,
            padding=False,
            add_special_tokens=True
        )
        
        # Direct device placement for speed
        device = next(model.parameters()).device
        inputs = {k: v.to(device, non_blocking=True) for k, v in inputs.items()}
        
        # Ultra-optimized generation parameters with 100 token hard limit
        max_new_tokens = 10 if fast_mode else min(params.get("max_length", 25), 100)  # Hard cap at 100 tokens
        
        # Fastest possible generation settings with randomness enabled
        generation_kwargs = {
            "max_new_tokens": max_new_tokens,
            "do_sample": True,  # Always enable sampling for randomness
            "num_return_sequences": 1,
            "pad_token_id": tokenizer.eos_token_id,
            "eos_token_id": tokenizer.eos_token_id,
            "early_stopping": True,
            "use_cache": True,
            # Use command line parameters for randomness
            "temperature": params.get("temperature", 0.8),
            "top_p": params.get("top_p", 0.9),
            "top_k": 50,  # Add top_k for more diversity
            "repetition_penalty": 1.1  # Reduce repetition
        }
        
        # Inference with minimal overhead
        with torch.no_grad():
            outputs = model.generate(**inputs, **generation_kwargs)
        
        # Fast decode with minimal processing
        generated_text = tokenizer.decode(outputs[0], skip_special_tokens=True, clean_up_tokenization_spaces=False)
        
        # Quick input removal
        if generated_text.startswith(clean_input):
            generated_text = generated_text[len(clean_input):].strip()
        
        # Quick validation
        if not generated_text:
            # Try alternative approach for empty results
            try:
                # Fallback: try with different parameters
                fallback_kwargs = generation_kwargs.copy()
                fallback_kwargs.update({
                    'max_new_tokens': 30,
                    'temperature': 0.9,
                    'do_sample': True
                })
                
                with torch.no_grad():
                    fallback_outputs = model.generate(**inputs, **fallback_kwargs)
                
                fallback_text = tokenizer.decode(fallback_outputs[0], skip_special_tokens=True, clean_up_tokenization_spaces=False)
                
                if fallback_text.startswith(clean_input):
                    fallback_text = fallback_text[len(clean_input):].strip()
                
                if fallback_text:
                    return fallback_text
                else:
                    return f"No text generated - model may need different parameters for this input."
            except:
                return f"No text generated - model processing completed but returned empty result."
        
        return generated_text
        
    except Exception as e:
        return f"ERROR: {str(e)}"


        

def run_text_to_speech(model_id: str, input_text: str, params: Dict[str, Any], local_model_path: Optional[str] = None) -> str:
    """Run text-to-speech synthesis on input text."""
    try:
        print(f"Processing text-to-speech with model: {model_id}", file=sys.stderr)
        print(f"Input text received: {input_text[:100]}{'...' if len(input_text) > 100 else ''}", file=sys.stderr)
        
        # Clean input text
        clean_input = input_text.strip()
        if not clean_input:
            return "ERROR: No text provided for speech synthesis"
        
        # Handle specific TTS models with different approaches
        if "vibevoice" in model_id.lower():
            # VibeVoice model - handle the unsupported architecture error
            return f"ERROR: The VibeVoice model architecture is not yet supported in this version of Transformers. Please try using an alternative TTS model like 'microsoft/speecht5_tts' or 'facebook/mms-tts-eng'."
        
        elif "speecht5" in model_id.lower():
            return run_speecht5_tts(model_id, clean_input, params, local_model_path)
        
        elif "mms-tts" in model_id.lower():
            return run_mms_tts(model_id, clean_input, params, local_model_path)
        
        elif "bark" in model_id.lower():
            return run_bark_tts(model_id, clean_input, params, local_model_path)
        
        else:
            # Generic TTS handling
            return run_generic_tts(model_id, clean_input, params, local_model_path)
            
    except Exception as e:
        error_msg = str(e)
        print(f"Error in text-to-speech synthesis: {error_msg}", file=sys.stderr)
        
        if "vibevoice" in error_msg.lower():
            return "ERROR: The VibeVoice model architecture is not yet supported. Try using 'microsoft/speecht5_tts' instead."
        elif "trust_remote_code" in error_msg.lower():
            return "ERROR: Model requires trust_remote_code=True but was blocked for security."
        else:
            return f"ERROR: {error_msg}"


def run_speecht5_tts(model_id: str, input_text: str, params: Dict[str, Any], local_model_path: Optional[str] = None) -> str:
    """Run SpeechT5 TTS model."""
    try:
        from transformers import SpeechT5Processor, SpeechT5ForTextToSpeech, SpeechT5HifiGan
        import soundfile as sf
        import numpy as np
        import os
        from datetime import datetime
        
        # Determine model path
        model_path_to_use = local_model_path if local_model_path and os.path.exists(local_model_path) else model_id
        
        print("Loading SpeechT5 TTS model and processor...", file=sys.stderr)
        processor = SpeechT5Processor.from_pretrained(model_path_to_use)
        model = SpeechT5ForTextToSpeech.from_pretrained(model_path_to_use)
        vocoder = SpeechT5HifiGan.from_pretrained("microsoft/speecht5_hifigan")
        
        # Prepare inputs
        inputs = processor(text=input_text, return_tensors="pt")
        
        # Load speaker embeddings (using default speaker)
        speaker_embeddings = torch.tensor([[ 0.0000,  0.0000,  0.0000, ...]]).unsqueeze(0)  # Default speaker embedding
        
        # Generate speech
        speech = model.generate_speech(inputs["input_ids"], speaker_embeddings, vocoder=vocoder)
        
        # Save audio file
        output_dir = "C:\\Users\\tanne\\Documents\\CSimple\\Resources\\Audio"
        os.makedirs(output_dir, exist_ok=True)
        
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        output_file = os.path.join(output_dir, f"tts_output_{timestamp}.wav")
        
        sf.write(output_file, speech.numpy(), samplerate=16000)
        
        return f"Speech synthesis completed. Audio saved to: {output_file}"
        
    except ImportError as e:
        return f"ERROR: Required library not installed: {e}. Try: pip install soundfile"
    except Exception as e:
        return f"ERROR: SpeechT5 TTS failed: {e}"


def run_mms_tts(model_id: str, input_text: str, params: Dict[str, Any], local_model_path: Optional[str] = None) -> str:
    """Run MMS TTS model."""
    try:
        from transformers import VitsModel, AutoTokenizer
        import soundfile as sf
        import os
        from datetime import datetime
        
        # Determine model path
        model_path_to_use = local_model_path if local_model_path and os.path.exists(local_model_path) else model_id
        
        print("Loading MMS TTS model...", file=sys.stderr)
        model = VitsModel.from_pretrained(model_path_to_use)
        tokenizer = AutoTokenizer.from_pretrained(model_path_to_use)
        
        # Prepare inputs
        inputs = tokenizer(input_text, return_tensors="pt")
        
        # Generate speech
        with torch.no_grad():
            outputs = model(**inputs)
        
        # Extract waveform
        waveform = outputs.waveform[0].cpu().numpy()
        
        # Save audio file
        output_dir = "C:\\Users\\tanne\\Documents\\CSimple\\Resources\\Audio"
        os.makedirs(output_dir, exist_ok=True)
        
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        output_file = os.path.join(output_dir, f"mms_tts_output_{timestamp}.wav")
        
        sf.write(output_file, waveform, samplerate=22050)
        
        return f"MMS TTS synthesis completed. Audio saved to: {output_file}"
        
    except ImportError as e:
        return f"ERROR: Required library not installed: {e}. Try: pip install soundfile"
    except Exception as e:
        return f"ERROR: MMS TTS failed: {e}"


def run_bark_tts(model_id: str, input_text: str, params: Dict[str, Any], local_model_path: Optional[str] = None) -> str:
    """Run Bark TTS model."""
    try:
        from transformers import AutoProcessor, BarkModel
        import soundfile as sf
        import os
        from datetime import datetime
        
        # Determine model path
        model_path_to_use = local_model_path if local_model_path and os.path.exists(local_model_path) else model_id
        
        print("Loading Bark TTS model...", file=sys.stderr)
        processor = AutoProcessor.from_pretrained(model_path_to_use)
        model = BarkModel.from_pretrained(model_path_to_use)
        
        # Prepare inputs with speaker preset
        inputs = processor(input_text, voice_preset="v2/en_speaker_6")
        
        # Generate speech
        with torch.no_grad():
            audio_array = model.generate(**inputs)
        
        # Convert to numpy
        audio_array = audio_array.cpu().numpy().squeeze()
        
        # Save audio file
        output_dir = "C:\\Users\\tanne\\Documents\\CSimple\\Resources\\Audio"
        os.makedirs(output_dir, exist_ok=True)
        
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        output_file = os.path.join(output_dir, f"bark_tts_output_{timestamp}.wav")
        
        sf.write(output_file, audio_array, samplerate=model.generation_config.sample_rate)
        
        return f"Bark TTS synthesis completed. Audio saved to: {output_file}"
        
    except ImportError as e:
        return f"ERROR: Required library not installed: {e}. Try: pip install soundfile"
    except Exception as e:
        return f"ERROR: Bark TTS failed: {e}"


def run_generic_tts(model_id: str, input_text: str, params: Dict[str, Any], local_model_path: Optional[str] = None) -> str:
    """Run generic TTS model using transformers pipeline."""
    try:
        from transformers import pipeline
        import soundfile as sf
        import os
        from datetime import datetime
        
        # Determine model path
        model_path_to_use = local_model_path if local_model_path and os.path.exists(local_model_path) else model_id
        
        print("Loading generic TTS model...", file=sys.stderr)
        
        # Create TTS pipeline
        tts_pipeline = pipeline(
            "text-to-speech",
            model=model_path_to_use,
            trust_remote_code=params.get("trust_remote_code", True)
        )
        
        # Generate speech
        result = tts_pipeline(input_text)
        
        # Extract audio data
        if isinstance(result, dict) and "audio" in result:
            audio_data = result["audio"]
            sample_rate = result.get("sampling_rate", 22050)
        else:
            audio_data = result
            sample_rate = 22050
        
        # Save audio file
        output_dir = "C:\\Users\\tanne\\Documents\\CSimple\\Resources\\Audio"
        os.makedirs(output_dir, exist_ok=True)
        
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        output_file = os.path.join(output_dir, f"generic_tts_output_{timestamp}.wav")
        
        sf.write(output_file, audio_data, samplerate=sample_rate)
        
        return f"TTS synthesis completed. Audio saved to: {output_file}"
        
    except ImportError as e:
        return f"ERROR: Required library not installed: {e}. Try: pip install soundfile"
    except Exception as e:
        return f"ERROR: Generic TTS failed: {e}"


def run_speech_recognition(model_id: str, input_text: str, params: Dict[str, Any], local_model_path: Optional[str] = None) -> str:
    """Run automatic speech recognition on audio files (supports multiple files)."""
    try:
        import re  # For regex pattern matching
        print(f"Processing speech recognition with model: {model_id}", file=sys.stderr)
        print(f"Raw input text received: {input_text}", file=sys.stderr)
        
        # Extract audio file paths from input text (support multiple audio files)
        audio_file_paths = []
        
        # Handle multiple formats:
        # 1. Direct file path
        # 2. "audio file: [path]" format
        # 3. Combined ensemble format like "[Node Name]: C:\path\to\file.wav"
        # 4. Multiple audio files separated by delimiters (|, ;, &, ,)
        
        if "audio file:" in input_text:
            # Extract the file path after "audio file:"
            parts = input_text.split("audio file:")
            if len(parts) > 1:
                audio_file_paths.append(parts[1].strip())
        elif any(ext in input_text.lower() for ext in ['.wav', '.mp3', '.m4a', '.flac', '.ogg', '.aac']):
            # Look for file paths in ensemble format
            import re
            
            # Check for ensemble delimiters first
            if any(delimiter in input_text for delimiter in ['|', ';', '&', ',']):
                # Parse ensemble format
                ensemble_patterns = [
                    r'audio\d+:([^|;,&]+)',  # Sequential format: audio1:path|audio2:path
                    r'([^|;,&]+\.(wav|mp3|m4a|flac|ogg|aac))',  # Direct paths with delimiters
                ]
                
                for pattern in ensemble_patterns:
                    matches = re.findall(pattern, input_text, re.IGNORECASE)
                    if matches:
                        for match in matches:
                            if isinstance(match, tuple):
                                path = match[0].strip()
                            else:
                                path = match.strip()
                            if path and os.path.exists(path):
                                audio_file_paths.append(path)
                        break
            
            # If no ensemble matches, look for individual file paths using pre-compiled patterns
            if not audio_file_paths:
                for pattern in _audio_patterns:
                    matches = pattern.findall(input_text)
                    if matches:
                        for match in matches:
                            if isinstance(match, tuple):
                                path = match[0].strip()
                            else:
                                path = match.strip()
                            if path and os.path.exists(path):
                                audio_file_paths.append(path)
        else:
            # Try treating the entire input as a file path
            potential_path = input_text.strip()
            if os.path.exists(potential_path) and any(potential_path.lower().endswith(ext) for ext in ['.wav', '.mp3', '.m4a', '.flac', '.ogg', '.aac']):
                audio_file_paths.append(potential_path)
        
        # Remove duplicates while preserving order
        audio_file_paths = list(dict.fromkeys(audio_file_paths))
        
        # Fallback logic for each file path
        processed_audio_paths = []
        for audio_file_path in audio_file_paths:
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
                
                if audio_file_path and os.path.exists(audio_file_path):
                    processed_audio_paths.append(audio_file_path)
            else:
                processed_audio_paths.append(audio_file_path)
        
        print(f"Extracted {len(processed_audio_paths)} audio file path(s): {processed_audio_paths}", file=sys.stderr)
        
        if not processed_audio_paths:
            return f"ERROR: No valid audio file paths found in input. Input received: {input_text}"
        
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
        
        # Process all audio files
        print(f"Loading and processing {len(processed_audio_paths)} audio file(s)...", file=sys.stderr)
        
        transcriptions = []
        for i, audio_file_path in enumerate(processed_audio_paths):
            try:
                print(f"Processing audio {i+1}/{len(processed_audio_paths)}: {os.path.basename(audio_file_path)}", file=sys.stderr)
                
                # Load audio file
                try:
                    audio_array, sampling_rate = librosa.load(audio_file_path, sr=16000)  # Whisper expects 16kHz
                    print(f"Audio {i+1} loaded: {len(audio_array)} samples at {sampling_rate}Hz", file=sys.stderr)
                except Exception as e:
                    transcriptions.append(f"Audio {i+1} ({os.path.basename(audio_file_path)}): ERROR - Failed to load audio: {str(e)}")
                    continue
                
                # Process audio with the model
                print(f"Running speech recognition for audio {i+1}...", file=sys.stderr)
                
                result = pipe(audio_array)
                
                # Extract transcription text
                if isinstance(result, dict) and "text" in result:
                    transcription = result["text"].strip()
                elif isinstance(result, list) and len(result) > 0 and "text" in result[0]:
                    transcription = result[0]["text"].strip()
                else:
                    transcription = str(result).strip()
                
                print(f"Transcription {i+1} complete: {len(transcription)} characters", file=sys.stderr)
                
                # Clean up transcription - remove duplicate filename if present
                filename_without_ext = os.path.splitext(os.path.basename(audio_file_path))[0]
                if transcription.startswith(f"{filename_without_ext}: "):
                    transcription = transcription[len(f"{filename_without_ext}: "):]
                elif transcription.startswith(f"{os.path.basename(audio_file_path)}: "):
                    transcription = transcription[len(f"{os.path.basename(audio_file_path)}: "):]
                
                if transcription:
                    transcriptions.append(f"Audio {i+1} ({os.path.basename(audio_file_path)}): {transcription}")
                else:
                    transcriptions.append(f"Audio {i+1} ({os.path.basename(audio_file_path)}): No speech detected in the audio file")
                    
            except Exception as e:
                error_msg = f"Audio {i+1} ({os.path.basename(audio_file_path)}): ERROR - {str(e)}"
                transcriptions.append(error_msg)
                print(f"Error processing audio {i+1}: {e}", file=sys.stderr)
        
        # Combine results
        if len(transcriptions) == 1:
            # Single audio result - clean format: just return the transcription without numbering
            result = transcriptions[0]
            # Remove "Audio 1 (" prefix and clean up
            if result.startswith("Audio 1 ("):
                # Extract just the transcription part after the filename
                match = re.search(r'Audio 1 \([^)]+\): (.+)', result)
                if match:
                    return match.group(1)
            return result
        else:
            # Multiple audio files result - return clean format for C# processing
            clean_results = []
            for result in transcriptions:
                # Extract just the transcription part for each audio
                match = re.search(r'Audio \d+ \([^)]+\): (.+)', result)
                if match:
                    clean_results.append(match.group(1))
                else:
                    clean_results.append(result)
            return "\n\n".join(clean_results)
        
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
        import re  # For regex pattern matching
        print(f"Processing image-to-text with model: {model_id}", file=sys.stderr)
        print(f"Raw input text received: {input_text}", file=sys.stderr)
        
        # Extract image file paths from input text (support multiple images)
        image_file_paths = []
        
        # Handle multiple formats:
        # 1. Direct file path
        # 2. "image file: [path]" format
        # 3. Combined ensemble format like "[Node Name]: C:\path\to\file.jpg"
        # 4. Multiple images separated by delimiters (|, ;, &, ,)
        
        if "image file:" in input_text:
            # Extract the file path after "image file:"
            parts = input_text.split("image file:")
            if len(parts) > 1:
                image_file_paths.append(parts[1].strip())
        elif any(ext in input_text.lower() for ext in ['.jpg', '.jpeg', '.png', '.bmp', '.gif', '.tiff', '.webp']):
            # Look for file paths in ensemble format
            import re
            
            # Check for ensemble delimiters first
            if any(delimiter in input_text for delimiter in ['|', ';', '&', ',']):
                # Parse ensemble format
                ensemble_patterns = [
                    r'img\d+:([^|;,&]+)',  # Sequential format: img1:path|img2:path
                    r'([^|;,&]+\.(jpg|jpeg|png|bmp|gif|tiff|webp))',  # Direct paths with delimiters
                ]
                
                for pattern in ensemble_patterns:
                    matches = re.findall(pattern, input_text, re.IGNORECASE)
                    if matches:
                        for match in matches:
                            if isinstance(match, tuple):
                                path = match[0].strip()
                            else:
                                path = match.strip()
                            if path and os.path.exists(path):
                                image_file_paths.append(path)
                        break
            
            # If no ensemble matches, look for individual file paths
            if not image_file_paths:
                image_patterns = [
                    r'\]:\s*([A-Z]:[^:]+\.(jpg|jpeg|png|bmp|gif|tiff|webp))',  # Match after ]: C:\path
                    r':\s*([A-Z]:[^:\[\]]+\.(jpg|jpeg|png|bmp|gif|tiff|webp))',  # Match after : C:\path (but not inside brackets)
                    r'([A-Z]:[^:\[\]]+\.(jpg|jpeg|png|bmp|gif|tiff|webp))'      # Direct match C:\path
                ]
                
                for pattern in image_patterns:
                    matches = re.findall(pattern, input_text, re.IGNORECASE)
                    if matches:
                        for match in matches:
                            if isinstance(match, tuple):
                                path = match[0].strip()
                            else:
                                path = match.strip()
                            if path and os.path.exists(path):
                                image_file_paths.append(path)
        else:
            # Try treating the entire input as a file path
            potential_path = input_text.strip()
            if os.path.exists(potential_path) and any(potential_path.lower().endswith(ext) for ext in ['.jpg', '.jpeg', '.png', '.bmp', '.gif', '.tiff', '.webp']):
                image_file_paths.append(potential_path)
        
        # Remove duplicates while preserving order
        image_file_paths = list(dict.fromkeys(image_file_paths))
        
        print(f"Extracted {len(image_file_paths)} image file path(s): {image_file_paths}", file=sys.stderr)
        
        if not image_file_paths:
            return f"ERROR: No valid image file paths found in input. Input received: {input_text}"
        
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
        
        # Process all images
        print(f"Loading and processing {len(image_file_paths)} image file(s)...", file=sys.stderr)
        
        captions = []
        for i, image_file_path in enumerate(image_file_paths):
            try:
                print(f"Processing image {i+1}/{len(image_file_paths)}: {os.path.basename(image_file_path)}", file=sys.stderr)
                
                # Load image file
                image = Image.open(image_file_path).convert("RGB")
                print(f"Image {i+1} loaded: {image.size} pixels", file=sys.stderr)
                
                # Process image with the model
                print(f"Running image captioning for image {i+1}...", file=sys.stderr)
                
                # Prepare inputs
                inputs = processor(image, return_tensors="pt")
                
                # Generate caption
                with torch.no_grad():
                    out = model.generate(**inputs, max_length=params.get("max_length", 50), num_beams=5)
                
                # Decode caption
                caption = processor.decode(out[0], skip_special_tokens=True)
                
                print(f"Caption {i+1} generated: {len(caption)} characters", file=sys.stderr)
                
                # Clean up caption - remove duplicate filename if present
                filename_without_ext = os.path.splitext(os.path.basename(image_file_path))[0]
                if caption.startswith(f"{filename_without_ext}: "):
                    caption = caption[len(f"{filename_without_ext}: "):]
                elif caption.startswith(f"{os.path.basename(image_file_path)}: "):
                    caption = caption[len(f"{os.path.basename(image_file_path)}: "):]
                
                if caption:
                    captions.append(f"Image {i+1} ({os.path.basename(image_file_path)}): {caption}")
                else:
                    captions.append(f"Image {i+1} ({os.path.basename(image_file_path)}): No caption could be generated")
                    
            except Exception as e:
                error_msg = f"Image {i+1} ({os.path.basename(image_file_path)}): ERROR - {str(e)}"
                captions.append(error_msg)
                print(f"Error processing image {i+1}: {e}", file=sys.stderr)
        
        # Combine results
        if len(captions) == 1:
            # Single image result - clean format: just return the caption without numbering
            result = captions[0]
            # Remove "Image 1 (" prefix and clean up
            if result.startswith("Image 1 ("):
                # Extract just the caption part after the filename
                match = re.search(r'Image 1 \([^)]+\): (.+)', result)
                if match:
                    return match.group(1)
            return result
        else:
            # Multiple images result - return clean format for C# processing
            clean_results = []
            for result in captions:
                # Extract just the caption part for each image
                match = re.search(r'Image \d+ \([^)]+\): (.+)', result)
                if match:
                    clean_results.append(match.group(1))
                else:
                    clean_results.append(result)
            return "\n\n".join(clean_results)
        
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


def get_or_load_model(model_id: str, params: Dict[str, Any], local_model_path: Optional[str] = None):
    """Get model and tokenizer from cache or load them with optimized performance."""
    cache_key = local_model_path if local_model_path else model_id
    
    # Check if already cached - fast path
    if cache_key in _model_cache and cache_key in _tokenizer_cache:
        return _model_cache[cache_key], _tokenizer_cache[cache_key]
    
    # Special handling for problematic models
    if "vibevoice" in model_id.lower():
        raise Exception("The VibeVoice model architecture is not yet supported. Please try using an alternative TTS model like 'microsoft/speecht5_tts' or 'facebook/mms-tts-eng'.")
    
    # Use global imports for better performance
    from transformers import AutoTokenizer, AutoModelForCausalLM
    
    model_path_to_use = local_model_path if local_model_path and os.path.exists(local_model_path) else model_id
    
    # Optimized tokenizer loading
    try:
        # Try fast tokenizer first with optimized settings
        tokenizer = AutoTokenizer.from_pretrained(
            model_path_to_use,
            trust_remote_code=params.get("trust_remote_code", True),
            cache_dir="C:\\Users\\tanne\\Documents\\CSimple\\Resources\\HFModels" if not local_model_path else None,
            local_files_only=bool(local_model_path),
            use_fast=True,  # Prefer fast tokenizer for speed
            padding_side="left"  # Optimize for generation
        )
    except Exception:
        # Fallback to slow tokenizer
        tokenizer = AutoTokenizer.from_pretrained(
            model_path_to_use,
            trust_remote_code=params.get("trust_remote_code", True),
            cache_dir="C:\\Users\\tanne\\Documents\\CSimple\\Resources\\HFModels" if not local_model_path else None,
            local_files_only=bool(local_model_path),
            use_fast=False
        )
    
    # Configure model loading for maximum speed
    force_cpu = params.get("cpu_optimize", False) or not torch.cuda.is_available()
    fast_mode = params.get("fast_mode", False)
    
    model_kwargs = {
        "trust_remote_code": params.get("trust_remote_code", True),
        "torch_dtype": torch.float32 if force_cpu else torch.float16,
        "low_cpu_mem_usage": True,
        "cache_dir": "C:\\Users\\tanne\\Documents\\CSimple\\Resources\\HFModels" if not local_model_path else None,
        "local_files_only": bool(local_model_path)
    }
    
    # Optimize model loading strategy
    if not force_cpu:
        if fast_mode:
            # For fast mode, use simple GPU placement
            model_kwargs["device_map"] = "cuda:0" if torch.cuda.is_available() else "cpu"
        else:
            model_kwargs["device_map"] = "auto"
    
    # Load model with error handling
    try:
        model = AutoModelForCausalLM.from_pretrained(model_path_to_use, **model_kwargs)
    except Exception as e:
        # Check for specific unsupported architecture errors
        if "vibevoice" in str(e).lower() or "does not recognize this architecture" in str(e).lower():
            raise Exception("The checkpoint you are trying to load has an unsupported model architecture. Consider using an alternative model like 'microsoft/speecht5_tts' for text-to-speech or updating your transformers library.")
        
        # Fallback to CPU if GPU loading fails
        if not force_cpu:
            print(f"GPU loading failed, falling back to CPU: {e}", file=sys.stderr)
            model_kwargs["device_map"] = "cpu"
            model_kwargs["torch_dtype"] = torch.float32
            model = AutoModelForCausalLM.from_pretrained(model_path_to_use, **model_kwargs)
        else:
            raise
    
    # Set model to eval mode for inference optimization
    model.eval()
    
    # Optimize for inference
    if hasattr(model, 'generation_config'):
        model.generation_config.use_cache = True
        if fast_mode:
            model.generation_config.max_new_tokens = 15  # Limit tokens in fast mode
    
    # Apply CPU optimization if needed
    if force_cpu and "device_map" not in model_kwargs:
        model = model.to("cpu")
    
    # Cache for future use
    _model_cache[cache_key] = model
    _tokenizer_cache[cache_key] = tokenizer
    
    return model, tokenizer


def preload_models(model_ids: list, params: Dict[str, Any]) -> bool:
    """Pre-load models into cache for faster subsequent execution."""
    if not model_ids:
        return True
    
    print(f"Pre-loading {len(model_ids)} models into cache...", file=sys.stderr)
    
    for model_id in model_ids:
        try:
            print(f"Loading {model_id}...", file=sys.stderr)
            model, tokenizer = get_or_load_model(model_id, params)
            print(f"✓ {model_id} loaded and cached", file=sys.stderr)
        except Exception as e:
            print(f"✗ Failed to preload {model_id}: {e}", file=sys.stderr)
            return False
    
    print(f"✓ All {len(model_ids)} models pre-loaded successfully", file=sys.stderr)
    return True


def main() -> int:
    """Main entry point - optimized for speed."""
    try:
        args = parse_arguments()
        
        # Skip verbose logging in fast mode for speed
        if not args.fast_mode:
            print(f"Setting up environment for model: {args.model_id}", file=sys.stderr)
        
        # Environment setup with caching
        if not setup_environment():
            print("ERROR: Failed to set up Python environment", file=sys.stderr)
            return 1
        
        # Pre-build params dict to avoid repeated dict creation
        params = {
            "max_length": args.max_length,
            "temperature": args.temperature,
            "top_p": args.top_p,
            "trust_remote_code": args.trust_remote_code,
            "cpu_optimize": args.cpu_optimize,
            "offline_mode": args.offline_mode,
            "fast_mode": args.fast_mode
        }
        
        # Pre-load models if specified (for batch processing optimization)
        if hasattr(args, 'preload_models') and args.preload_models:
            if not preload_models(args.preload_models, params):
                print("WARNING: Some models failed to preload", file=sys.stderr)
        
        # Skip expensive cache validation in fast mode
        if not args.local_model_path or not os.path.exists(args.local_model_path):
            if not args.fast_mode:
                # Only do cache validation in non-fast mode
                cache_valid = check_model_cache_status(args.model_id)
                if not cache_valid:
                    if not force_download_model(args.model_id):
                        print(f"ERROR: Failed to download model {args.model_id}", file=sys.stderr)
                        return 1
        
        # Detect model type once
        model_type = detect_model_type(args.model_id)
        
        # Pre-build params dict to avoid repeated dict creation
        params = {
            "max_length": args.max_length,
            "temperature": args.temperature,
            "top_p": args.top_p,
            "trust_remote_code": args.trust_remote_code,
            "cpu_optimize": args.cpu_optimize,
            "offline_mode": args.offline_mode,
            "fast_mode": args.fast_mode
        }
        
        # Preload models if specified
        if args.preload_models:
            preload_success = preload_models(args.preload_models, params)
            if not preload_success:
                print("ERROR: Failed to preload specified models", file=sys.stderr)
                return 1
        
        # Direct dispatch for performance
        if model_type == "text-generation":
            result = run_text_generation(args.model_id, args.input, params, args.local_model_path)
        elif model_type == "automatic-speech-recognition":
            result = run_speech_recognition(args.model_id, args.input, params, args.local_model_path)
        elif model_type == "image-to-text":
            result = run_image_to_text(args.model_id, args.input, params, args.local_model_path)
        elif model_type == "text-to-speech":
            result = run_text_to_speech(args.model_id, args.input, params, args.local_model_path)
        else:
            # Fast fallback for unknown types
            result = f"Model type '{model_type}' not fully implemented yet. Basic response: Processed '{args.input}' with {args.model_id}"
        
        # Minimal output processing for speed
        clean_result = result.strip() if result else "No output generated"
        
        # Direct output with flush for immediate response
        print(clean_result, flush=True)
        return 0
        
    except KeyboardInterrupt:
        print("ERROR: Operation cancelled by user", file=sys.stderr)
        return 1
    except Exception as e:
        error_msg = f"ERROR: {str(e)}"
        print(error_msg, file=sys.stderr)
        # Skip traceback in fast mode to avoid overhead
        if not getattr(args, 'fast_mode', False):
            traceback.print_exc(file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
