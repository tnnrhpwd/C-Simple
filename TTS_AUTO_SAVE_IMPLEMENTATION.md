# Auto-Save TTS Settings Implementation

## Implementation Summary

I've successfully implemented automatic pipeline saving when the TTS "Read Aloud on Completion" setting is toggled for any node. This ensures that user preferences for TTS autoplay are immediately persisted to the pipeline.json file.

## What Was Added

### 1. PropertyChanged Subscription in SelectedNode
Modified the `SelectedNode` property setter in `OrientPageViewModel.cs` to:
- **Subscribe** to PropertyChanged events when a node is selected
- **Unsubscribe** from PropertyChanged events when switching to a different node
- **Handle cleanup** properly when the ViewModel is disposed

```csharp
private NodeViewModel _selectedNode;
public NodeViewModel SelectedNode
{
    get => _selectedNode;
    set
    {
        // Unsubscribe from the old node's PropertyChanged event
        if (_selectedNode != null)
        {
            _selectedNode.PropertyChanged -= OnSelectedNodePropertyChanged;
        }

        if (SetProperty(ref _selectedNode, value))
        {
            // Subscribe to the new node's PropertyChanged event
            if (_selectedNode != null)
            {
                _selectedNode.PropertyChanged += OnSelectedNodePropertyChanged;
            }
            // ... existing code ...
        }
    }
}
```

### 2. ReadAloudOnCompletion Change Handler
Added `OnSelectedNodePropertyChanged` method that:
- **Listens specifically** for `ReadAloudOnCompletion` property changes
- **Automatically saves** the pipeline when this setting changes
- **Provides debug logging** for troubleshooting

```csharp
private async void OnSelectedNodePropertyChanged(object sender, PropertyChangedEventArgs e)
{
    if (e.PropertyName == nameof(NodeViewModel.ReadAloudOnCompletion))
    {
        Debug.WriteLine($"ReadAloudOnCompletion changed for node '{SelectedNode?.Name}' to: {SelectedNode?.ReadAloudOnCompletion}");
        
        // Auto-save the pipeline when TTS setting changes
        if (!string.IsNullOrEmpty(CurrentPipelineName))
        {
            try
            {
                await _pipelineManagementService.SaveCurrentPipelineAsync(CurrentPipelineName, Nodes, Connections);
                Debug.WriteLine($"Pipeline '{CurrentPipelineName}' auto-saved after ReadAloudOnCompletion change");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error auto-saving pipeline after ReadAloudOnCompletion change: {ex.Message}");
            }
        }
        else
        {
            Debug.WriteLine("No current pipeline name set, ReadAloudOnCompletion change not saved");
        }
    }
}
```

### 3. Proper Cleanup in Dispose
Enhanced the `Dispose()` method to unsubscribe from PropertyChanged events:

```csharp
public void Dispose()
{
    // Unsubscribe from selected node PropertyChanged event
    if (_selectedNode != null)
    {
        _selectedNode.PropertyChanged -= OnSelectedNodePropertyChanged;
    }
    // ... existing disposal code ...
}
```

## How It Works

1. **User Action**: User clicks the TTS checkbox for a node in the UI
2. **Two-Way Binding**: The checkbox is bound to `SelectedNode.ReadAloudOnCompletion` with `Mode=TwoWay`
3. **Property Change**: When toggled, the `ReadAloudOnCompletion` property changes
4. **Event Trigger**: `OnSelectedNodePropertyChanged` is called with the property name
5. **Auto-Save**: Pipeline is automatically saved to pipeline.json with the new setting
6. **Persistence**: Setting is now saved and will be restored when the pipeline is loaded

## User Experience

✅ **Immediate Persistence**: TTS settings are saved instantly when toggled
✅ **No Manual Save Required**: Users don't need to remember to save the pipeline
✅ **Reliable Restoration**: Settings are properly restored when pipelines are loaded
✅ **Debug Visibility**: Console logs show when auto-save occurs

## Console Output Examples

When user toggles the TTS setting, you'll see:
```
ReadAloudOnCompletion changed for node 'Gpt2 [Goal]' to: True
Pipeline 'Lovely Pipeline' auto-saved after ReadAloudOnCompletion change
```

## Testing

- ✅ Project builds successfully with 0 errors
- ✅ PropertyChanged subscription/unsubscription implemented correctly
- ✅ Auto-save logic follows existing pattern (similar to classification changes)
- ✅ Proper cleanup in Dispose method

## Files Modified

- **`src/CSimple/ViewModels/OrientPageViewModel.cs`**
  - Modified `SelectedNode` property setter
  - Added `OnSelectedNodePropertyChanged` event handler
  - Enhanced `Dispose()` method

## Verification

To verify this is working:
1. Load a pipeline
2. Select a node
3. Toggle the "Read Aloud on Completion" checkbox
4. Check the console output for auto-save messages
5. Reload the pipeline and verify the setting is preserved

The TTS autoplay setting should now be automatically saved to pipeline.json whenever it's changed!
