# Online/Offline Mode Persistence

This document describes the implementation of persistent online/offline mode settings in the CSimple application.

## Overview

The online/offline mode settings on the HomePage are now persistent across application restarts. This means that when a user toggles between online and offline mode, their preference will be remembered the next time they launch the application.

## Implementation

### AppModeService Enhancements

The `AppModeService` class in `Services/AppModeService/AppModeService.cs` has been enhanced with persistence functionality:

1. **SecureStorage Integration**: Uses MAUI's `SecureStorage` to safely persist the app mode setting
2. **Automatic Loading**: Loads the saved mode when the service is initialized
3. **Automatic Saving**: Saves the mode immediately when it changes

#### Key Methods

- `SaveModeAsync(AppMode mode)`: Saves the current mode to secure storage
- `LoadSavedModeAsync()`: Loads the previously saved mode from secure storage
- Constructor automatically calls `LoadSavedModeAsync()` on initialization

### HomePage Integration

The `HomePage` class has been updated to properly display the persisted settings:

1. **OnAppearing Override**: Ensures the UI reflects the current persisted state
2. **Property Notifications**: Updates `IsOnlineMode` and `AppModeLabel` properties when the page appears
3. **Debug Logging**: Logs the loaded app mode for troubleshooting

## Usage

### For Users

1. Open the CSimple application
2. Navigate to the HomePage
3. Toggle the "Online Mode" switch in the AI Assistant status bar
4. Close and restart the application
5. The toggle will maintain its previous state

### For Developers

The persistence is handled automatically by the `AppModeService`. No additional code is required in other parts of the application that use this service.

```csharp
// The service automatically loads and saves settings
var appModeService = new AppModeService(); // Loads saved state
appModeService.CurrentMode = AppMode.Online; // Automatically saves
```

## Technical Details

### Storage Key

- **Key**: "AppMode"
- **Values**: "Online" or "Offline"
- **Storage**: Uses MAUI SecureStorage for cross-platform compatibility

### Error Handling

- Graceful fallback to default (Offline) mode if loading fails
- Debug logging for troubleshooting storage issues
- Non-blocking operations to avoid UI freezing

### Testing

The implementation includes automated tests in `AppModeServiceTests.cs` that verify:

- Persistence functionality is correctly implemented
- HomePage properly loads persisted settings
- Constructor initializes with saved state

## Benefits

1. **Better User Experience**: Users don't need to reconfigure their preferred mode every time
2. **Consistent Behavior**: The app maintains user preferences across sessions
3. **Cross-Platform**: Works on all MAUI-supported platforms
4. **Secure**: Uses platform-specific secure storage mechanisms

## Troubleshooting

If the online/offline setting isn't persisting:

1. Check debug output for AppModeService messages
2. Verify SecureStorage permissions on the target platform
3. Ensure the app has proper file system access
4. Check for exceptions in the error logs

The system will automatically fall back to Offline mode if any issues occur with loading the saved settings.
