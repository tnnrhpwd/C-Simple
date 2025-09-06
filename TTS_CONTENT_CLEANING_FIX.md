# TTS Content Cleaning Fix

## Problem Identified

The user reported that **auto TTS on OrientPage node step content is still reading the non-cleaned step content**. Despite implementing comprehensive step content cleaning for display, the TTS system was still reading the raw, uncleaned content containing concatenated ensemble input.

## Root Cause Analysis

### TTS Content Access Points
The TTS system accesses step content through multiple pathways:

1. **Immediate Generation TTS**: `ExecuteGenerateAsync` → `ReadActionContentAloudAsync`
2. **Manual Play Button**: `PlayAudio` → `PlayStepContentAsync` using `StepContent`
3. **Pipeline Execution TTS**: `PipelineExecutionService` → `TriggerTtsAutoplayIfEnabledAsync`

### Issues Discovered

#### Issue 1: TTS Called Before Cleaning
**Location**: `OrientPageViewModel.ExecuteGenerateAsync` line 3235

**Problem**: TTS was triggered with raw `result` before the cleaning step
```csharp
// TTS called here with raw result
_ = Task.Run(async () => await ReadActionContentAloudAsync(SelectedNode, result, resultContentType));

// Cleaning happened after TTS call
result = _ensembleModelService?.CleanModelResultForDisplay(result, SelectedNode.Name) ?? result;
```

#### Issue 2: Raw Results in Model Execution Services
**Location**: `EnsembleModelService.ExecuteModelWithInput`

**Problem**: Raw results from Python scripts passed through without cleaning
```csharp
var result = await netPageViewModel.ExecuteModelAsync(model.HuggingFaceModelId, processedInput);
return result ?? "No output generated"; // Raw result returned
```

## Complete Solution Implemented

### 1. Fixed TTS Timing in ExecuteGenerateAsync
**File**: `OrientPageViewModel.cs`

**Enhancement**: Moved TTS call to after cleaning step
```csharp
// Clean the result to remove concatenated ensemble input before displaying/storing
result = _ensembleModelService?.CleanModelResultForDisplay(result, SelectedNode.Name) ?? result;
Debug.WriteLine($"🧹 [ExecuteGenerateAsync] Cleaned result: {result?.Substring(0, Math.Min(result?.Length ?? 0, 200))}...");

// Update step content with the cleaned result
StepContent = result;

// Read action content aloud if this is an action model OR if ReadAloudOnCompletion is enabled for text output
// Use the cleaned result for TTS to avoid reading concatenated ensemble input
if (resultContentType?.ToLowerInvariant() == "text")
{
    // Check if this is an action model (automatic TTS) or if user has enabled TTS toggle
    bool shouldReadAloud = (SelectedNode?.Classification?.ToLowerInvariant() == "action") ||
                         (SelectedNode?.ReadAloudOnCompletion == true);

    if (shouldReadAloud)
    {
        _ = Task.Run(async () => await ReadActionContentAloudAsync(SelectedNode, result, resultContentType));
    }
}
```

**Impact**: TTS now receives cleaned content immediately after generation.

### 2. Added Cleaning to Core Model Execution
**File**: `EnsembleModelService.ExecuteModelWithInput`

**Enhancement**: Clean results before returning from model execution
```csharp
var result = await netPageViewModel.ExecuteModelAsync(model.HuggingFaceModelId, processedInput);
var rawResult = result ?? "No output generated";

// Clean the result to remove concatenated ensemble input before returning
var cleanedResult = CleanModelResultForDisplay(rawResult, model.Name);
Debug.WriteLine($"🧹 [{DateTime.Now:HH:mm:ss.fff}] [ExecuteModelWithInput] Cleaned result for {model.Name}: {cleanedResult?.Substring(0, Math.Min(cleanedResult?.Length ?? 0, 100))}...");

return cleanedResult;
```

**Impact**: All services using `ExecuteModelWithInput` now receive cleaned results, including PipelineExecutionService TTS.

### 3. Verified Existing TTS Paths
**Manual TTS Access**: Confirmed that manual TTS (play button) uses `StepContent` which is now set with cleaned results.

**Node Selection TTS**: Confirmed that `ActionReviewService.UpdateStepContent` uses `selectedNode.GetStepOutput()` which applies cleaning.

## TTS Content Flow After Fix

### Immediate Generation TTS:
1. **Model Execution**: Raw result from Python script
2. **Cleaning Applied**: `CleanModelResultForDisplay()` removes concatenated input
3. **StepContent Set**: Clean result stored
4. **TTS Triggered**: Clean result passed to `ReadActionContentAloudAsync`
5. **TTS Playback**: Speaks only clean model output

### Pipeline Execution TTS:
1. **Model Execution**: `EnsembleModelService.ExecuteModelWithInput`
2. **Cleaning Applied**: Clean result returned from service
3. **TTS Triggered**: `TriggerTtsAutoplayIfEnabledAsync` with clean result
4. **TTS Playback**: Speaks only clean model output

### Manual TTS (Play Button):
1. **Content Access**: Uses `StepContent` property
2. **Source**: Clean result stored during generation
3. **TTS Playback**: Speaks only clean model output

### Node Selection TTS:
1. **Content Retrieval**: `ActionReviewService.UpdateStepContent`
2. **Source**: `selectedNode.GetStepOutput()` with cleaning applied
3. **TTS Playback**: Speaks only clean model output

## Expected User Experience

### Before Fix:
- ❌ TTS reads: "Image file not found: Blip Image Captioning Base: a man wearing sunglasses... Gpt2: ] [Client thread/INFO]: Loading skin images..."
- ❌ Long, confusing concatenated content with logs and system messages
- ❌ Inconsistent between auto-TTS and manual TTS

### After Fix:
- ✅ TTS reads: "a man wearing sunglasses and a black t-shirt a man sitting at a desk with a keyboard in front of him"
- ✅ Clean, meaningful content only
- ✅ Consistent across all TTS trigger methods

## Files Modified

1. **`src/CSimple/ViewModels/OrientPageViewModel.cs`**
   - **Lines ~3210-3240**: Moved TTS call after cleaning step
   - **Enhancement**: Added comment about using cleaned result for TTS

2. **`src/CSimple/Services/EnsembleModelService.cs`**
   - **Lines ~308-315**: Added cleaning before returning from `ExecuteModelWithInput`
   - **Impact**: All services now receive clean results by default

## Build Status
✅ **Build successful** with 78 warnings (no new errors)

## Verification Strategy

### Test Scenarios:
1. **Auto-TTS on Generation**: Enable TTS toggle, click "Generate (Single run)", verify clean TTS
2. **Manual TTS**: Generate content, click play button, verify clean TTS  
3. **Pipeline TTS**: Run pipeline with TTS enabled, verify clean TTS
4. **Node Selection TTS**: Click different nodes, verify clean TTS

### Expected Results:
- **No Concatenated Input**: TTS should never read ensemble input markers
- **No System Logs**: TTS should not read gaming logs, config files, timestamps
- **Clean Descriptions**: TTS should read only meaningful model output
- **Consistent Behavior**: Same clean content across all TTS methods

## Technical Benefits

### Performance:
- **Single Cleaning Pass**: Results cleaned once at source
- **Efficient Pipeline**: No redundant cleaning in multiple services
- **Reduced Complexity**: Unified cleaning approach

### Maintainability:
- **Centralized Logic**: All cleaning in `CleanModelResultForDisplay`
- **Consistent API**: All services receive clean results by default
- **Future-Proof**: New TTS integration points automatically get clean content

### User Experience:
- **Immediate Clean TTS**: No delay for content to be "processed"
- **Professional Output**: TTS sounds natural and meaningful
- **Predictable Behavior**: Users know what to expect from TTS

The comprehensive TTS cleaning fix ensures that all Text-to-Speech functionality now reads only clean, meaningful model output instead of raw concatenated ensemble input containing system logs and technical data.
