# Model Training and Alignment Scripts

This directory contains Python scripts for training and aligning machine learning models, supporting both pretrained model alignment and training from scratch.

## Files

- `model_training_alignment.py` - Main training script
- `example_config.json` - Example configuration for alignment training
- `example_architecture_spec.json` - Example architecture specification for training from scratch
- `training_requirements.txt` - Python dependencies required for training

## Usage

### 1. Install Dependencies

```bash
pip install -r training_requirements.txt
```

### 2. Prepare Configuration

For **model alignment** (pretrained models):
- Copy and modify `example_config.json`
- Set `TrainingMode` to "Align Pretrained Model"
- Choose alignment technique (Fine-tuning, RLHF, DPO, etc.)

For **training from scratch**:
- Copy and modify `example_config.json` and set `TrainingMode` to "Train From Scratch"
- Copy and modify `example_architecture_spec.json` to define your model architecture

### 3. Run Training

**Align Pretrained Model:**
```bash
python model_training_alignment.py \
    --config example_config.json \
    --model_path /path/to/pretrained/model \
    --dataset_path /path/to/dataset \
    --output_path /path/to/output
```

**Train From Scratch:**
```bash
python model_training_alignment.py \
    --config example_config.json \
    --architecture_spec example_architecture_spec.json \
    --dataset_path /path/to/dataset \
    --output_path /path/to/output
```

## Supported Features

### Model Types
- Text models (Transformers, RNN/LSTM)
- Vision models (Vision Transformer, CNN)
- Audio models
- Multimodal models

### Alignment Techniques
- Fine-tuning
- Instruction Tuning
- RLHF (Reinforcement Learning from Human Feedback)
- DPO (Direct Preference Optimization)
- Constitutional AI
- Parameter-Efficient Fine-tuning (PEFT/LoRA)

### Dataset Formats
- JSONL (text data)
- CSV (structured data)
- Image directories (classification)
- Audio directories (classification)
- HuggingFace datasets
- Custom formats with auto-detection

### Model Architectures (Training from Scratch)
- Transformer (GPT-style)
- Vision Transformer (ViT)
- Convolutional Neural Network (CNN)
- Recurrent Neural Network (RNN/LSTM)
- Custom architectures

## Integration with C# Application

This Python script is designed to work with the NetPage training system in the C# MAUI application. The C# application can:

1. Generate configuration files based on UI selections
2. Call this Python script with appropriate parameters
3. Monitor training progress through logs
4. Load trained models for inference

## Configuration Parameters

### TrainingConfig Properties
- `TrainingMode`: "Align Pretrained Model" or "Train From Scratch"
- `AlignmentTechnique`: Alignment method to use
- `ModelArchitecture`: Architecture type (for training from scratch)
- `DatasetFormat`: Format of training data
- `FineTuningMethod`: Method for parameter-efficient fine-tuning
- `LearningRate`: Training learning rate
- `Epochs`: Number of training epochs
- `BatchSize`: Training batch size
- `UseAdvancedConfig`: Enable advanced hyperparameter configuration
- `CustomHyperparameters`: Additional hyperparameters as JSON string

### Architecture Specification (Training from Scratch)
- `Type`: Model architecture type
- `HiddenSize`: Hidden layer size
- `Layers`: Number of layers
- `AttentionHeads`: Number of attention heads (for Transformers)
- `VocabSize`: Vocabulary size (for text models)
- `MaxSequenceLength`: Maximum sequence length
- `CustomParameters`: Architecture-specific parameters

## Logging

The script creates detailed logs in `training.log` and outputs progress to the console. Logs include:
- Configuration validation
- Dataset loading information
- Training progress (loss, epochs, steps)
- Model saving confirmation
- Error details for troubleshooting

## Hardware Requirements

- **GPU**: CUDA-compatible GPU recommended for training
- **RAM**: Minimum 8GB, 16GB+ recommended for larger models
- **Storage**: Sufficient space for datasets and model checkpoints

## Troubleshooting

1. **Missing Dependencies**: Install all requirements from `training_requirements.txt`
2. **CUDA Issues**: Ensure CUDA toolkit is installed and compatible with PyTorch version
3. **Memory Issues**: Reduce batch size or use gradient accumulation
4. **Dataset Format**: Check dataset format matches the specified format in configuration

## Examples

See the example configuration files for common training scenarios:
- Text model fine-tuning with LoRA
- Vision model training from scratch
- Multimodal alignment with custom datasets
