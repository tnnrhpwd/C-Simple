# Windows Text-to-Speech (TTS) Integration

## Overview

A simple Windows Text-to-Speech feature has been added to make it easier to read action model node step content aloud without requiring complex neural network TTS models. This uses the built-in Windows Speech Synthesis APIs for immediate text-to-speech functionality.

## Features

### Automatic TTS for Action Model Nodes
- **Action Model Detection**: When a model node with classification "Action" produces text output, it will automatically be read aloud using TTS
- **Background Processing**: TTS runs in the background and won't block the UI
- **Smart Announcement**: Action content is prefixed with "Performing action:" when spoken

### Manual TTS Control
- **Play Button**: The existing "Play Audio" button now supports both audio files and text content via TTS
- **Stop Button**: The "Stop Audio" button stops both audio playback and TTS speech
- **Auto-Detection**: The system automatically determines whether to play an audio file or read text aloud based on content type

### Enhanced Audio Step Content Service
The `AudioStepContentService` has been extended to support:
- **Text Content**: Reads text aloud using Windows TTS
- **Audio Files**: Plays audio files using the existing audio playback system
- **Unified Interface**: Single service handles both audio and TTS functionality

## How It Works

### 1. Automatic Action Reading
When running action models (either single generation or batch processing):
1. The system detects when an Action-classified model produces text output
2. Automatically triggers TTS to read the action aloud
3. Prefixes the speech with "Performing action:" for clarity

### 2. Manual Text Reading
For any text content displayed in the step content area:
1. Select the model node containing text content
2. Click the "Play Audio" button
3. The system will read the text aloud using TTS

### 3. Content Type Detection
The system automatically determines what to do based on content type:
- **"text" content**: Uses Windows TTS to read aloud
- **"audio" content**: Plays the audio file using NAudio
- **Other types**: Not supported for playback

## Technical Implementation

### Windows TTS Service (`WindowsTtsService`)
- Uses Windows Runtime Speech Synthesis APIs (`Windows.Media.SpeechSynthesis`)
- Compatible with .NET MAUI Windows applications
- Supports voice selection and volume control
- Proper disposal and resource management

### Enhanced Audio Service
The `AudioStepContentService` now:
- Initializes both audio playback and TTS services
- Routes text content to TTS and audio content to NAudio
- Provides unified event handling for both playback types
- Maintains backward compatibility with existing audio functionality

### Integration Points
- **ExecuteModelForStepAsync**: Automatically triggers TTS for Action model outputs
- **ExecuteGenerateAsync**: Triggers TTS for single Action model generation
- **PlayAudio Command**: Enhanced to support both audio and text playback
- **CanPlayAudio**: Updated to detect both audio files and text content

## Platform Support

### Windows
- Full TTS support using Windows Runtime APIs
- Voice selection from installed Windows voices
- Volume control via MediaPlayer

### Other Platforms
- TTS service gracefully degrades on non-Windows platforms
- Audio file playback remains fully functional
- No errors or crashes on unsupported platforms

## Usage Examples

### Action Model Node
1. Create a model node and set its classification to "Action"
2. Connect input nodes with action descriptions
3. Run the model - output will be automatically read aloud
4. Example spoken output: "Performing action: Click the submit button"

### Manual Text Reading
1. Select any model node with text output
2. Ensure the text is displayed in the step content area
3. Click the "Play Audio" button
4. The text will be read aloud using the default Windows voice

### Stopping Speech
1. Click the "Stop Audio" button to stop any active TTS or audio playback
2. Speech stops immediately and the UI updates accordingly

## Benefits

### Immediate Availability
- No need to download or configure neural network TTS models
- Uses Windows built-in voices available on all Windows systems
- Instant setup with no additional dependencies

### Accessibility
- Improves accessibility for users with visual impairments
- Provides audio feedback for action execution
- Enables hands-free operation feedback

### Workflow Enhancement
- Audio confirmation of actions being performed
- Reduces need to constantly monitor screen during automated actions
- Enables multitasking while models are executing

## Future Enhancements

Potential improvements that could be added:
- Voice selection UI for choosing different Windows voices
- Speech rate/speed controls
- SSML support for more natural speech patterns
- Text preprocessing for better pronunciation of technical terms
- Integration with neural TTS models for higher quality voices

## Troubleshooting

### No Speech Output
- Ensure Windows speech synthesis is enabled in system settings
- Check system volume and TTS volume settings
- Verify the content type is "text" for TTS functionality

### TTS Not Available Error
- Occurs on non-Windows platforms - this is expected behavior
- Audio file playback will still work normally
- No action needed - the feature gracefully degrades

### Performance
- TTS processing happens in background threads
- Should not impact UI responsiveness
- Large text blocks may take a moment to start speaking
