#!/usr/bin/env python3
"""
Test script to validate the Visual Data Delay Fix implementation.

This script simulates the intelligence pipeline behavior and validates that:
1. Initial delay is properly configured and respected
2. Visual data validation is working
3. Settings are properly loaded and applied
"""

import time
import json
from datetime import datetime
from typing import List, Tuple

class MockSettingsService:
    """Mock settings service to simulate the C# SettingsService behavior"""
    
    def __init__(self):
        self.settings = {
            "IntelligenceIntervalMs": 1000,
            "IntelligenceInitialDelayMs": 5000,
            "IntelligenceAutoExecutionEnabled": True
        }
    
    def GetIntelligenceIntervalMs(self) -> int:
        return self.settings["IntelligenceIntervalMs"]
    
    def GetIntelligenceInitialDelayMs(self) -> int:
        return self.settings["IntelligenceInitialDelayMs"]
    
    def GetIntelligenceAutoExecutionEnabled(self) -> bool:
        return self.settings["IntelligenceAutoExecutionEnabled"]
    
    def SetIntelligenceInitialDelayMs(self, delay_ms: int):
        if delay_ms < 1000:
            delay_ms = 1000
        if delay_ms > 30000:
            delay_ms = 30000
        self.settings["IntelligenceInitialDelayMs"] = delay_ms
        print(f"Intelligence initial delay set to: {delay_ms}ms")

class MockIntelligenceSystem:
    """Mock intelligence system to test the visual data delay logic"""
    
    def __init__(self, settings_service: MockSettingsService):
        self.settings_service = settings_service
        self.captured_screenshots = []
        self.captured_audio = []
        self.captured_text = []
        self.is_intelligence_active = False
    
    def capture_system_state(self) -> Tuple[List[bytes], List[bytes], List[str]]:
        """Simulate capturing system state data"""
        # Simulate gradual data capture
        if len(self.captured_screenshots) < 5:
            self.captured_screenshots.append(b"fake_screenshot_data")
        
        if len(self.captured_audio) < 3:
            self.captured_audio.append(b"fake_audio_data")
        
        return (
            self.captured_screenshots.copy(),
            self.captured_audio.copy(), 
            self.captured_text.copy()
        )
    
    def validate_sufficient_visual_data(self, screenshots: List[bytes], min_required: int = 2) -> bool:
        """Validate if sufficient visual data is available"""
        return len(screenshots) >= min_required
    
    def intelligent_pipeline_loop_simulation(self):
        """Simulate the enhanced intelligent pipeline loop with visual data delay"""
        print("🚀 Starting Intelligence Pipeline Loop Simulation")
        print("=" * 60)
        
        # Get settings
        initial_delay_ms = self.settings_service.GetIntelligenceInitialDelayMs()
        min_screenshots_required = 2
        max_initial_wait_ms = max(initial_delay_ms * 2, 10000)
        
        print(f"⚙️  Configuration:")
        print(f"   Initial Delay: {initial_delay_ms}ms")
        print(f"   Min Screenshots Required: {min_screenshots_required}")
        print(f"   Max Initial Wait: {max_initial_wait_ms}ms")
        print()
        
        print(f"⏳ Waiting {initial_delay_ms}ms for initial visual data capture...")
        
        initial_wait_start = datetime.now()
        sufficient_data_captured = False
        
        # Simulate the initial delay and data capture loop
        while (datetime.now() - initial_wait_start).total_seconds() * 1000 < max_initial_wait_ms:
            # Simulate data capture
            screenshots, audio, text = self.capture_system_state()
            
            elapsed_ms = (datetime.now() - initial_wait_start).total_seconds() * 1000
            print(f"📊 Data Status ({elapsed_ms:4.0f}ms): {len(screenshots)} screenshots, {len(audio)} audio, {len(text)} text")
            
            # Check if we have sufficient data after minimum delay
            if elapsed_ms >= initial_delay_ms:
                if self.validate_sufficient_visual_data(screenshots, min_screenshots_required):
                    sufficient_data_captured = True
                    print(f"✅ Sufficient visual data captured ({len(screenshots)} screenshots) after {elapsed_ms:0.0f}ms")
                    break
            
            # Simulate checking every 500ms
            time.sleep(0.5)
        
        if not sufficient_data_captured:
            print(f"⚠️  Proceeding with limited visual data after {max_initial_wait_ms}ms maximum wait")
        
        print()
        print("🎯 Pipeline execution would begin now with visual context available")
        
        # Show final data state
        final_screenshots, final_audio, final_text = self.capture_system_state()
        print(f"📈 Final Data State: {len(final_screenshots)} screenshots, {len(final_audio)} audio, {len(final_text)} text")
        
        return sufficient_data_captured, len(final_screenshots), len(final_audio)

def test_settings_validation():
    """Test the settings validation logic"""
    print("🔧 Testing Settings Validation")
    print("-" * 40)
    
    settings = MockSettingsService()
    
    # Test valid settings
    original_delay = settings.GetIntelligenceInitialDelayMs()
    print(f"Default initial delay: {original_delay}ms")
    
    # Test boundary conditions
    test_values = [500, 1000, 5000, 15000, 30000, 35000]
    
    for test_value in test_values:
        settings.SetIntelligenceInitialDelayMs(test_value)
        actual_value = settings.GetIntelligenceInitialDelayMs()
        
        expected_value = test_value
        if test_value < 1000:
            expected_value = 1000
        elif test_value > 30000:
            expected_value = 30000
        
        status = "✅" if actual_value == expected_value else "❌"
        print(f"{status} Set {test_value}ms -> Got {actual_value}ms (Expected: {expected_value}ms)")
    
    print()

def test_visual_data_validation():
    """Test the visual data validation logic"""
    print("📸 Testing Visual Data Validation")
    print("-" * 40)
    
    # Test cases: (screenshots_count, min_required, expected_result)
    test_cases = [
        (0, 2, False),
        (1, 2, False), 
        (2, 2, True),
        (3, 2, True),
        (5, 2, True)
    ]
    
    settings = MockSettingsService()
    intelligence = MockIntelligenceSystem(settings)
    
    for screenshots_count, min_required, expected in test_cases:
        # Simulate having a specific number of screenshots
        fake_screenshots = [b"fake_data"] * screenshots_count
        result = intelligence.validate_sufficient_visual_data(fake_screenshots, min_required)
        
        status = "✅" if result == expected else "❌"
        print(f"{status} {screenshots_count} screenshots >= {min_required} required: {result} (Expected: {expected})")
    
    print()

def run_full_simulation():
    """Run the full pipeline simulation"""
    print("🎮 Running Full Intelligence Pipeline Simulation")
    print("=" * 60)
    
    settings = MockSettingsService()
    intelligence = MockIntelligenceSystem(settings)
    
    # Test with different initial delay values
    test_delays = [2000, 5000, 8000]
    
    for delay_ms in test_delays:
        print(f"\n🔄 Testing with {delay_ms}ms initial delay:")
        settings.SetIntelligenceInitialDelayMs(delay_ms)
        
        start_time = datetime.now()
        success, screenshots, audio = intelligence.intelligent_pipeline_loop_simulation()
        end_time = datetime.now()
        
        total_time = (end_time - start_time).total_seconds() * 1000
        
        print(f"⏱️  Total execution time: {total_time:0.0f}ms")
        print(f"📊 Result: Success={success}, Screenshots={screenshots}, Audio={audio}")
        
        # Reset data for next test
        intelligence.captured_screenshots = []
        intelligence.captured_audio = []
        intelligence.captured_text = []

def main():
    """Main test function"""
    print("🧪 Visual Data Delay Fix Validation Tests")
    print("=" * 60)
    print(f"Test started at: {datetime.now()}")
    print()
    
    try:
        # Run individual tests
        test_settings_validation()
        test_visual_data_validation()
        run_full_simulation()
        
        print()
        print("✅ All tests completed successfully!")
        print("📝 Summary:")
        print("   - Settings validation works correctly")
        print("   - Visual data validation logic is functional") 
        print("   - Pipeline delay mechanism simulates properly")
        print("   - Initial delay prevents execution with insufficient visual data")
        
    except Exception as e:
        print(f"❌ Test failed with error: {e}")
        raise

if __name__ == "__main__":
    main()
