# Step Content Consistency Fix

## Problem Identified

The user reported inconsistent step content display behavior when clicking "Generate (Single run)" on a model node:

### Before Fix Behavior:
1. **First Time Generation**: Shows concatenated, unclean content with ensemble input data mixed with model output
   ```
   [SafeImageSourceConverter] Image file not found: Blip Image Captioning Base: Screen Image: a group of deer standing in a field of tall grass Screen Image: a screenshot of a screenshot...
   ```
   
2. **After Clicking Off and Re-clicking**: Shows clean, processed content
   ```
   [SafeImageSourceConverter] Image file not found: H Message No Problem --------------------------------------------- 0% Complete File(s) Successful Folder...
   ```

## Root Cause Analysis

The inconsistency was caused by **two different code paths for retrieving model output**:

### Path 1: Direct Storage Access (Inconsistent)
- **Location**: `NodeViewModel.GetStepContent()` line 437-439
- **Issue**: Directly returned `stepData.Value` from `ActionSteps[step - 1]` without cleaning
- **Result**: Raw concatenated ensemble input mixed with model output

### Path 2: Cleaned Retrieval (Consistent) 
- **Location**: `NodeViewModel.GetStepOutput()` line 1491-1505
- **Feature**: Applied `CleanStoredModelOutput()` to remove concatenated ensemble input
- **Result**: Clean, processed model output

## Technical Analysis

### Storage Process (Working Correctly):
1. `ExecuteGenerateAsync()` stores raw result via `SetStepOutput()`
2. Raw result contains concatenated ensemble input data

### Retrieval Process (Inconsistent):
1. **GetStepContent()**: Returns raw stored data directly ❌
2. **GetStepOutput()**: Returns cleaned data via `CleanStoredModelOutput()` ✅

## Complete Solution

### Fixed Code in `NodeViewModel.GetStepContent()`

**Before (Inconsistent)**:
```csharp
// For Model nodes, if we have stored output (generated content), return it directly
if (Type == NodeType.Model && !string.IsNullOrEmpty(stepData.Value))
{
    return (stepData.Type, stepData.Value); // ❌ Raw data, no cleaning
}
```

**After (Consistent)**:
```csharp
// For Model nodes, if we have stored output (generated content), return it with cleaning applied
if (Type == NodeType.Model && !string.IsNullOrEmpty(stepData.Value))
{
    // Use GetStepOutput to ensure consistent cleaning logic is applied
    var cleanedOutput = GetStepOutput(step); // ✅ Always applies cleaning
    return cleanedOutput;
}
```

## Key Benefits of the Fix

### 🔧 **Consistent Behavior**
- **Before**: Different content on first generation vs. subsequent views
- **After**: Same clean content every time, regardless of interaction pattern

### 🛡️ **Unified Cleaning Logic**
- **Single Source**: All model output retrieval now uses the same cleaning pipeline
- **DRY Principle**: No duplication of cleaning logic across different methods

### 🔍 **Predictable UX**
- **Reliable Display**: Users see the same clean content consistently
- **No Confusion**: No more mysterious content changes after clicking off/on nodes

## Technical Details

### Cleaning Logic Applied:
- **Ensemble Input Removal**: Strips concatenated input data from model output
- **Content Formatting**: Ensures only actual model-generated content is displayed
- **Consistency**: Same cleaning rules applied regardless of retrieval path

### Methods Now Unified:
1. **GetStepContent()**: For UI display - now uses cleaned output
2. **GetStepOutput()**: For programmatic access - already had cleaning
3. **SetStepOutput()**: For storage - unchanged, stores raw data for later cleaning

## Files Modified

1. **`src/CSimple/ViewModels/NodeViewModel.cs`**
   - **Lines 434-442**: Updated `GetStepContent()` to use `GetStepOutput()`
   - **Behavior**: Model nodes now consistently return cleaned output for display

## Build Status
✅ **Build successful** with 78 warnings (no new errors introduced)

## Expected User Experience

### Before Fix:
- ❌ First generation: Long concatenated content with ensemble input
- ❌ After clicking off/on: Different, shorter clean content
- ❌ Confusing inconsistent behavior

### After Fix:
- ✅ First generation: Clean, processed model output
- ✅ After clicking off/on: Same clean content
- ✅ Consistent, predictable behavior

## Testing Verification

To verify the fix works:
1. Click on a model node
2. Click "Generate (Single run)"
3. Observe clean content immediately
4. Click off the node, then click back on it
5. Verify the same clean content is displayed

The content should now be identical in both scenarios, with concatenated ensemble input properly removed from the display.

## Implementation Notes

- **Backwards Compatibility**: No breaking changes to existing APIs
- **Performance**: Minimal impact - cleaning logic was already running for `GetStepOutput()`
- **Maintainability**: Reduced code duplication by reusing existing cleaning logic
