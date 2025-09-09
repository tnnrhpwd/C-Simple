#!/usr/bin/env python3
"""
Model Training and Alignment Script for C-Simple
Supports both pretrained model alignment and training from scratch
for text, image, audio, and multimodal models.
"""

import os
import sys
import json
import argparse
import logging
import traceback
from pathlib import Path
from typing import Dict, List, Optional, Any, Union
from datetime import datetime
import torch
import torch.nn as nn
import torch.optim as optim
from torch.utils.data import DataLoader, Dataset
import transformers
from transformers import (
    AutoModel, AutoTokenizer, AutoConfig,
    TrainingArguments, Trainer,
    DataCollatorWithPadding,
    get_linear_schedule_with_warmup
)

try:
    from peft import LoraConfig, get_peft_model, TaskType
    PEFT_AVAILABLE = True
except ImportError:
    PEFT_AVAILABLE = False
    print("Warning: PEFT not available. Some fine-tuning methods will not work.")

try:
    import datasets
    from datasets import Dataset as HFDataset
    DATASETS_AVAILABLE = True
except ImportError:
    DATASETS_AVAILABLE = False
    print("Warning: datasets library not available. Some dataset features will not work.")

try:
    from PIL import Image
    import torchvision.transforms as transforms
    import torchaudio
    MULTIMODAL_AVAILABLE = True
except ImportError:
    MULTIMODAL_AVAILABLE = False
    print("Warning: PIL/torchvision/torchaudio not available. Multimodal training will not work.")

# Setup logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(levelname)s - %(message)s',
    handlers=[
        logging.FileHandler('training.log'),
        logging.StreamHandler(sys.stdout)
    ]
)
logger = logging.getLogger(__name__)

class TrainingConfig:
    """Configuration class for training parameters"""
    def __init__(self, config_path: str = None, **kwargs):
        # Default configuration
        self.training_mode = "Align Pretrained Model"
        self.alignment_technique = "Fine-tuning"
        self.model_architecture = "Transformer"
        self.dataset_format = "Auto-detect"
        self.fine_tuning_method = "LoRA"
        self.learning_rate = 0.0001
        self.epochs = 3
        self.batch_size = 4
        self.use_advanced_config = False
        self.custom_hyperparameters = ""
        
        # Load from file if provided
        if config_path and os.path.exists(config_path):
            self.load_from_file(config_path)
        
        # Override with any provided kwargs
        for key, value in kwargs.items():
            if hasattr(self, key):
                setattr(self, key, value)
    
    def load_from_file(self, config_path: str):
        """Load configuration from JSON file"""
        try:
            with open(config_path, 'r') as f:
                config_data = json.load(f)
            
            for key, value in config_data.items():
                snake_case_key = self._camel_to_snake(key)
                if hasattr(self, snake_case_key):
                    setattr(self, snake_case_key, value)
        except Exception as e:
            logger.error(f"Error loading config from {config_path}: {e}")
    
    def _camel_to_snake(self, name):
        """Convert camelCase to snake_case"""
        import re
        s1 = re.sub('(.)([A-Z][a-z]+)', r'\1_\2', name)
        return re.sub('([a-z0-9])([A-Z])', r'\1_\2', s1).lower()

class TextDataset(Dataset):
    """Dataset class for text data"""
    def __init__(self, data_path: str, tokenizer, max_length: int = 512):
        self.tokenizer = tokenizer
        self.max_length = max_length
        self.data = self._load_data(data_path)
    
    def _load_data(self, data_path: str):
        """Load data from various formats"""
        data = []
        
        if data_path.endswith('.jsonl'):
            with open(data_path, 'r', encoding='utf-8') as f:
                for line in f:
                    try:
                        item = json.loads(line.strip())
                        data.append(item)
                    except json.JSONDecodeError:
                        continue
        elif data_path.endswith('.csv'):
            import pandas as pd
            df = pd.read_csv(data_path)
            data = df.to_dict('records')
        else:
            raise ValueError(f"Unsupported file format: {data_path}")
        
        return data
    
    def __len__(self):
        return len(self.data)
    
    def __getitem__(self, idx):
        item = self.data[idx]
        
        # Handle different field names
        input_text = item.get('input', item.get('text', item.get('prompt', '')))
        output_text = item.get('output', item.get('completion', item.get('response', '')))
        
        # Tokenize
        encoding = self.tokenizer(
            input_text,
            output_text,
            truncation=True,
            padding='max_length',
            max_length=self.max_length,
            return_tensors='pt'
        )
        
        return {
            'input_ids': encoding['input_ids'].flatten(),
            'attention_mask': encoding['attention_mask'].flatten(),
            'labels': encoding['input_ids'].flatten()
        }

class ImageDataset(Dataset):
    """Dataset class for image data"""
    def __init__(self, data_path: str, transform=None):
        self.data_path = Path(data_path)
        self.transform = transform or transforms.Compose([
            transforms.Resize((224, 224)),
            transforms.ToTensor(),
            transforms.Normalize(mean=[0.485, 0.456, 0.406], std=[0.229, 0.224, 0.225])
        ])
        
        # Load image paths and labels
        self.samples = self._load_image_data()
    
    def _load_image_data(self):
        """Load image data from directory structure or metadata file"""
        samples = []
        
        # Check for metadata file
        metadata_file = self.data_path / "metadata.json"
        if metadata_file.exists():
            with open(metadata_file, 'r') as f:
                metadata = json.load(f)
            for item in metadata:
                samples.append((item['image_path'], item.get('label', 0)))
        else:
            # Use directory structure (each subdirectory is a class)
            for class_dir in self.data_path.iterdir():
                if class_dir.is_dir():
                    for img_file in class_dir.glob('*'):
                        if img_file.suffix.lower() in ['.jpg', '.jpeg', '.png', '.bmp']:
                            samples.append((str(img_file), class_dir.name))
        
        return samples
    
    def __len__(self):
        return len(self.samples)
    
    def __getitem__(self, idx):
        img_path, label = self.samples[idx]
        
        if not MULTIMODAL_AVAILABLE:
            raise ImportError("PIL/torchvision not available for image processing")
        
        image = Image.open(img_path).convert('RGB')
        if self.transform:
            image = self.transform(image)
        
        return {
            'pixel_values': image,
            'labels': torch.tensor(int(label) if str(label).isdigit() else hash(label) % 1000)
        }

class AudioDataset(Dataset):
    """Dataset class for audio data"""
    def __init__(self, data_path: str, sample_rate: int = 16000):
        self.data_path = Path(data_path)
        self.sample_rate = sample_rate
        self.samples = self._load_audio_data()
    
    def _load_audio_data(self):
        """Load audio data from directory"""
        samples = []
        
        for audio_file in self.data_path.glob('**/*'):
            if audio_file.suffix.lower() in ['.wav', '.mp3', '.flac', '.ogg']:
                # Extract label from parent directory or filename
                label = audio_file.parent.name if audio_file.parent != self.data_path else "unknown"
                samples.append((str(audio_file), label))
        
        return samples
    
    def __len__(self):
        return len(self.samples)
    
    def __getitem__(self, idx):
        audio_path, label = self.samples[idx]
        
        if not MULTIMODAL_AVAILABLE:
            raise ImportError("torchaudio not available for audio processing")
        
        waveform, sr = torchaudio.load(audio_path)
        
        # Resample if necessary
        if sr != self.sample_rate:
            resampler = torchaudio.transforms.Resample(sr, self.sample_rate)
            waveform = resampler(waveform)
        
        return {
            'input_values': waveform.flatten(),
            'labels': torch.tensor(hash(label) % 1000)
        }

class ModelAligner:
    """Main class for model alignment and training"""
    
    def __init__(self, config: TrainingConfig):
        self.config = config
        self.device = torch.device('cuda' if torch.cuda.is_available() else 'cpu')
        logger.info(f"Using device: {self.device}")
        
        self.model = None
        self.tokenizer = None
        self.dataset = None
        self.trainer = None
    
    def load_pretrained_model(self, model_path: str):
        """Load a pretrained model for alignment"""
        try:
            # Determine model type and load accordingly
            if os.path.exists(os.path.join(model_path, 'config.json')):
                # HuggingFace model
                self.model = AutoModel.from_pretrained(model_path)
                try:
                    self.tokenizer = AutoTokenizer.from_pretrained(model_path)
                except:
                    logger.warning("No tokenizer found, using default")
            else:
                # Custom model
                logger.info("Loading custom model architecture")
                self.model = self._create_custom_model()
            
            self.model.to(self.device)
            logger.info(f"Loaded model from {model_path}")
            
        except Exception as e:
            logger.error(f"Error loading model: {e}")
            raise
    
    def create_model_from_scratch(self, architecture_spec: Dict[str, Any]):
        """Create a new model from scratch based on architecture specification"""
        try:
            arch_type = architecture_spec.get('Type', 'Transformer')
            
            if arch_type == 'Transformer':
                self.model = self._create_transformer_model(architecture_spec)
            elif arch_type == 'Vision Transformer (ViT)':
                self.model = self._create_vit_model(architecture_spec)
            elif arch_type == 'Convolutional Neural Network (CNN)':
                self.model = self._create_cnn_model(architecture_spec)
            elif arch_type == 'Recurrent Neural Network (RNN/LSTM)':
                self.model = self._create_rnn_model(architecture_spec)
            else:
                raise ValueError(f"Unsupported architecture: {arch_type}")
            
            self.model.to(self.device)
            logger.info(f"Created {arch_type} model from scratch")
            
        except Exception as e:
            logger.error(f"Error creating model from scratch: {e}")
            raise
    
    def _create_transformer_model(self, spec: Dict[str, Any]) -> nn.Module:
        """Create a transformer model from scratch"""
        from transformers import GPT2Config, GPT2LMHeadModel
        
        config = GPT2Config(
            vocab_size=spec.get('VocabSize', 50000),
            n_positions=spec.get('MaxSequenceLength', 512),
            n_embd=spec.get('HiddenSize', 768),
            n_layer=spec.get('Layers', 12),
            n_head=spec.get('AttentionHeads', 12)
        )
        
        return GPT2LMHeadModel(config)
    
    def _create_vit_model(self, spec: Dict[str, Any]) -> nn.Module:
        """Create a Vision Transformer model"""
        try:
            from transformers import ViTConfig, ViTForImageClassification
            
            config = ViTConfig(
                hidden_size=spec.get('HiddenSize', 768),
                num_hidden_layers=spec.get('Layers', 12),
                num_attention_heads=spec.get('AttentionHeads', 12),
                image_size=spec.get('CustomParameters', {}).get('image_size', 224),
                patch_size=spec.get('CustomParameters', {}).get('patch_size', 16),
                num_labels=1000  # Default for ImageNet
            )
            
            return ViTForImageClassification(config)
        except ImportError:
            logger.error("Vision Transformer not available")
            raise
    
    def _create_cnn_model(self, spec: Dict[str, Any]) -> nn.Module:
        """Create a CNN model"""
        class SimpleCNN(nn.Module):
            def __init__(self, num_classes=1000):
                super().__init__()
                
                filters = spec.get('CustomParameters', {}).get('filters', [32, 64, 128, 256])
                
                layers = []
                in_channels = 3
                
                for out_channels in filters:
                    layers.extend([
                        nn.Conv2d(in_channels, out_channels, 3, padding=1),
                        nn.BatchNorm2d(out_channels),
                        nn.ReLU(),
                        nn.MaxPool2d(2)
                    ])
                    in_channels = out_channels
                
                self.features = nn.Sequential(*layers)
                self.classifier = nn.Sequential(
                    nn.AdaptiveAvgPool2d((1, 1)),
                    nn.Flatten(),
                    nn.Linear(filters[-1], num_classes)
                )
            
            def forward(self, x):
                x = self.features(x)
                x = self.classifier(x)
                return x
        
        return SimpleCNN()
    
    def _create_rnn_model(self, spec: Dict[str, Any]) -> nn.Module:
        """Create an RNN/LSTM model"""
        class SimpleLSTM(nn.Module):
            def __init__(self, vocab_size=50000, embed_size=256, hidden_size=512, num_layers=2):
                super().__init__()
                
                self.embedding = nn.Embedding(vocab_size, embed_size)
                self.lstm = nn.LSTM(embed_size, hidden_size, num_layers, batch_first=True)
                self.fc = nn.Linear(hidden_size, vocab_size)
            
            def forward(self, x):
                embedded = self.embedding(x)
                lstm_out, _ = self.lstm(embedded)
                output = self.fc(lstm_out)
                return output
        
        return SimpleLSTM(
            vocab_size=spec.get('VocabSize', 50000),
            hidden_size=spec.get('HiddenSize', 512),
            num_layers=spec.get('Layers', 2)
        )
    
    def _create_custom_model(self) -> nn.Module:
        """Create a simple custom model"""
        class SimpleModel(nn.Module):
            def __init__(self):
                super().__init__()
                self.linear = nn.Linear(768, 768)
            
            def forward(self, x):
                return self.linear(x)
        
        return SimpleModel()
    
    def setup_alignment(self):
        """Setup model for alignment based on the selected technique"""
        if not self.model:
            raise ValueError("Model must be loaded before setting up alignment")
        
        technique = self.config.alignment_technique
        
        if technique == "Fine-tuning":
            self._setup_fine_tuning()
        elif technique == "Instruction Tuning":
            self._setup_instruction_tuning()
        elif technique == "RLHF (Reinforcement Learning from Human Feedback)":
            self._setup_rlhf()
        elif technique == "DPO (Direct Preference Optimization)":
            self._setup_dpo()
        elif technique == "Constitutional AI":
            self._setup_constitutional_ai()
        elif technique == "Parameter-Efficient Fine-tuning (PEFT)":
            self._setup_peft()
        else:
            logger.warning(f"Unknown alignment technique: {technique}, using fine-tuning")
            self._setup_fine_tuning()
    
    def _setup_fine_tuning(self):
        """Setup standard fine-tuning"""
        logger.info("Setting up standard fine-tuning")
        # Standard fine-tuning uses the model as-is
        
    def _setup_instruction_tuning(self):
        """Setup instruction tuning"""
        logger.info("Setting up instruction tuning")
        # Instruction tuning typically involves specific data formatting
        # and potentially adding instruction tokens
        
    def _setup_rlhf(self):
        """Setup RLHF training"""
        logger.info("Setting up RLHF")
        logger.warning("RLHF implementation is a placeholder - requires reward model and PPO")
        
    def _setup_dpo(self):
        """Setup Direct Preference Optimization"""
        logger.info("Setting up DPO")
        logger.warning("DPO implementation is a placeholder - requires preference dataset")
        
    def _setup_constitutional_ai(self):
        """Setup Constitutional AI"""
        logger.info("Setting up Constitutional AI")
        logger.warning("Constitutional AI implementation is a placeholder")
        
    def _setup_peft(self):
        """Setup Parameter-Efficient Fine-tuning"""
        if not PEFT_AVAILABLE:
            logger.error("PEFT library not available")
            return
        
        logger.info("Setting up PEFT")
        
        # Configure LoRA
        peft_config = LoraConfig(
            task_type=TaskType.CAUSAL_LM,
            inference_mode=False,
            r=16,
            lora_alpha=32,
            lora_dropout=0.1,
            target_modules=["q_proj", "v_proj"]
        )
        
        self.model = get_peft_model(self.model, peft_config)
        logger.info("Applied LoRA to model")
    
    def load_dataset(self, dataset_path: str):
        """Load and prepare dataset"""
        try:
            dataset_format = self.config.dataset_format
            
            if dataset_format == "Auto-detect":
                dataset_format = self._detect_dataset_format(dataset_path)
            
            if dataset_format in ["JSONL (Text)", "CSV"] or "Text" in dataset_format:
                self.dataset = TextDataset(dataset_path, self.tokenizer)
            elif "Image" in dataset_format:
                self.dataset = ImageDataset(dataset_path)
            elif "Audio" in dataset_format:
                self.dataset = AudioDataset(dataset_path)
            else:
                raise ValueError(f"Unsupported dataset format: {dataset_format}")
            
            logger.info(f"Loaded dataset with {len(self.dataset)} samples")
            
        except Exception as e:
            logger.error(f"Error loading dataset: {e}")
            raise
    
    def _detect_dataset_format(self, dataset_path: str) -> str:
        """Auto-detect dataset format"""
        path = Path(dataset_path)
        
        if path.suffix.lower() == '.jsonl':
            return "JSONL (Text)"
        elif path.suffix.lower() == '.csv':
            return "CSV"
        elif path.is_dir():
            # Check for image files
            image_exts = ['.jpg', '.jpeg', '.png', '.bmp', '.gif']
            audio_exts = ['.wav', '.mp3', '.flac', '.ogg']
            
            has_images = any(path.glob(f'**/*{ext}') for ext in image_exts)
            has_audio = any(path.glob(f'**/*{ext}') for ext in audio_exts)
            
            if has_images:
                return "Image Classification"
            elif has_audio:
                return "Audio Classification"
        
        return "Custom Format"
    
    def train(self):
        """Main training/alignment loop"""
        try:
            if not self.model or not self.dataset:
                raise ValueError("Model and dataset must be loaded before training")
            
            logger.info("Starting training/alignment process")
            
            # Setup training arguments
            training_args = TrainingArguments(
                output_dir="./results",
                num_train_epochs=self.config.epochs,
                per_device_train_batch_size=self.config.batch_size,
                learning_rate=self.config.learning_rate,
                warmup_steps=100,
                logging_steps=10,
                save_steps=500,
                evaluation_strategy="steps",
                eval_steps=500,
                load_best_model_at_end=True,
                metric_for_best_model="loss",
                greater_is_better=False,
            )
            
            # Create data loader
            dataloader = DataLoader(
                self.dataset,
                batch_size=self.config.batch_size,
                shuffle=True
            )
            
            # Setup optimizer
            optimizer = optim.AdamW(
                self.model.parameters(),
                lr=self.config.learning_rate
            )
            
            # Setup scheduler
            num_training_steps = len(dataloader) * self.config.epochs
            scheduler = get_linear_schedule_with_warmup(
                optimizer,
                num_warmup_steps=100,
                num_training_steps=num_training_steps
            )
            
            # Training loop
            self.model.train()
            total_loss = 0
            
            for epoch in range(self.config.epochs):
                logger.info(f"Starting epoch {epoch + 1}/{self.config.epochs}")
                
                for step, batch in enumerate(dataloader):
                    # Move batch to device
                    batch = {k: v.to(self.device) if isinstance(v, torch.Tensor) else v 
                            for k, v in batch.items()}
                    
                    # Forward pass
                    outputs = self.model(**batch)
                    loss = outputs.loss if hasattr(outputs, 'loss') else self._compute_loss(outputs, batch)
                    
                    # Backward pass
                    loss.backward()
                    optimizer.step()
                    scheduler.step()
                    optimizer.zero_grad()
                    
                    total_loss += loss.item()
                    
                    if step % 10 == 0:
                        logger.info(f"Epoch {epoch + 1}, Step {step}, Loss: {loss.item():.4f}")
                
                avg_loss = total_loss / len(dataloader)
                logger.info(f"Epoch {epoch + 1} completed. Average loss: {avg_loss:.4f}")
            
            logger.info("Training completed successfully")
            
        except Exception as e:
            logger.error(f"Error during training: {e}")
            logger.error(traceback.format_exc())
            raise
    
    def _compute_loss(self, outputs, batch):
        """Compute loss for custom models"""
        if 'labels' in batch:
            criterion = nn.CrossEntropyLoss()
            return criterion(outputs.view(-1, outputs.size(-1)), batch['labels'].view(-1))
        else:
            # Default MSE loss
            criterion = nn.MSELoss()
            return criterion(outputs, torch.zeros_like(outputs))
    
    def save_model(self, output_path: str):
        """Save the trained/aligned model"""
        try:
            os.makedirs(output_path, exist_ok=True)
            
            # Save model
            if hasattr(self.model, 'save_pretrained'):
                self.model.save_pretrained(output_path)
            else:
                torch.save(self.model.state_dict(), os.path.join(output_path, 'pytorch_model.bin'))
            
            # Save tokenizer if available
            if self.tokenizer and hasattr(self.tokenizer, 'save_pretrained'):
                self.tokenizer.save_pretrained(output_path)
            
            # Save training metadata
            metadata = {
                'training_completed': datetime.now().isoformat(),
                'config': vars(self.config),
                'model_type': type(self.model).__name__,
                'device_used': str(self.device)
            }
            
            with open(os.path.join(output_path, 'training_metadata.json'), 'w') as f:
                json.dump(metadata, f, indent=2)
            
            logger.info(f"Model saved to {output_path}")
            
        except Exception as e:
            logger.error(f"Error saving model: {e}")
            raise

def main():
    """Main training script entry point"""
    parser = argparse.ArgumentParser(description="Model Training and Alignment")
    parser.add_argument('--config', type=str, help='Path to training configuration file')
    parser.add_argument('--model_path', type=str, help='Path to pretrained model')
    parser.add_argument('--dataset_path', type=str, required=True, help='Path to training dataset')
    parser.add_argument('--output_path', type=str, required=True, help='Output path for trained model')
    parser.add_argument('--architecture_spec', type=str, help='Path to architecture specification (for training from scratch)')
    
    args = parser.parse_args()
    
    try:
        # Load configuration
        config = TrainingConfig(args.config)
        
        # Initialize model aligner
        aligner = ModelAligner(config)
        
        # Setup model
        if config.training_mode == "Train From Scratch":
            if not args.architecture_spec:
                raise ValueError("Architecture specification required for training from scratch")
            
            with open(args.architecture_spec, 'r') as f:
                arch_spec = json.load(f)
            
            aligner.create_model_from_scratch(arch_spec)
        else:
            if not args.model_path:
                raise ValueError("Model path required for alignment")
            
            aligner.load_pretrained_model(args.model_path)
            aligner.setup_alignment()
        
        # Load dataset
        aligner.load_dataset(args.dataset_path)
        
        # Train
        aligner.train()
        
        # Save result
        aligner.save_model(args.output_path)
        
        logger.info("Training completed successfully!")
        
    except Exception as e:
        logger.error(f"Training failed: {e}")
        logger.error(traceback.format_exc())
        sys.exit(1)

if __name__ == "__main__":
    main()
