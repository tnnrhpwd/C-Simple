# GitHub Copilot Instructions for C-Simple

C-Simple is a C# MAUI application that builds an AI-powered Windows system assistant using neural network pipelines and visual node-based model orchestration.

## Architecture Overview

C-Simple uses a **service-oriented architecture** with strict **dependency injection** patterns managed through `ServiceCollectionExtensions.cs`. The core concept is a **visual pipeline editor** where users connect input nodes (screen, audio, text) to AI models to build intelligent workflows.

### Key Components

- **Pipeline System**: Visual node-based editor for connecting data inputs → AI models → outputs
- **Node Types**: `Input`, `Model`, `Output`, `File`, `Processor`, `Junction`, `Splitter`, `Merger` (see `Models/NodeType.cs`)
- **Ensemble Execution**: Multiple models can process the same input with different combination strategies
- **Action Recording**: Captures user interactions for training automation models

## Critical Development Patterns

### 1. Service Registration (MauiProgram.cs)

All services use DI with specific lifetime patterns:

```csharp
// Singleton for shared state
services.AddSingleton<OrientPageViewModel>();
// Factory pattern for circular dependency resolution
services.AddSingleton<EnsembleModelService>(sp => new EnsembleModelService(sp));
```

### 2. ViewModels Follow Strict MVVM

- All ViewModels implement `INotifyPropertyChanged`
- Use `[CallerMemberName]` for `OnPropertyChanged()`
- Commands use `Command<T>` with CanExecute predicates
- Example: `OrientPageViewModel` has 15+ injected services via constructor

### 3. Node Management Architecture

**NodeViewModel** is the core pipeline element:

- `Type` determines behavior (`NodeType.Input`, `NodeType.Model`, etc.)
- `DataType` specifies content ("text", "image", "audio", "unknown")
- `ActionSteps` stores execution results
- `ExecutionState` tracks pipeline execution status

### 4. Pipeline Execution Flow

1. **Dependency Resolution**: `PipelineExecutionService` builds execution order
2. **Input Preparation**: `EnsembleModelService.PrepareModelInput()` combines connected inputs
3. **Model Execution**: `NetPageViewModel.ExecuteModelAsync()` handles HuggingFace models
4. **Result Combination**: Uses ensemble methods (concatenation, averaging, voting)

### 5. Async Patterns & Performance

- Always use `async Task` for UI operations
- Pre-load models: `PrewarmExecutionEnvironment()`
- Cache expensive operations: `_stepContentCache`, `_nodeCache`
- Background processing: `Task.Run()` for non-UI work

## Essential Commands

### Build & Test

```powershell
# Main build (must specify Windows target)
cd "src"; dotnet run --project CSimple --framework net8.0-windows10.0.19041.0

# Run tests
powershell -ExecutionPolicy Bypass -File .\run-tests.ps1

# Build only
cd "src"; dotnet build --configuration Release
```

### Development Workflow

```powershell
# Complete dev cycle (commit, build, deploy)
powershell -ExecutionPolicy Bypass -Command "
$msg = Read-Host 'Enter commit message';
git add .; git commit -m '$msg'; git push origin;
.\src\CSimple\Scripts\publish-and-upload.ps1"
```

## Project-Specific Conventions

### 1. File Organization

- **ViewModels**: Heavy dependency injection, async initialization
- **Services**: Interface-based with concrete implementations
- **Models**: Simple POCOs with enums (avoid complex logic)
- **Resources**: JSON pipeline definitions, HuggingFace model cache

### 2. Error Handling

- Use `Debug.WriteLine()` with timestamps: `[{DateTime.Now:HH:mm:ss.fff}]`
- Emoji prefixes for log categories: 🤖 (models), 📂 (files), ⚡ (execution)
- Try-catch in async methods with user-friendly status messages

### 3. Data Flow Patterns

- **Input Nodes**: Pull data from system (screen capture, audio, keyboard)
- **Model Nodes**: Execute HuggingFace transformers via Python
- **File Nodes**: Persistent storage for memory/goals
- **Connections**: Determine data flow between nodes

### 4. HuggingFace Integration

Models stored in `Resources/HFModels/` with download markers. Core models:

- **GUI-Owl-7B**: Vision-language model for screen understanding
- **Whisper-Base**: Audio transcription
- **BLIP**: Image captioning
- **GPT-2**: Text generation

### 5. Memory Management

- Use `IDisposable` for ViewModels with subscriptions
- Clear caches: `ClearStepContentCache()` on pipeline changes
- Background cleanup in `OnNavigatedFrom()`

## Common Refactoring Requests

When extracting logic to services:

1. Create interface in `Services/Interfaces/`
2. Add concrete implementation with DI constructor
3. Register in `ServiceCollectionExtensions.cs`
4. Inject into consuming ViewModels
5. Update existing usages to use service methods

When optimizing pipeline execution:

- Focus on `EnsembleModelService.ExecuteModelNode()`
- Cache input preparation results
- Batch similar model executions
- Pre-load neural networks during startup

## Key Files to Reference

- `OrientPageViewModel.cs`: Main pipeline orchestration logic
- `EnsembleModelService.cs`: Model execution and result combination
- `ServiceCollectionExtensions.cs`: Dependency injection setup
- `GUI Agent Pipeline.pipeline.json`: Example pipeline structure
- `NodeType.cs`: Core type system for pipeline elements

Always validate builds after changes and maintain the established DI patterns. The architecture prioritizes modularity and testability over simplicity.
