#!/usr/bin/env python3
"""
Test script to verify model alignment file creation
"""
import os
import json
import sys
from pathlib import Path

def check_model_folder(model_path):
    """Check if a model folder has the expected files and structure"""
    
    if not os.path.exists(model_path):
        print(f"❌ Model folder does not exist: {model_path}")
        return False
    
    print(f"🔍 Checking model folder: {model_path}")
    
    # Expected files
    expected_files = [
        "pytorch_model.bin",
        "config.json",
        "alignment_info.json"
    ]
    
    optional_files = [
        "model.safetensors",
        "README.md",
        "model_card.json",
        "tokenizer.json",
        "vocab.txt"
    ]
    
    issues = []
    
    # Check for required files
    for file in expected_files:
        file_path = os.path.join(model_path, file)
        if not os.path.exists(file_path):
            issues.append(f"Missing required file: {file}")
        else:
            size = os.path.getsize(file_path)
            print(f"✅ {file}: {size / (1024*1024):.2f} MB")
            
            # Special checks for specific files
            if file == "pytorch_model.bin" and size < 1000000:  # Less than 1MB
                issues.append(f"pytorch_model.bin is too small ({size} bytes) - likely placeholder text")
            
            if file == "config.json":
                try:
                    with open(file_path, 'r') as f:
                        config = json.load(f)
                        if not config or config == {}:
                            issues.append("config.json is empty or invalid")
                        else:
                            print(f"   📋 Config keys: {list(config.keys())}")
                except:
                    issues.append("config.json is not valid JSON")
    
    # Check optional files
    for file in optional_files:
        file_path = os.path.join(model_path, file)
        if os.path.exists(file_path):
            size = os.path.getsize(file_path)
            print(f"✅ {file}: {size / (1024*1024):.2f} MB")
    
    # List all files in the directory
    all_files = []
    for root, dirs, files in os.walk(model_path):
        for file in files:
            full_path = os.path.join(root, file)
            rel_path = os.path.relpath(full_path, model_path)
            size = os.path.getsize(full_path)
            all_files.append((rel_path, size))
    
    print(f"\n📊 Total files in model: {len(all_files)}")
    total_size = sum(size for _, size in all_files)
    print(f"📊 Total size: {total_size / (1024*1024):.2f} MB")
    
    if issues:
        print(f"\n❌ Issues found:")
        for issue in issues:
            print(f"   • {issue}")
        return False
    else:
        print(f"\n✅ Model folder appears to be properly created!")
        return True

def main():
    # Check the AlignedModels directory
    aligned_models_dir = r"C:\Users\tanne\Documents\CSimple\Resources\AlignedModels"
    
    if not os.path.exists(aligned_models_dir):
        print(f"❌ AlignedModels directory does not exist: {aligned_models_dir}")
        return
    
    print(f"🔍 Scanning AlignedModels directory: {aligned_models_dir}")
    
    model_folders = [d for d in os.listdir(aligned_models_dir) 
                    if os.path.isdir(os.path.join(aligned_models_dir, d))]
    
    print(f"📁 Found {len(model_folders)} model folders:")
    
    for folder in model_folders:
        print(f"\n{'='*60}")
        model_path = os.path.join(aligned_models_dir, folder)
        result = check_model_folder(model_path)
        
        if not result:
            print(f"❌ {folder}: Has issues")
        else:
            print(f"✅ {folder}: Looks good")

if __name__ == "__main__":
    main()
