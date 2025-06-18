# RTX 3090 Compatible Models for CSimple

## Issue: DeepSeek R1 FP8 Quantization
- DeepSeek-ai/DeepSeek-R1 requires FP8 quantization (compute capability 8.9+)
- RTX 3090 has compute capability 8.6
- FP8 quantization is NOT supported on RTX 3090

## Alternative DeepSeek Models (RTX 3090 Compatible):
1. **deepseek-ai/deepseek-coder-7b-instruct-v1.5** - Code generation, no FP8 requirement
2. **deepseek-ai/deepseek-llm-7b-chat** - General chat model, compatible with RTX 3090
3. **deepseek-ai/deepseek-coder-1.3b-instruct** - Smaller, faster, guaranteed compatibility

## Recommended GPU-Friendly Models for RTX 3090:
1. **microsoft/DialoGPT-medium** - Excellent for conversations
2. **gpt2-large** - Larger version of GPT-2
3. **microsoft/DialoGPT-large** - Advanced conversation model
4. **huggingface/CodeBERTa-small-v1** - Good for code tasks

## Working Solution:
Replace "deepseek-ai/DeepSeek-R1" with "deepseek-ai/deepseek-coder-7b-instruct-v1.5" in your model configuration.

## PyTorch Settings for RTX 3090:
- CUDA: ✅ Enabled
- FP16: ✅ Supported
- FP8: ❌ Not supported (requires compute capability 8.9+)
- Device Map: ✅ Auto mapping works
- Flash Attention: ✅ Supported

Your RTX 3090 is powerful and will work great with most models - just not the specific FP8-quantized DeepSeek R1.
