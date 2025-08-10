# CSimple Path Configuration System

## Overview
The CSimple application now includes a centralized path configuration system that allows users to define, modify, and reference application resource directories from any page in the application.

## Key Features
- **Configurable Base Path**: Set the root location for all CSimple resources
- **Default Location**: `C:\Users\[username]\Documents\CSimple`
- **Settings UI**: Modify paths through the Settings page
- **Global Access**: Reference paths from any page in the application
- **Automatic Directory Creation**: Directories are created automatically when paths are set

## Configured Resource Directories
The system manages the following resource directories:
- **WebcamImages**: `[BasePath]\Resources\WebcamImages`
- **PCAudio**: `[BasePath]\Resources\PCAudio`
- **HFModels**: `[BasePath]\Resources\HFModels`

## Implementation Details

### Core Services
1. **AppPathService** (`Services\AppPathService.cs`)
   - Manages all application directory paths
   - Handles path validation and directory creation
   - Provides methods to get/set base path and retrieve specific resource directories

2. **FileService** (Updated)
   - Now uses AppPathService for all resource directory operations
   - Maintains backward compatibility while using configurable paths

3. **SettingsService** (Enhanced)
   - Added path management methods
   - Integrates with AppPathService for persistence

### User Interface
The Settings page now includes an "Application Paths" section where users can:
- View the current base path
- Change the base path location
- Reset paths to default location
- See all configured resource directories

### Usage Examples

#### From Code - Getting Resource Directories
```csharp
// Inject AppPathService in your constructor
public MyService(IAppPathService appPathService)
{
    _appPathService = appPathService;
}

// Get specific resource directories
string webcamDir = await _appPathService.GetWebcamImagesDirectoryAsync();
string audioDir = await _appPathService.GetPCAudioDirectoryAsync();
string modelsDir = await _appPathService.GetHFModelsDirectoryAsync();

// Get base path
string basePath = _appPathService.GetBasePath();
```

#### From Code - Setting Paths
```csharp
// Set new base path
await _appPathService.SetBasePathAsync(@"D:\MyCustomLocation\CSimple");

// This will automatically create:
// D:\MyCustomLocation\CSimple\Resources\WebcamImages
// D:\MyCustomLocation\CSimple\Resources\PCAudio
// D:\MyCustomLocation\CSimple\Resources\HFModels
```

## Installation Configuration
During installation, the system can be configured to use a custom base path by:
1. Setting the path programmatically via `AppPathService.SetBasePathAsync()`
2. Or by modifying the default in `AppPathService.cs` before compilation

## Settings Page Usage
1. Open the Settings page in the application
2. Navigate to the "Application Paths" section
3. Click "Change Base Path" to select a new location
4. Use "Reset to Default" to return to the default Documents location
5. All resource directories will be automatically updated and created

## Backward Compatibility
The system maintains full backward compatibility with existing code that used hardcoded paths. All FileService methods continue to work as before, but now use the configurable paths internally.
