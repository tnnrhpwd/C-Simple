# C-Simple (Windows) 🚀

Give your Windows system the intelligence to help you. It will make decisions based on your previous interactions for the simple goal of accurately predicting your actions.

**[Early Access Download](https://drive.google.com/drive/folders/1lKQeLUHYwlrqO8P7LkMztHxSd_CpTvrx?usp=sharing)**

## Table of Contents

- [Introduction](#introduction)
- [Features](#features)
- [GitHub Copilot Development Guide](#github-copilot-development-guide)
- [Installation](#installation)
- [End-User Installation](#end-user-installation)
- [Usage](#usage)
- [Project Structure](#project-structure)
- [Data Model](#data-model)
- [Navigation](#navigation)
- [Publication](#publication)
- [Contributing](#contributing)
- [License](#license)

## Features

- **Multi-page Navigation:** Navigate between Home, Login, Inputs, Contact, and About pages.
- **Top Navigation Bar:** A centralized `Navbar.xaml` for easy navigation across the app.
- **Tabbed Navigation:** Utilize a `TabBar` within the `AppShell` to switch between key pages.
- **Responsive Layout:** The app is designed to work on multiple platforms, including iOS, Android, and Windows.
- **Pipeline Execution:** Advanced model execution with dependency resolution and parallel processing.
- **Service-Oriented Architecture:** Well-structured services for maintainable and testable code.

## GitHub Copilot Development Guide 🤖

This section provides recommended GitHub Copilot commands and best practices for working with this C# MAUI project.

### 🧪 Testing Commands

**Run All Tests:**

```bash
powershell -ExecutionPolicy Bypass -File .\run-tests.ps1
```

**Run Build Tests Only:**

```bash
cd "src\CSimple.Tests"; dotnet test --filter "FullyQualifiedName~SimpleBuildTests" --logger "console;verbosity=normal"
```

**Run Unit Tests Only:**

```bash
cd "src\CSimple.Tests"; dotnet test --filter "TestCategory=Unit" --logger "console;verbosity=normal"
```

**Build Test Project:**

```bash
cd "src"; dotnet build CSimple.Tests/CSimple.Tests.csproj
```

### 🔧 Refactoring Commands

**Extract Complex Logic to Services:**

```bash
please reduce the size of [FileName] by refactor outsourcing similar logic to a service file. only do one the best file compatible with outsourcing so you dont over extend yourself. then do a build checking for errors with cd "src\CSimple"; dotnet run --framework net8.0-windows10.0.19041.0
```

**Optimize Model Execution:**

```text
please try to optimize my run all models logic so it is faster. dont over extend yourself. then do a build checking for errors with cd "src\CSimple"; dotnet run --framework net8.0-windows10.0.19041.0
```

**Extract Methods:**

```text
please extract the complex logic in this method into smaller, more focused methods with clear responsibilities
```

**Move to Service:**

```text
please move this functionality to a dedicated service class and update all references to use dependency injection
```

### 🏗️ Code Generation Commands

**Create New Service:**

```text
please create a new service called [ServiceName] that handles [functionality] with proper dependency injection and error handling
```

**Generate ViewModels:**

```text
please create a new ViewModel for [PageName] following the MVVM pattern with proper property change notifications and commands
```

**Add New Page:**

```text
please create a new MAUI page called [PageName] with corresponding ViewModel, navigation setup, and proper XAML structure
```

### 🧪 Testing Commands

**Generate Unit Tests:**

```text
please create comprehensive unit tests for this service class including happy path, error scenarios, and edge cases
```

**Test Coverage Analysis:**

```text
please analyze this class and suggest what additional unit tests should be written to improve coverage
```

### 🔍 Code Analysis Commands

**Performance Review:**

```text
please review this code for performance issues and suggest optimizations, particularly for async operations and memory usage
```

**Architecture Review:**

```text
please review the architecture of this class and suggest improvements for better separation of concerns and maintainability
```

**Security Analysis:**

```text
please review this code for potential security vulnerabilities and suggest improvements
```

### 🛠️ Build & Deployment Commands

**Build and Run:**

```bash
cd "src"; dotnet run --project CSimple --framework net8.0-windows10.0.19041.0
```

**Clean Build:**

```bash
cd "src"; dotnet clean; dotnet restore; dotnet build --configuration Release
```

**Dev Cleanup & Deploy:**

```powershell
powershell -ExecutionPolicy Bypass -Command "$msg = Read-Host 'Enter commit message'; git add .; git commit -m '$msg'; git push origin; Write-Host 'Starting C-Simple Build Process...' -ForegroundColor Cyan; .\src\CSimple\Scripts\publish-and-upload.ps1; Write-Host '--- BUILD PROCESS COMPLETED ---' -ForegroundColor Green"
```

**Dev Cleanup & Deploy (with Copilot):**

```powershell
powershell -ExecutionPolicy Bypass -Command "gh copilot suggest 'git commit message for recent changes'; pause; $msg = Read-Host 'Enter commit message'; git add .; git commit -m '$msg'; git push origin; Write-Host 'Starting C-Simple Build Process...' -ForegroundColor Cyan; .\src\CSimple\Scripts\publish-and-upload.ps1; Write-Host '--- BUILD PROCESS COMPLETED ---' -ForegroundColor Green"
```

**Check Dependencies:**

```text
please analyze the project dependencies and suggest any outdated packages that should be updated
```

### 📖 Documentation Commands

**Generate Documentation:**

```text
please generate comprehensive XML documentation comments for this class including all public methods and properties
```

**Create README Sections:**

```text
please create a detailed README section explaining how to use this service/component with code examples
```

### 🐛 Debugging Commands

**Error Analysis:**

```text
please analyze this error message and suggest the most likely causes and solutions: [error message]
```

**Code Review:**

```text
please review this code for potential bugs, code smell, and adherence to C# best practices
```

**Async Issues:**

```text
please review this async code for potential deadlocks, race conditions, and proper exception handling
```

### 📊 Data & Models Commands

**Model Generation:**

```text
please create a model class for [entity] with proper validation attributes and documentation
```

**Data Service:**

```text
please create a data service for [entity] with CRUD operations, error handling, and async patterns
```

### 🎨 UI/UX Commands

**XAML Generation:**

```text
please create a modern, accessible XAML layout for [page description] following MAUI best practices
```

**Styling:**

```text
please create consistent styling for this XAML page that matches the app's design system
```

### 🚀 Advanced Patterns

**Dependency Injection Setup:**

```text
please help me set up proper dependency injection for this service in MauiProgram.cs with appropriate lifetime management
```

**MVVM Implementation:**

```text
please implement the MVVM pattern for this page with proper command binding and property change notifications
```

**Background Tasks:**

```text
please implement this functionality as a background task with proper cancellation support and progress reporting
```

### 💡 Best Practices Tips

1. **Always specify the framework target** when running dotnet commands
2. **Use semicolons (;)** instead of && for command chaining in PowerShell
3. **Request builds after refactoring** to catch compilation errors early
4. **Limit scope** of refactoring requests to avoid overwhelming changes
5. **Ask for specific patterns** like MVVM, dependency injection, or async/await
6. **Request error handling** and logging in service implementations
7. **Specify test types** when requesting test generation (unit, integration, etc.)

### 🎯 Project-Specific Commands

**Pipeline Execution:**

```text
please optimize the pipeline execution logic for better performance and add more detailed logging
```

**Model Management:**

```text
please implement model loading and caching strategies for better performance in the neural network components
```

**Action Review:**

```text
please enhance the action review functionality with better step navigation and content management
```

## Installation

1. **Clone the Repository:**

   ```bash
   git clone https://github.com/tnnrhpwd/C_Simple.git
   ```
2. **Open the Project:**

    Open the solution file (C_Simple.sln) in Visual Studio 2022.

3. **Restore NuGet Packages:**

    In Visual Studio, right-click on the solution in Solution Explorer and select "Restore NuGet Packages."

4. **Build the Project:**

    Press Ctrl+Shift+B or click on "Build > Build Solution" in the top menu.

5. **Run the App:**

    Choose your target platform (iOS, Android, Windows) and press F5 to run the app.

## End-User Installation

### Prerequisites

- Windows 10 version 1809 or later
- Administrator privileges on your computer

### Installation Steps

#### Step 1: Install the Certificate

Before installing the application, you need to install our security certificate:

1. Locate the `SimpleCert.cer` file in the same folder as the application
2. Double-click on the certificate file
3. Select "Open" if you get a security warning
4. In the Certificate window, click "Install Certificate"
5. Select "Local Machine" and click "Next" (this requires admin rights)
6. Select "Place all certificates in the following store"
7. Click "Browse" and select "Trusted Root Certification Authorities"
8. Click "Next" and then "Finish"
9. Confirm the security warning by clicking "Yes"
10. You should see a message that the import was successful

#### Step 2: Install the Application

After installing the certificate, you can install the application:

1. Locate the `Simple.msix` file
2. Double-click on the file
3. Click "Install"
4. Wait for the installation to complete
5. The application should now be available in your Start menu

### Troubleshooting

If you see an error about the certificate not being trusted:
- Make sure you've completed Step 1 correctly
- Ensure you selected "Local Machine" and "Trusted Root Certification Authorities"
- Try restarting your computer after installing the certificate

### Updates

When updates are available, you can install them by following only Step 2. You don't need to reinstall the certificate for updates.

## Usage

**Home Page:**

 The default landing page of the app with buttons for navigation.

**Login Page:**

A simple login form.

**Inputs Page:**

Demonstrates form input handling.

**Contact Page:**

A page to showcase contact details or form.

**About Page:**

Provides information about the app.

## Project Structure

```
plaintext
Copy code
C_Simple/
├── C_Simple.sln
├── App.xaml
├── AppShell.xaml
├── MainPage.xaml
├── Pages/
│   ├── HomePage.xaml
│   ├── LoginPage.xaml
│   ├── InputsPage.xaml
│   ├── ContactPage.xaml
│   └── AboutPage.xaml
├── Views/
│   ├── Navbar.xaml
│   └── OtherViewFiles.xaml
├── Tests/
│   ├── CSimple.Tests/
│   │   ├── BuildVerificationTests.cs
│   │   ├── SimpleBuildTests.cs
│   │   ├── DotNetBuildIntegrationTests.cs
│   │   └── BasicApplicationTests.cs
│   └── run-tests.ps1
└── Resources/
    ├── Fonts/
    ├── Images/
    └── Styles/
```

### Testing Infrastructure

The project includes a comprehensive test suite with:

- **Build Verification Tests**: Ensure the project builds correctly and meets MAUI requirements
- **Integration Tests**: Test dotnet CLI commands and build scenarios  
- **Unit Tests**: Basic functionality and logic tests
- **VS Code Integration**: Test Explorer support for running tests in the IDE

To run tests, use the provided PowerShell script: `.\run-tests.ps1` or use VS Code's Test Explorer.

## Data Model

**Action Group:**

```
[
  {
    "ActionName": "NavigateToPage",
    "ActionArray": [
      {
        "Timestamp": "2024-08-25T12:40:10.234Z",
        "KeyCode": 40,
        "EventType": 0,
        "Duration": 200,
        "Coordinates": null
      },
      {
        "Timestamp": "2024-08-25T12:40:15.789Z",
        "KeyCode": 13,
        "EventType": 0,
        "Duration": 100,
        "Coordinates": null
      }
    ]
  },...
]
```

**ActionGroup**
**ActionName (string)

Description: The name assigned to the group of actions. This identifier is used to categorize and manage a set of related actions.
Example: "NavigateToPage"

**ActionArray (List<ActionArrayItem>)**

Description: A list of actions that belong to the ActionGroup. Each action is defined by the ActionArrayItem class and contains information on what should be done, when, and how.
Example: An array containing multiple ActionArrayItem objects.

**ActionArrayItem**

**Timestamp (string)**

Description: The specific date and time when the action should occur, formatted as an ISO 8601 string. This timestamp helps in scheduling actions in sequence.
Example: "2024-08-25T12:40:10.234Z"

**KeyCode (ushort)**

Description: Represents the virtual key code associated with keyboard actions. This value determines which key is simulated during a key press action.
Example: 40 (which corresponds to the "Arrow Down" key in virtual key codes)

*Common Key Codes (Alphanumeric Keys)*
48 - 0, 49 - 1, 50 - 2, 51 - 3, 52 - 4, 53 - 5, 54 - 6, 55 - 7, 56 - 8, 57 - 9, 65 - A, 66 - B, 67 - C, 68 - D, 69 - E, 70 - F, 71 - G, 72 - H, 73 - I, 74 - J, 75 - K, 76 - L, 77 - M, 78 - N, 79 - O, 80 - P, 81 - Q, 82 - R, 83 - S, 84 - T, 85 - U, 86 - V, 87 - W, 88 - X, 89 - Y, 90 - Z, Function Keys, 112 - F1, 113 - F2, 114 - F3, 115 - F4, 116 - F5, 117 - F6, 118 - F7, 119 - F8, 120 - F9, 121 - F10, 122 - F11, 123 - F12, Control Keys, 16 - Shift, 17 - Ctrl, 18 - Alt, 27 - Esc, 32 - Space, 33 - Page Up, 34 - Page Down, 35 - End, 36 - Home, 37 - Left Arrow, 38 - Up Arrow, 39 - Right Arrow, 40 - Down Arrow, Mouse Actions, 1 - Left Mouse Button, 2 - Right Mouse Button, 4 - Middle Mouse Button, Other Special Keys, 13 - Enter, 9 - Tab, 8 - Backspace, 46 - Delete, 45 - Insert, 144 - Num Lock, 145 - Scroll Lock

**EventType (int)**

Description: An integer value that specifies the type of event. It distinguishes between different types of actions such as key presses, mouse clicks, or other events.
Example: 0 (which might represent a key down event; specific values depend on the application's event type definitions)

**Duration (int)**

Description: The length of time in milliseconds for which the action should be performed. For key presses or mouse actions, this indicates how long the action should last before being released or stopped.
Example: 200 (for a key press that should last 200 milliseconds)

**Coordinates (Coordinates)**

Description: Optional property used for mouse actions to specify the position of the mouse on the screen. This includes X and Y coordinates.
Example: null (indicating that coordinates are not applicable for this action, such as a keyboard event)
X (int): The horizontal position on the screen where the mouse action should occur.
Y (int): The vertical position on the screen where the mouse action should occur.

**Coordinates**
X (int)

Description: The X-coordinate for the mouse action, indicating the horizontal position on the screen where the action should be performed.
Y (int)

Description: The Y-coordinate for the mouse action, indicating the vertical position on the screen where the action should be performed.

## Navigation

**Top Navigation Bar:**

 The Navbar.xaml is included at the top of each page for easy navigation.

**Tab Bar Navigation:**

 The AppShell.xaml uses a TabBar to enable tabbed navigation between major sections of the app.

**Generating Project Structure**

To generate a file hierarchy for your project, run the following PowerShell command. This will create a visual hierarchy of your project files.

```bash
powershell -ExecutionPolicy Bypass -File ./generate_structure.ps1
```

## Publication

### For Developers

1. **Automated Publishing**

   Run the publication script to build, package, and deploy a new version:

   ```powershell
   powershell -ExecutionPolicy Bypass -Command "Write-Host 'Starting C-Simple Build Process...' -ForegroundColor Cyan; .\src\CSimple\Scripts\publish-and-upload.ps1; Write-Host '--- BUILD PROCESS COMPLETED ---' -ForegroundColor Green"
   ```

   This script will:
   - **Automatically increment the revision number** (tracked in `revision.txt`)
   - Build and publish the MAUI application
   - Create and sign an MSIX package with revision-based naming
   - Deploy to the configured destination folder with version tracking
   - Maintain an organized version history

2. **Revision Management**

   The system automatically manages revision numbers:
   - **Current Revision**: Stored in `revision.txt` in the project root
   - **Auto-increment**: Each publish automatically increments the revision
   - **Naming Convention**: Files are named as `Simple-v1.0.X.0-rY` where X is the revision and Y is the revision number
   - **Directory Structure**: Version directories include revision info: `v1.0.X.0-rY-YYYY.MM.DD`
   
   To check the current revision:

   ```powershell
   powershell -ExecutionPolicy Bypass -Command "Write-Host 'Current Build Information:' -ForegroundColor Yellow; Get-Content .\build-info.json | ConvertFrom-Json | Format-List"
   ```

   Or check revision only:

   ```powershell
   powershell -ExecutionPolicy Bypass -Command "Write-Host 'Current Revision:' -ForegroundColor Yellow; (Get-Content .\build-info.json | ConvertFrom-Json).revision"
   ```

3. **Version Management**

   The system maintains:
   - `/current` - Always contains the latest version
   - `/v1.0.X.0-rY-YYYY.MM.DD` - Version-specific folders with revision info
   - `/archive` - Older versions automatically archived
   - `releases.json` - Metadata including version and revision history

4. **Manual Publishing**

   If you prefer to publish manually:

   - Open the solution file (C_Simple.sln) in Visual Studio 2022
   - Right-click on the solution in Solution Explorer and select "Publish..."
   - Follow the publishing wizard steps

### For End Users

End users can always access the latest version at:
- [Latest CSimple Download](https://drive.google.com/drive/folders/1lKQeLUHYwlrqO8P7LkMztHxSd_CpTvrx?usp=sharing)

For installation instructions, see the [End-User Installation](#end-user-installation) section above.

## Contributing

Contributions are welcome! Please feel free to submit a pull request or open an issue for any feature requests or bug fixes.

Future state goals for this application: Implement user authentication to backup neural network guided application managers. The manager model function would be trained to read user data, predict environment interactions of many forms, and respond in ways that promote its programmed future state goal. This could include programming, video editing, and other forms of CAD in the styles of other intelligent systems.

## License

This project is licensed under the MIT License - see the LICENSE file for details.
