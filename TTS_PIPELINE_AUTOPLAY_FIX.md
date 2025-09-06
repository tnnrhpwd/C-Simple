# TTS Autoplay Complete Fix - Pipeline Execution Support

## Problem Analysis
The user reported that TTS autoplay was still not working during pipeline execution (using "Run All Models"), even with the autoplay toggle enabled. Looking at the debug output, I could see that:

1. ✅ Model execution was completing successfully via `ExecuteAllModelsOptimizedAsync`
2. ❌ **No TTS debug output was appearing**, indicating TTS logic wasn't being called
3. ❌ **Pipeline execution was bypassing TTS logic entirely**

## Root Cause Discovery
The issue was that TTS autoplay logic was only implemented in:
- ✅ `OrientPageViewModel.ExecuteGenerateAsync` (single model execution)
- ✅ `OrientPageViewModel.ExecuteModelForStepAsync` (direct model execution)

But **NOT** in:
- ❌ `PipelineExecutionService.ExecuteAllModelsOptimizedAsync` (batch model execution)

When the user runs "Run All Models", it uses the `PipelineExecutionService` which was missing the TTS autoplay functionality.

## Complete Solution Implemented

### 1. Enhanced PipelineExecutionService Constructor
**File**: `PipelineExecutionService.cs`

Added AudioStepContentService dependency:
```csharp
public PipelineExecutionService(
    EnsembleModelService ensembleModelService, 
    Func<NodeViewModel, NeuralNetworkModel> findCorrespondingModelFunc, 
    AudioStepContentService audioStepContentService = null)
{
    _ensembleModelService = ensembleModelService ?? throw new ArgumentNullException(nameof(ensembleModelService));
    _findCorrespondingModelFunc = findCorrespondingModelFunc ?? throw new ArgumentNullException(nameof(findCorrespondingModelFunc));
    _audioStepContentService = audioStepContentService; // Optional - can be null if TTS not available
}
```

### 2. Added TTS Autoplay Method
**File**: `PipelineExecutionService.cs`

Implemented comprehensive TTS logic:
```csharp
private Task TriggerTtsAutoplayIfEnabledAsync(NodeViewModel modelNode, string result, string resultContentType)
{
    try
    {
        // Check if TTS service is available
        if (_audioStepContentService == null)
        {
            return Task.CompletedTask; // TTS not available, skip
        }

        // Check if content should be read aloud - either action node OR user enabled autoplay
        bool shouldReadAloud = false;
        string reason = "";

        if (resultContentType?.ToLowerInvariant() == "text" && !string.IsNullOrWhiteSpace(result))
        {
            // Check for action classification (automatic TTS)
            if (modelNode?.Classification?.ToLowerInvariant() == "action")
            {
                shouldReadAloud = true;
                reason = "Action-classified model";
            }
            // Check for user-enabled autoplay toggle
            else if (modelNode?.ReadAloudOnCompletion == true)
            {
                shouldReadAloud = true;
                reason = "User-enabled autoplay";
            }
        }

        if (shouldReadAloud)
        {
            Debug.WriteLine($"[PipelineExecutionService] Reading content aloud ({reason}): {result.Substring(0, Math.Min(result.Length, 100))}...");
            
            // Run TTS in background to avoid blocking pipeline execution
            _ = Task.Run(async () =>
            {
                try
                {
                    await _audioStepContentService.PlayStepContentAsync(result, resultContentType, modelNode);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[PipelineExecutionService] Error during TTS playback: {ex.Message}");
                }
            });
        }
        else
        {
            Debug.WriteLine($"[PipelineExecutionService] Skipping TTS - not action node and autoplay not enabled for '{modelNode?.Name}'");
        }
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"[PipelineExecutionService] Error in TTS autoplay logic: {ex.Message}");
    }

    return Task.CompletedTask;
}
```

### 3. Integrated TTS Calls Into Execution Paths
**File**: `PipelineExecutionService.cs`

Added TTS triggers in **both** execution paths:

#### A. Main Dynamic Input Execution
```csharp
if (!string.IsNullOrEmpty(result))
{
    // Determine content type and store result
    string resultContentType = _ensembleModelService.DetermineResultContentType(model, result);
    modelNode.SetStepOutput(stepIndex, resultContentType, result);

    // Trigger TTS autoplay if enabled
    await TriggerTtsAutoplayIfEnabledAsync(modelNode, result, resultContentType);

    // Propagate output to connected File nodes for memory saving
    await PropagateOutputToConnectedFileNodesAsync(modelNode, result, currentActionStep, connections, nodes);
    
    return true;
}
```

#### B. Fallback Execution Path
```csharp
if (!string.IsNullOrEmpty(result))
{
    modelNode.SetStepOutput(currentActionStep + 1, "text", result);

    // Trigger TTS autoplay if enabled
    await TriggerTtsAutoplayIfEnabledAsync(modelNode, result, "text");

    // Propagate output to connected File nodes for memory saving
    await PropagateOutputToConnectedFileNodesAsync(modelNode, result, currentActionStep, connections, nodes);
    
    return true;
}
```

### 4. Updated OrientPageViewModel Dependency Injection
**File**: `OrientPageViewModel.cs`

Enhanced constructor call to pass TTS service:
```csharp
// Initialize pipeline execution service with dependency injection
_pipelineExecutionService = new PipelineExecutionService(
    _ensembleModelService,
    (node) => FindCorrespondingModel(((App)Application.Current)?.NetPageViewModel, node),
    _audioStepContentService  // Pass the TTS service for autoplay functionality
);
```

## Key Features of the Fix

### 🎯 **Comprehensive Coverage**
- ✅ Single model execution (`ExecuteGenerateAsync`)
- ✅ Direct model execution (`ExecuteModelForStepAsync`) 
- ✅ **Pipeline batch execution (`ExecuteAllModelsOptimizedAsync`)** ← **Fixed**

### 🔧 **Robust Implementation**
- ✅ **Action models**: Automatic TTS (existing behavior preserved)
- ✅ **User autoplay toggle**: TTS when `ReadAloudOnCompletion = true` ← **Fixed**
- ✅ **Graceful fallback**: Works even if TTS service is null
- ✅ **Non-blocking**: TTS runs in background, doesn't block pipeline execution

### 🐛 **Enhanced Debugging**
- ✅ Clear debug output showing which condition triggered TTS
- ✅ Specific logging for pipeline execution TTS
- ✅ Error handling with detailed debug messages

## Expected Behavior After Fix

When you run "Run All Models" with autoplay enabled:

1. **Pipeline executes models** via `ExecuteAllModelsOptimizedAsync`
2. **For each model that completes**:
   - ✅ Checks if it's an action model (automatic TTS)
   - ✅ **Checks if user enabled autoplay toggle** (`ReadAloudOnCompletion`)
   - ✅ **Triggers TTS automatically** if either condition is true
3. **Debug output will show**:
   ```
   [PipelineExecutionService] Reading content aloud (User-enabled autoplay): <text content>...
   [AudioStepContentService] Reading text aloud using TTS: <text content>...
   [WindowsTtsService] Speaking text: <text content>...
   ```

## Files Modified

1. **`src/CSimple/Services/PipelineExecutionService.cs`**
   - Added `AudioStepContentService` dependency
   - Added `TriggerTtsAutoplayIfEnabledAsync` method
   - Integrated TTS calls into both execution paths

2. **`src/CSimple/ViewModels/OrientPageViewModel.cs`**
   - Updated PipelineExecutionService constructor call
   - Enhanced `ReadActionContentAloudAsync` method (previous fix)

## Build Status
✅ **Build successful** with no new errors (78 warnings preserved)

## Testing Ready
The fix is now complete and ready for testing. When you:
1. Enable the "Read aloud on completion" toggle on any model
2. Run "Run All Models" 
3. The text should automatically be read aloud on completion

You should now see TTS debug output in the console during pipeline execution, and the autoplay toggle should work properly for batch model execution!
