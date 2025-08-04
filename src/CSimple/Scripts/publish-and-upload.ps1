# Variables
$APP_NAME = "CSimple"
$ScriptBaseDir = $PSScriptRoot # Get the directory where the script is located
$PROJECT_PATH = Join-Path $ScriptBaseDir "..\CSimple.csproj"  # Path relative to script location
$OUTPUT_DIR = (Resolve-Path (Join-Path $ScriptBaseDir "..\..\..\published")).Path  # Publish output folder in base directory
$TARGET_FRAMEWORK = "net8.0-windows10.0.19041.0"  # Target framework
$BUILD_CONFIG = "Release"  # Adjust as necessary
$MSI_OUTPUT_DIR = Join-Path $ScriptBaseDir "..\..\..\msi_output" # MSI output folder in base directory
$newCertPassword = "CSimpleNew"  # Password for the new .pfx file
$CertDir = Join-Path $ScriptBaseDir "..\..\..\certs" # Certificate directory in base directory
$env:PATH += ";C:\Program Files\dotnet"
$subject = "CN=Simple Inc, O=Simple Inc, C=US"
$mappingFilePath = Join-Path $ScriptBaseDir "..\..\..\mapping.txt" # Mapping file in base directory

# Build info file (consolidates version and revision tracking)
$buildInfoPath = Join-Path $ScriptBaseDir "..\..\..\build-info.json" # Build info file in base directory

# Root destination directory 
$rootDestDir = "D:\My Drive\Simple"
# Version-specific paths will be set after determining the version

# Maximum number of versions to keep in the main directory before archiving
$maxVersionsToKeep = 3

# Release metadata file
$releaseMetadataPath = Join-Path $rootDestDir "releases.json"

# Import Certificate Service
. (Join-Path $PSScriptRoot "CertificateService.ps1")

# Import Documentation Service
. (Join-Path $PSScriptRoot "DocumentationService.ps1")

# Function to check if tool exists
function Test-ToolExists {
    param (
        [string]$toolName,
        [string]$toolPath = ""
    )
    
    if ($toolPath -and (Test-Path $toolPath)) {
        return $true
    }
    
    return $null -ne (Get-Command $toolName -ErrorAction SilentlyContinue)
}

# Function to get and increment build info
function Get-IncrementedBuildInfo {
    # Load existing build info or create default
    if (Test-Path $buildInfoPath) {
        try {
            $buildInfo = Get-Content $buildInfoPath -Raw | ConvertFrom-Json
            $currentRevision = $buildInfo.revision
            $currentVersion = $buildInfo.version
        }
        catch {
            Write-Host "Invalid build info format. Resetting to defaults." -ForegroundColor Yellow
            $currentRevision = 0
            $currentVersion = "1.0.0.0"
        }
    }
    else {
        Write-Host "Build info file not found. Starting fresh." -ForegroundColor Yellow
        $currentRevision = 0
        $currentVersion = "1.0.0.0"
    }
    
    # Increment revision
    $newRevision = $currentRevision + 1
    
    # Generate new version (using revision as build number)
    $versionParts = $currentVersion -split '\.'
    $newVersion = "$($versionParts[0]).$($versionParts[1]).$newRevision.0"
    
    # Create updated build info
    $newBuildInfo = @{
        "version"    = $newVersion
        "revision"   = $newRevision
        "lastBuild"  = (Get-Date).ToString("o")
        "buildCount" = $newRevision
    }
    
    # Save updated build info
    $newBuildInfo | ConvertTo-Json -Depth 5 | Set-Content -Path $buildInfoPath -Encoding UTF8
    
    return $newBuildInfo
}

# Function to get release date in standardized format
function Get-FormattedReleaseDate {
    return Get-Date -Format "yyyy.MM.dd"
}

# Function to get all version directories
function Get-VersionDirectories {
    param (
        [string]$rootPath
    )
    
    if (Test-Path $rootPath) {
        return Get-ChildItem -Path $rootPath -Directory | Where-Object { $_.Name -match "^v\d+\.\d+\.\d+\.\d+.*$" } | Sort-Object CreationTime -Descending
    }
    return @()
}

# Function to clean up old versions
function Invoke-VersionArchiving {
    param (
        [string]$rootPath,
        [int]$keepCount
    )
    
    $versionDirs = Get-VersionDirectories -rootPath $rootPath
    
    # Skip cleanup if we don't have more than the keep count
    if ($versionDirs.Count -le $keepCount) {
        return
    }
    
    # Delete older versions (skip the first $keepCount which are the newest)
    $toDelete = $versionDirs | Select-Object -Skip $keepCount
    
    foreach ($dir in $toDelete) {
        Write-Host "Deleting older version: $($dir.Name)" -ForegroundColor Yellow
        
        try {
            # Remove the directory and all its contents
            Remove-Item -Path $dir.FullName -Recurse -Force
            Write-Host "Successfully deleted: $($dir.Name)" -ForegroundColor Green
        }
        catch {
            Write-Host "Failed to delete $($dir.Name): $_" -ForegroundColor Red
        }
    }
}

# Function to update release metadata
function Update-ReleaseMetadata {
    param (
        [string]$metadataPath,
        [string]$version,
        [int]$revision,
        [string]$releaseDate,
        [string]$releaseNotes = ""
    )
    
    # Initialize metadata object
    $metadata = @{
        "releases" = @()
    }
    
    # Load existing metadata if present
    if (Test-Path $metadataPath) {
        try {
            $jsonContent = Get-Content $metadataPath -Raw | ConvertFrom-Json
            # Convert PSCustomObject to hashtable manually for compatibility
            $metadata = @{
                "releases" = @()
            }
            if ($jsonContent.releases) {
                $metadata.releases = $jsonContent.releases
            }
        }
        catch {
            Write-Host "Error reading metadata file, creating new one: $_" -ForegroundColor Yellow
        }
    }
    
    # Add new release info
    $releaseInfo = @{
        "version"     = $version
        "revision"    = $revision
        "releaseDate" = $releaseDate
        "timestamp"   = (Get-Date).ToString("o")
        "notes"       = $releaseNotes
    }
    
    # Ensure releases array exists
    if (-not $metadata.releases) {
        $metadata.releases = @()
    }
    
    # Add new release at the beginning (most recent first)
    $metadata.releases = @($releaseInfo) + $metadata.releases
    
    # Save metadata
    $metadata | ConvertTo-Json -Depth 10 | Set-Content -Path $metadataPath -Encoding UTF8
}

# Check for dotnet
Write-Host "Checking for dotnet executable..."
if (-not (Test-ToolExists -toolName "dotnet")) {
    Write-Host "dotnet not found in the PATH."
    exit 1
}
Write-Host "dotnet found."

# Check for signtool and makeappx
$signtoolPath = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.19041.0\x64\signtool.exe"
$makeAppxPath = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.19041.0\x64\makeappx.exe"

if (-not (Test-ToolExists -toolName "signtool" -toolPath $signtoolPath)) {
    Write-Host "signtool not found at $signtoolPath" -ForegroundColor Red
    exit 1
}

if (-not (Test-ToolExists -toolName "makeappx" -toolPath $makeAppxPath)) {
    Write-Host "makeappx not found at $makeAppxPath" -ForegroundColor Red
    exit 1
}

# Create output directories
if (-not (Test-Path $OUTPUT_DIR)) { New-Item -ItemType Directory -Path $OUTPUT_DIR }
if (-not (Test-Path $MSI_OUTPUT_DIR)) { New-Item -ItemType Directory -Path $MSI_OUTPUT_DIR }

# Create root destination directory if it doesn't exist
if (-not (Test-Path $rootDestDir)) {
    New-Item -ItemType Directory -Path $rootDestDir -Force | Out-Null
}

# Get build info and increment it
$buildInfo = Get-IncrementedBuildInfo
$currentRevision = $buildInfo.revision
$appVersion = $buildInfo.version

Write-Host "Building revision #$currentRevision (v$appVersion)" -ForegroundColor Green

# Get release date for folder naming
$releaseDate = Get-FormattedReleaseDate

# Setup version-specific paths and names
$versionDirName = "v$appVersion-$releaseDate"
$versionDir = Join-Path $rootDestDir $versionDirName

$msixFileName = "$APP_NAME-v$appVersion.msix"
$certFileName = "SimpleCert.cer"  # Use consistent certificate name
$MSIX_MANIFEST_PATH = Join-Path $ScriptBaseDir "..\..\..\AppxManifest.xml"
$MSIX_OUTPUT_PATH = Join-Path $ScriptBaseDir "..\..\..\$msixFileName"

# Create version directory
if (Test-Path $versionDir) {
    # Clear existing files if directory exists
    Remove-Item -Path "$versionDir\*" -Force -Recurse
}
else {
    New-Item -ItemType Directory -Path $versionDir -Force | Out-Null
}

# Build .NET MAUI project
Write-Host "Building .NET MAUI app..."
# Check if project file exists before building
if (-not (Test-Path $PROJECT_PATH)) {
    Write-Host "Project file not found at $PROJECT_PATH" -ForegroundColor Red
    exit 1
}
dotnet build $PROJECT_PATH --framework $TARGET_FRAMEWORK -c $BUILD_CONFIG
if ($LASTEXITCODE -ne 0) { Write-Host "Build failed." ; exit 1 }

# Publish .NET MAUI App
Write-Host "Publishing app..."
# Add /p:AppxPackageSigningEnabled=false to disable signing during publish
dotnet publish $PROJECT_PATH -f $TARGET_FRAMEWORK -c $BUILD_CONFIG -o $OUTPUT_DIR --self-contained /p:AppxPackageSigningEnabled=false
if ($LASTEXITCODE -ne 0) { Write-Host "Publish failed." ; exit 1 }

# Initialize and handle certificate management
$certResult = Initialize-AppCertificate -certDir $CertDir -subject $subject -password $newCertPassword

if ($certResult.IsNew) {
    # Sign the published output using the new certificate
    Invoke-FilesSigning -outputDir $OUTPUT_DIR -pfxPath $certResult.PfxPath -password $newCertPassword -signtoolPath $signtoolPath
}

Write-Host "Creating mapping file..."

# Always create a new AppxManifest.xml with the correct format
Write-Host "Creating a new AppxManifest.xml file..."
$defaultManifest = @"
<?xml version="1.0" encoding="utf-8"?>
<Package 
  xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10" 
  xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10" 
  xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities" 
  IgnorableNamespaces="uap rescap">
  
  <Identity 
    Name="CSimple-App" 
    Publisher="CN=Simple Inc, O=Simple Inc, C=US" 
    Version="$appVersion" />
  
  <Properties>
    <DisplayName>CSimple</DisplayName>
    <PublisherDisplayName>Simple Inc</PublisherDisplayName>
    <Description>CSimple - Streamlined productivity and automation tools</Description>
    <Logo>appiconStoreLogo.scale-100.png</Logo>
  </Properties>
  
  <Dependencies>
    <TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.17763.0" MaxVersionTested="10.0.19041.0" />
  </Dependencies>
  
  <!-- Removed the Resources section that was causing issues -->
  
  <Capabilities>
    <rescap:Capability Name="runFullTrust"/>
  </Capabilities>
  
  <Applications>
    <Application Id="CSimple" Executable="CSimple.exe" EntryPoint="Windows.FullTrustApplication">
      <uap:VisualElements 
        DisplayName="CSimple" 
        Description="Streamlined productivity and automation tools" 
        BackgroundColor="#081B25" 
        Square150x150Logo="appiconMediumTile.scale-100.png" 
        Square44x44Logo="appiconLogo.scale-100.png">
        
        <!-- Enhanced Logo Support for Microsoft Store -->
        <uap:DefaultTile 
          Wide310x150Logo="appiconWideTile.scale-100.png"
          Square310x310Logo="appiconLargeTile.scale-100.png"
          Square71x71Logo="appiconSmallTile.scale-100.png"
          ShortName="CSimple">
          <uap:ShowNameOnTiles>
            <uap:ShowOn Tile="square150x150Logo"/>
            <uap:ShowOn Tile="wide310x150Logo"/>
            <uap:ShowOn Tile="square310x310Logo"/>
          </uap:ShowNameOnTiles>
        </uap:DefaultTile>
        
        <!-- Splash Screen using the generated splash screen assets -->
        <uap:SplashScreen Image="appiconfgSplashScreen.scale-100.png" BackgroundColor="#081B25"/>
      </uap:VisualElements>
    </Application>
  </Applications>
</Package>
"@

Set-Content -Path $MSIX_MANIFEST_PATH -Value $defaultManifest -Encoding UTF8

Write-Host "Using MAUI-generated logo assets for Microsoft Store compliance..." -ForegroundColor Green

# Create the mapping file with only the files from OUTPUT_DIR
# Don't include AppxManifest.xml in mapping when using /m parameter
$mappingContent = "[Files]`r`n"

# Ensure $OUTPUT_DIR is set
if (-not $OUTPUT_DIR) {
    Write-Host "Error: OUTPUT_DIR is not defined." -ForegroundColor Red
    exit 1
}

# Process each file and directory in $OUTPUT_DIR, excluding resources.pri
Get-ChildItem -Path $OUTPUT_DIR -Recurse -File | Where-Object { $_.Name -ne "resources.pri" } | ForEach-Object {
    # Ensure the file path starts with OUTPUT_DIR (case-insensitive)
    if ($_.FullName.StartsWith($OUTPUT_DIR, [System.StringComparison]::OrdinalIgnoreCase)) {
        $relativePath = $_.FullName.Substring($OUTPUT_DIR.Length + 1).Replace("\", "/")
        $mappingContent += "`"$($_.FullName -replace '\\', '/')`" `"$relativePath`"`r`n"
    }
    else {
        Write-Host "Warning: File path doesn't start with OUTPUT_DIR: $($_.FullName)" -ForegroundColor Yellow
        Write-Host "  OUTPUT_DIR: $OUTPUT_DIR" -ForegroundColor Yellow
    }
}

# Output the mapping file content to a file
Set-Content -Path $mappingFilePath -Value $mappingContent -Encoding UTF8

# Debug: Display the first few lines of the mapping file
Write-Host "Debug: First few lines of mapping file:"
Get-Content $mappingFilePath -TotalCount 5 | ForEach-Object { Write-Host $_ }

Write-Host "Mapping file created successfully at $mappingFilePath" -ForegroundColor Green

# Create MSIX package
Write-Host "Creating MSIX package..."
& $makeAppxPath pack /m $MSIX_MANIFEST_PATH /f $mappingFilePath /p $MSIX_OUTPUT_PATH
if ($LASTEXITCODE -ne 0) {
    Write-Host "MSIX package creation failed." -ForegroundColor Red
    exit 1
}

# Sign the MSIX package
Invoke-MsixSigning -msixPath $MSIX_OUTPUT_PATH -pfxPath $certResult.PfxPath -password $newCertPassword -signtoolPath $signtoolPath

# Copy files to the version-specific directory
Write-Host "Copying files to version directory: $versionDir"
try {
    # Copy MSIX and certificate to version directory
    Copy-Item -Path $MSIX_OUTPUT_PATH -Destination (Join-Path $versionDir $msixFileName) -Force
    Copy-Item -Path $certResult.CerPath -Destination (Join-Path $versionDir $certFileName) -Force
    
    # Create all installation documentation using the documentation service
    New-InstallationDocumentation -versionDir $versionDir -appVersion $appVersion -releaseDate $releaseDate -certFileName $certFileName -msixFileName $msixFileName -buildInfoPath $buildInfoPath
    
    # Update release metadata
    Update-ReleaseMetadata -metadataPath $releaseMetadataPath -version $appVersion -revision $currentRevision -releaseDate $releaseDate
    
    # Archive older versions
    Invoke-VersionArchiving -rootPath $rootDestDir -keepCount $maxVersionsToKeep

    Write-Host "Files successfully copied to version directory: $versionDir" -ForegroundColor Green
} 
catch {
    Write-Host "Failed to copy files to version directory. Error: $_" -ForegroundColor Red
}

# Clean up temporary files
Write-Host "Cleaning up temporary files..."
if (Test-Path $mappingFilePath) { Remove-Item $mappingFilePath -Force }
if (Test-Path $MSIX_MANIFEST_PATH) { Remove-Item $MSIX_MANIFEST_PATH -Force }
if (Test-Path $MSIX_OUTPUT_PATH) { Remove-Item $MSIX_OUTPUT_PATH -Force }

Write-Host "Process complete! Files have been published to:" -ForegroundColor Green
Write-Host "- Version directory: $versionDir" -ForegroundColor Green
Write-Host ""
Write-Host "MSIX package: $(Join-Path $versionDir $msixFileName)" -ForegroundColor Green
Write-Host "Certificate: $(Join-Path $versionDir $certFileName)" -ForegroundColor Green