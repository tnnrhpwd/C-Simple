# CSimple Application Paths Configuration

## Overview

CSimple now supports configurable application paths, allowing users to specify where application data and resources are stored. This feature provides flexibility for different storage requirements and enables easy data management.

## Default Structure

By default, CSimple creates the following directory structure in the user's Documents folder:

```
C:\Users\[Username]\Documents\CSimple\
├── Resources\
│   ├── WebcamImages\
│   ├── PCAudio\
│   ├── HFModels\
│   ├── Pipelines\
│   ├── MemoryFiles\
│   ├── dataItems.json
│   ├── recordedActions.json
│   ├── goals.json
│   ├── plans.json
│   ├── localDataItems.json
│   └── huggingFaceModels.json
```

## Features

### 1. Centralized Path Management
- All application paths are managed through the `IAppPathService`
- Paths are automatically created when needed
- Settings are persisted across application restarts

### 2. User Configuration
- Users can change the base application folder through the Settings page
- Options include:
  - Documents folder (default)
  - Desktop folder
  - Custom folder (user-specified path)
- Reset to default location option

### 3. Developer Access
- Services can access paths through dependency injection
- FileService automatically uses the configured paths
- All path operations are centralized and consistent

## Usage

### For Users

1. **Access Settings**: Navigate to Settings → Application Paths
2. **View Current Paths**: See the current application folder and structure
3. **Change Location**: 
   - Click "Change Application Folder"
   - Select from predefined options or specify a custom path
   - Confirm the change
4. **Reset**: Use "Reset to Default Location" to return to Documents/CSimple

### For Developers

#### Accessing Paths in Services

```csharp
public class MyService
{
    private readonly IAppPathService _appPathService;
    
    public MyService(IAppPathService appPathService)
    {
        _appPathService = appPathService;
    }
    
    public void SaveData()
    {
        var dataPath = _appPathService.GetResourcesPath();
        var imagePath = _appPathService.GetWebcamImagesPath();
        // Use paths as needed
    }
}
```

#### Using FileService

```csharp
public class MyService
{
    private readonly FileService _fileService;
    
    public MyService(FileService fileService)
    {
        _fileService = fileService;
    }
    
    public void AccessDirectories()
    {
        var resourcesDir = _fileService.GetResourcesDirectory();
        var webcamDir = _fileService.GetWebcamImagesDirectory();
        var audioDir = _fileService.GetPCAudioDirectory();
        var modelsDir = _fileService.GetHFModelsDirectory();
        // All paths automatically use the configured base location
    }
}
```

#### Setting Paths Programmatically

```csharp
// In SettingsService or other services
public async Task<bool> ChangeAppLocation(string newPath)
{
    return await _settingsService.SetApplicationBasePath(newPath);
}

public Dictionary<string, string> GetAllPaths()
{
    return _settingsService.GetAllApplicationPaths();
}
```

## Technical Implementation

### Services Involved

1. **IAppPathService/AppPathService**: Core path management
2. **SettingsService**: User interface for path configuration
3. **FileService**: Utilizes configured paths for file operations

### Key Methods

- `GetBasePath()`: Returns the current base application directory
- `GetResourcesPath()`: Returns the Resources subdirectory
- `GetWebcamImagesPath()`: Returns WebcamImages directory
- `GetPCAudioPath()`: Returns PCAudio directory
- `GetHFModelsPath()`: Returns HFModels directory
- `SetBasePath(string)`: Changes the base path and creates directories
- `InitializeDirectoriesAsync()`: Ensures all required directories exist

### Data Persistence

- Path preferences are stored using MAUI's `Preferences` API
- Settings persist across application sessions
- Automatic fallback to default path if stored path becomes inaccessible

## Migration

When upgrading to this version:

1. Existing data in `Documents\CSimple` will continue to work
2. No manual migration required
3. Users can move data by:
   - Copying existing CSimple folder to new location
   - Setting new path in application
   - Optionally removing old folder

## Error Handling

- Invalid paths show user-friendly error messages
- Automatic fallback to default location if configured path fails
- Directory creation happens automatically
- Access permission issues are caught and reported

## Benefits

1. **Flexibility**: Users can store data where convenient
2. **Organization**: Better data management for different workflows
3. **Backup**: Easier to backup/restore when data location is known
4. **Network Storage**: Support for network drives and alternative storage
5. **Multi-User**: Different users can have separate data locations

## Notes

- Application restart may be required for all services to use new paths
- Ensure selected directories have appropriate read/write permissions
- Network paths should be consistently accessible
- Large model files will be stored in the configured HFModels directory
