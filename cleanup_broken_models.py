#!/usr/bin/env python3
"""
Cleanup script to remove broken model folders and allow testing of new model creation
"""
import os
import shutil
from pathlib import Path

def cleanup_broken_models():
    """Remove model folders that have placeholder files instead of realistic weights"""
    
    aligned_models_dir = r"C:\Users\tanne\Documents\CSimple\Resources\AlignedModels"
    
    if not os.path.exists(aligned_models_dir):
        print(f"❌ AlignedModels directory does not exist: {aligned_models_dir}")
        return
    
    print(f"🔍 Scanning for broken models in: {aligned_models_dir}")
    
    model_folders = [d for d in os.listdir(aligned_models_dir) 
                    if os.path.isdir(os.path.join(aligned_models_dir, d))]
    
    broken_models = []
    
    for folder in model_folders:
        model_path = os.path.join(aligned_models_dir, folder)
        pytorch_file = os.path.join(model_path, "pytorch_model.bin")
        
        if os.path.exists(pytorch_file):
            size = os.path.getsize(pytorch_file)
            print(f"📊 {folder}: pytorch_model.bin = {size} bytes")
            
            # If the file is less than 1MB, it's likely a placeholder
            if size < 1000000:  # Less than 1MB
                print(f"❌ {folder}: Detected as broken (size: {size} bytes)")
                broken_models.append((folder, model_path, size))
            else:
                print(f"✅ {folder}: Looks good (size: {size / (1024*1024):.2f} MB)")
        else:
            print(f"❓ {folder}: No pytorch_model.bin found")
            broken_models.append((folder, model_path, 0))
    
    if not broken_models:
        print("\n✅ No broken models found!")
        return
    
    print(f"\n🗑️  Found {len(broken_models)} broken models:")
    for folder, path, size in broken_models:
        print(f"   • {folder} ({size} bytes)")
    
    # Ask for confirmation (in real scenario)
    print(f"\n🧹 Removing broken models...")
    
    for folder, path, size in broken_models:
        try:
            print(f"   Removing {folder}...")
            shutil.rmtree(path)
            print(f"   ✅ Removed {folder}")
        except Exception as e:
            print(f"   ❌ Failed to remove {folder}: {e}")
    
    print(f"\n✅ Cleanup complete!")

if __name__ == "__main__":
    cleanup_broken_models()
