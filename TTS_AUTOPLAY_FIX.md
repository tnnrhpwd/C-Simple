# TTS Autoplay Fix - ReadAloudOnCompletion Toggle

## Problem Description
The TTS autoplay toggle (`ReadAloudOnCompletion`) was not working properly. When users enabled the "Read aloud on completion" checkbox, the text would only play when manually pressing the play button, not automatically after model execution.

## Root Cause Analysis
The issue was in the `ReadActionContentAloudAsync` method in `OrientPageViewModel.cs`. This method was responsible for triggering TTS after model execution, but it had flawed logic:

### Original Problematic Code
```csharp
private async Task ReadActionContentAloudAsync(NodeViewModel actionNode, string content, string contentType)
{
    try
    {
        if (actionNode?.Classification?.ToLowerInvariant() == "action" &&
            contentType?.ToLowerInvariant() == "text" &&
            !string.IsNullOrWhiteSpace(content))
        {
            Debug.WriteLine($"[ReadActionContentAloud] Reading action content aloud: {content.Substring(0, Math.Min(content.Length, 100))}...");
            await _audioStepContentService.PlayStepContentAsync(content, contentType, actionNode);
        }
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"[ReadActionContentAloud] Error reading action content aloud: {ex.Message}");
    }
}
```

**Problem**: The method only checked for `Classification == "action"` but ignored the user's `ReadAloudOnCompletion` setting.

## Solution Implemented
Enhanced the `ReadActionContentAloudAsync` method to properly handle both scenarios:

### Fixed Code
```csharp
private async Task ReadActionContentAloudAsync(NodeViewModel actionNode, string content, string contentType)
{
    try
    {
        // Check if content should be read aloud - either action node OR user enabled autoplay
        bool shouldReadAloud = false;
        string reason = "";

        if (contentType?.ToLowerInvariant() == "text" && !string.IsNullOrWhiteSpace(content))
        {
            // Check for action classification (automatic TTS)
            if (actionNode?.Classification?.ToLowerInvariant() == "action")
            {
                shouldReadAloud = true;
                reason = "Action-classified model";
            }
            // Check for user-enabled autoplay toggle
            else if (actionNode?.ReadAloudOnCompletion == true)
            {
                shouldReadAloud = true;
                reason = "User-enabled autoplay";
            }
        }

        if (shouldReadAloud)
        {
            Debug.WriteLine($"[ReadActionContentAloud] Reading content aloud ({reason}): {content.Substring(0, Math.Min(content.Length, 100))}...");
            await _audioStepContentService.PlayStepContentAsync(content, contentType, actionNode);
        }
        else
        {
            Debug.WriteLine($"[ReadActionContentAloud] Skipping TTS - not action node and autoplay not enabled for '{actionNode?.Name}'");
        }
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"[ReadActionContentAloud] Error reading action content aloud: {ex.Message}");
    }
}
```

## Key Improvements

1. **Dual Condition Check**: Now properly checks both:
   - Action-classified models (automatic TTS)
   - User-enabled autoplay toggle (`ReadAloudOnCompletion`)

2. **Better Debugging**: Added detailed logging to show which condition triggered TTS or why it was skipped

3. **Clear Logic Flow**: Restructured the conditional logic to be more readable and maintainable

## Integration Points
This method is called from three locations:

1. **ExecuteModelForStepAsync** (batch execution): Line 2890
2. **ExecuteGenerateAsync** (single model execution): Line 3230
3. **Manual action processing**: Line 2871

All three calling locations already have the correct logic to call this method for both action models and user-enabled autoplay.

## Testing Verification
After the fix:
- ✅ Action-classified models still auto-play (existing behavior preserved)
- ✅ Non-action models with `ReadAloudOnCompletion=true` now auto-play (bug fixed)
- ✅ Non-action models with `ReadAloudOnCompletion=false` don't auto-play (expected behavior)
- ✅ Manual play button still works for all scenarios

## Files Modified
- `c:\Users\tanne\Documents\Github\C-Simple\src\CSimple\ViewModels\OrientPageViewModel.cs`
  - Enhanced `ReadActionContentAloudAsync` method (lines ~3535-3570)

## Build Status
✅ Build successful with no new errors or warnings
