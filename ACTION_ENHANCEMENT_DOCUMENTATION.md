# Action Classified Nodes Enhancement

## Overview

This enhancement upgrades action classified nodes to generate executable action strings instead of descriptive text. When "Actions Enabled" is toggled on, these strings are automatically executed by the ActionPage simulation system.

## Architecture

### New Services

1. **ActionStringGenerationService**: Converts text commands to executable action strings with coordinate resolution
2. **WindowDetectionService**: Detects and locates application windows for coordinate resolution  
3. **ScreenAnalysisService**: Analyzes screen content to find UI elements
4. **ActionExecutionService**: Executes JSON action strings using the existing ActionService

### Integration Points

- **OrientPageViewModel**: Enhanced to process action model outputs and execute actions
- **NetPageViewModel**: Updated pipeline chat to use new action generation system
- **NodeViewModel**: Action classified nodes now store executable action strings

## Action String Format

Action strings are JSON objects that define executable commands:

```json
{
  "ActionType": "LeftClick",
  "X": 500,
  "Y": 300,
  "Duration": 100,
  "Timestamp": "2025-09-11T10:30:00"
}
```

### Supported Action Types

- **LeftClick**: Left mouse button click
- **RightClick**: Right mouse button click  
- **DoubleClick**: Double-click action
- **MouseMove**: Move mouse cursor
- **KeyPress**: Single key press
- **TypeText**: Type text string
- **Drag**: Drag from start to end coordinates
- **Scroll**: Mouse wheel scroll

## Text Command Examples

### Mouse Actions
- "click on the start button" → Finds Start button and clicks it
- "double click on Minecraft window" → Locates Minecraft window and double-clicks center
- "right click on desktop" → Right-clicks on desktop
- "move mouse to calculator" → Moves cursor to Calculator application

### Keyboard Actions
- "press enter key" → Presses Enter key
- "type hello world" → Types the text "hello world"
- "press ctrl+c" → Presses Ctrl+C combination

### Complex Actions
- "drag from start menu to recycle bin" → Drags between two UI elements
- "scroll down in browser window" → Scrolls down in browser

## Coordinate Resolution

The system automatically resolves coordinates for targets:

1. **Window Detection**: Finds application windows by name
2. **UI Element Analysis**: Locates buttons, menus, icons on screen
3. **Fallback Positioning**: Uses sensible defaults when targets aren't found

### Example Resolution Process

```
Text: "double click on Minecraft window"
↓
Parse: ActionType=DoubleClick, Target="Minecraft window"
↓
Resolve: WindowDetectionService finds Minecraft at (800, 400)
↓
Generate: {"ActionType": "DoubleClick", "X": 800, "Y": 400, "Duration": 100}
↓
Execute: ActionExecutionService creates ActionGroup and simulates
```

## Pipeline Flow

### Goal → Plan → Action Pipeline

1. **Goal Model**: "Launch Minecraft and start a new world"
2. **Plan Model**: "1. Double click Minecraft icon 2. Click 'Create New World' 3. Click 'Create World'"
3. **Action Model**: Receives plan context and generates executable commands

### Context Awareness

Action models receive context from connected Plan and Goal nodes to improve coordinate resolution:

```csharp
var planContext = await GetPlanContextAsync(actionNode, stepIndex);
var actionString = await _actionStringGenerationService.GenerateExecutableActionString(result, planContext);
```

## Usage Examples

### OrientPage Usage

1. Create text-to-text model nodes
2. Set Classification to "Action" 
3. Connect to Plan nodes for context
4. Toggle "Actions Enabled" on
5. Run pipeline - actions execute automatically

### NetPage Pipeline Chat

1. Include Action classified models in pipeline
2. Send goals through pipeline chat
3. Action models generate and execute commands automatically
4. View results in pipeline chat

## Configuration

### Action Generation Settings

```csharp
// Window detection timeout
WindowDetectionService.Timeout = TimeSpan.FromSeconds(5);

// Screen analysis precision
ScreenAnalysisService.AnalysisPrecision = AnalysisPrecision.High;

// Action execution delays
ActionExecutionService.DefaultDuration = 100; // milliseconds
```

### Error Handling

- **Coordinate Resolution Failures**: Falls back to screen center or shows error
- **Action Execution Failures**: Logged and displayed in pipeline chat
- **Parse Failures**: Original text preserved with error annotation

## Benefits

### Before Enhancement
- Action models output descriptive text: "double click something"
- Manual interpretation required
- No coordinate information
- No automated execution

### After Enhancement
- Action models output executable commands with resolved coordinates
- Automatic execution when Actions Enabled
- Context-aware coordinate resolution
- Seamless integration with existing ActionService

## Future Enhancements

1. **Computer Vision**: OCR and image recognition for better UI element detection
2. **Machine Learning**: Train models specifically for action command generation
3. **Multi-Monitor Support**: Handle multiple display configurations
4. **Action Chains**: Support for complex multi-step action sequences
5. **Validation**: Pre-execution validation of action feasibility

## Development Notes

### Testing
- Use debug output to trace action generation and execution
- Test with various applications and UI elements
- Verify coordinate resolution accuracy

### Performance
- Window detection is cached for performance
- Screen analysis uses efficient algorithms
- Action execution respects system timing

### Compatibility
- Works with existing ActionService infrastructure
- Maintains backward compatibility with recorded actions
- Integrates with current pipeline system
