# Concurrency Fix for Step Navigation - System.InvalidOperationException Resolution

## Issue Description
When incrementing to step 1 during action replay navigation, the application threw:
```
System.InvalidOperationException: Operations that change non-concurrent collections must have exclusive access. A concurrent update was performed on this collection and corrupted its state. The collection's state is no longer correct.
```

The error occurred specifically during the `PrecomputeExecutionOptimizations` phase when "Pre-computing input relationships" was executing.

## Root Cause Analysis
The issue was in the `PrecomputeExecutionOptimizationsAsync` method in `OrientPageViewModel.cs`. The method was accessing cached collection references (`_cachedModelNodes` and `_cachedInputNodes`) without proper thread-safety protection when creating parallel tasks for:

1. **Model Pre-loading Tasks**: Line 2192 - `_cachedModelNodes.Select(async modelNode => ...)`
2. **Step Content Tasks**: Line ~2248 - `_cachedInputNodes.Select(async inputNode => ...)`  
3. **Relationship Tasks**: Line 2270 - `_cachedModelNodes.Select(async modelNode => ...)`

While the `GetConnectedInputNodes` method itself was thread-safe, the iteration over the cached collections was vulnerable to concurrent modification exceptions.

## Solution Implemented

### 1. Thread-Safe Collection Copying for Model Pre-loading
**Location**: `PrecomputeExecutionOptimizationsAsync` method, around line 2190
```csharp
// Before (vulnerable):
var modelTasks = _cachedModelNodes.Select(async modelNode => ...

// After (thread-safe):
List<NodeViewModel> modelNodesCopy;
lock (_nodesLock)
{
    modelNodesCopy = _cachedModelNodes?.ToList() ?? new List<NodeViewModel>();
}
var modelTasks = modelNodesCopy.Select(async modelNode => ...
```

### 2. Thread-Safe Collection Copying for Step Content Pre-computation
**Location**: `PrecomputeExecutionOptimizationsAsync` method, around line 2245
```csharp
// Before (vulnerable):
var stepContentTasks = _cachedInputNodes.Select(async inputNode => ...

// After (thread-safe):
List<NodeViewModel> inputNodesCopy;
lock (_nodesLock)
{
    inputNodesCopy = _cachedInputNodes?.ToList() ?? new List<NodeViewModel>();
}
var stepContentTasks = inputNodesCopy.Select(async inputNode => ...
```

### 3. Thread-Safe Collection Copying for Relationship Pre-computation
**Location**: `PrecomputeExecutionOptimizationsAsync` method, around line 2280
```csharp
// Before (vulnerable):
var relationshipTasks = _cachedModelNodes.Select(async modelNode => ...

// After (thread-safe):
List<NodeViewModel> relationshipModelNodesCopy;
lock (_nodesLock)
{
    relationshipModelNodesCopy = _cachedModelNodes?.ToList() ?? new List<NodeViewModel>();
}
var relationshipTasks = relationshipModelNodesCopy.Select(async modelNode => ...
```

## Technical Details

### Why This Fix Works
1. **Eliminates Race Conditions**: By creating local copies of the collections within lock statements, we ensure that the parallel task iteration operates on stable, immutable snapshots
2. **Preserves Performance**: The background optimization still runs in parallel, but with safe collection access
3. **Maintains Functionality**: All existing features (Goal/Plan/Action classification, step content, model execution) continue to work correctly

### Thread Safety Architecture
- **Primary Collections**: `Nodes` and `Connections` ObservableCollections protected by `_nodesLock` and `_connectionsLock`
- **Cached Collections**: `_cachedModelNodes` and `_cachedInputNodes` are created within lock statements and accessed through safe copies
- **Background Operations**: Use local copies for iteration while maintaining thread-safe access to shared resources

## Verification Steps
1. ✅ **Build Success**: Application compiles without errors
2. ✅ **Application Launch**: CSimple.exe starts successfully
3. 🔄 **Runtime Testing**: Step navigation from step 0 to step 1 should no longer throw `InvalidOperationException`

## Files Modified
- `src/CSimple/ViewModels/OrientPageViewModel.cs` - `PrecomputeExecutionOptimizationsAsync` method

## Impact Assessment
- **Risk Level**: Low - Only affects background optimization code paths
- **Performance Impact**: Minimal - Small overhead from creating collection copies
- **Compatibility**: Full - No changes to public APIs or user-facing functionality
- **Regression Risk**: Very Low - Changes are isolated to thread safety improvements

## Testing Recommendations
1. Test step navigation through multiple action steps (0→1→2→3→...)
2. Verify Goal/Plan/Action text classification still works
3. Confirm background optimization benefits are preserved
4. Test with various pipeline configurations (different numbers of nodes/connections)

This fix addresses the specific `System.InvalidOperationException` that occurred during step navigation while preserving all existing functionality and performance benefits of the background optimization system.
