#!/usr/bin/env python3
"""
Simple script to test if webcam is accessible using OpenCV
"""

import cv2
import os
from datetime import datetime

def test_webcam():
    print("Testing webcam access...")
    
    # Try to open the webcam
    cap = cv2.VideoCapture(0)
    
    if not cap.isOpened():
        print("❌ Failed to open webcam")
        return False
    
    print("✅ Webcam opened successfully")
    
    # Try to read a frame
    ret, frame = cap.read()
    
    if not ret:
        print("❌ Failed to read frame from webcam")
        cap.release()
        return False
        
    print(f"✅ Frame captured successfully - Shape: {frame.shape}")
    
    # Try to save the frame
    output_dir = r"C:\Users\tanne\Documents\CSimple\Resources\WebcamImages"
    if not os.path.exists(output_dir):
        os.makedirs(output_dir)
        print(f"✅ Created directory: {output_dir}")
    
    filename = f"TestWebcamImage_{datetime.now().strftime('%Y%m%d_%H%M%S')}.jpg"
    filepath = os.path.join(output_dir, filename)
    
    success = cv2.imwrite(filepath, frame)
    
    if success and os.path.exists(filepath):
        print(f"✅ Image saved successfully: {filepath}")
        print(f"   File size: {os.path.getsize(filepath)} bytes")
    else:
        print(f"❌ Failed to save image: {filepath}")
    
    cap.release()
    print("Webcam released")
    
    return True

if __name__ == "__main__":
    test_webcam()
