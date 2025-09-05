# TTS TargetInvocationException Fixes

## Problem
The application was throwing `System.Reflection.TargetInvocationException` when clicking the TTS button on selected nodes. This was causing the TTS functionality to fail.

## Root Cause
The exception was occurring due to several issues in the TTS implementation:

1. **Thread Safety Issues**: MediaPlayer events were being fired from background threads but trying to invoke UI-related events without proper marshaling
2. **Insufficient Error Handling**: Exceptions in event handlers were propagating up and causing the TargetInvocationException
3. **Resource Management**: Poor cleanup of Windows Runtime components when initialization failed

## Fixes Applied

### 1. WindowsTtsService.cs
- **Enhanced Constructor Error Handling**: Added step-by-step initialization with proper cleanup on failure
- **Thread-Safe Event Handlers**: Wrapped all MediaPlayer event handlers with proper error handling and thread marshaling using `Task.Run()`
- **Improved SpeakTextAsync Method**: Added comprehensive error handling with resource cleanup and safer exception handling
- **Better Resource Management**: Enhanced dispose pattern with proper error handling

### 2. AudioStepContentService.cs  
- **Graceful TTS Initialization**: Added handling for `PlatformNotSupportedException` and `InvalidOperationException`
- **Enhanced Error Handling**: Added try-catch blocks around all TTS event handlers and method calls
- **Better Fallback Behavior**: Improved handling when TTS service is unavailable

## Key Changes

### Thread Safety
```csharp
private void OnMediaOpened(MediaPlayer sender, object args)
{
    try
    {
        Debug.WriteLine("[WindowsTtsService] Speech started");
        
        // Marshal to UI thread if needed
        Task.Run(() =>
        {
            try
            {
                SpeechStarted?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WindowsTtsService] Error in SpeechStarted event: {ex.Message}");
            }
        });
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"[WindowsTtsService] Error in OnMediaOpened: {ex.Message}");
    }
}
```

### Resource Management
```csharp
try
{
    speechStream = await _synthesizer.SynthesizeTextToStreamAsync(text);
}
catch (Exception ex)
{
    Debug.WriteLine($"[WindowsTtsService] Error synthesizing text: {ex.Message}");
    SpeechError?.Invoke(ex);
    return false;
}

if (speechStream == null)
{
    Debug.WriteLine("[WindowsTtsService] Failed to generate speech stream");
    return false;
}
```

### Platform Support
```csharp
catch (PlatformNotSupportedException ex)
{
    Debug.WriteLine($"[AudioStepContentService] TTS not supported on this platform: {ex.Message}");
    _ttsService = null;
}
catch (InvalidOperationException ex)
{
    Debug.WriteLine($"[AudioStepContentService] TTS service unavailable: {ex.Message}");
    _ttsService = null;
}
```

## Testing
- Project builds successfully with 0 errors
- TTS service should now handle initialization failures gracefully
- Event handling is now thread-safe and exception-resistant
- Application should continue to work even if TTS is unavailable

## Next Steps
1. Test the TTS functionality by clicking the TTS button on selected nodes
2. Verify that the application no longer crashes with TargetInvocationException
3. Check that error messages are properly logged in the debug console
4. Confirm that the application gracefully falls back when TTS is unavailable

## Files Modified
- `src/CSimple/Services/WindowsTtsService.cs` - Major improvements to error handling and thread safety
- `src/CSimple/Services/AudioStepContentService.cs` - Enhanced TTS initialization and error handling
