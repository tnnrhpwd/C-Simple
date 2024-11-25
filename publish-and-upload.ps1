# Variables
$APP_NAME = "Simple"
$PROJECT_PATH = "C:\Users\Aries\Documents\GitHub\C-Simple\src\CSimple\CSimple.csproj"  # Path to your .NET MAUI project file
$OUTPUT_DIR = "C:\Users\Aries\Documents\GitHub\C-Simple\published"  # Publish output folder
$TARGET_FRAMEWORK = "net8.0-windows10.0.19041.0"  # Target framework
$BUILD_CONFIG = "Release"  # Adjust as necessary
$MSI_OUTPUT_DIR = "D:\My Drive\Simple"  # MSI output folder
$newCertPath = "C:\Users\Aries\Documents\CSimple\Certificates\CSimple_NewCert.pfx"  # Path to save the new certificate
$newCertPassword = "CSimpleNew"  # Password for the new .pfx file
$MSIX_OUTPUT_PATH = "C:\Users\Aries\Documents\GitHub\C-Simple\Simple.msix"  # Path to save the MSIX package
$MSIX_MANIFEST_PATH = "C:\Users\Aries\Documents\GitHub\C-Simple\AppxManifest.xml"  # Path to the AppxManifest.xml file
$cerPath = "C:\Users\Aries\Documents\CSimple\Certificates\SimpleCert.cer"
$pfxPath = "C:\Users\Aries\Documents\CSimple\Certificates\CSimple_NewCert.pfx"
$env:PATH += ";C:\Program Files\dotnet"

# Function to check if a certificate with the same CN and O exists
function Test-CertificateExists {
  param (
      [string]$certPath,
      [string]$subject
  )
  if (Test-Path $certPath) {
      $cert = Get-PfxCertificate -FilePath $certPath
      return $cert.Subject -eq $subject
  }
  return $false
}

# Check for dotnet
Write-Host "Checking for dotnet executable..."
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "dotnet not found in the PATH."
    exit 1
}
Write-Host "dotnet found."

# Create output directories
if (-not (Test-Path $OUTPUT_DIR)) { New-Item -ItemType Directory -Path $OUTPUT_DIR }
if (-not (Test-Path $MSI_OUTPUT_DIR)) { New-Item -ItemType Directory -Path $MSI_OUTPUT_DIR }
if (-not (Test-Path "C:\Users\Aries\Documents\CSimple")) { New-Item -ItemType Directory -Path "C:\Users\Aries\Documents\CSimple" }

# Build .NET MAUI project
Write-Host "Building .NET MAUI app..."
dotnet build $PROJECT_PATH --framework $TARGET_FRAMEWORK -c $BUILD_CONFIG
if ($LASTEXITCODE -ne 0) { Write-Host "Build failed." ; exit 1 }

# Publish .NET MAUI App
Write-Host "Publishing app..."
dotnet publish $PROJECT_PATH -f $TARGET_FRAMEWORK -c $BUILD_CONFIG -o $OUTPUT_DIR --self-contained
if ($LASTEXITCODE -ne 0) { Write-Host "Publish failed." ; exit 1 }

# Create a new self-signed certificate with a unique name
Write-Host "Creating new self-signed certificate..."
$newCert = New-SelfSignedCertificate -Type Custom -Subject "CN=CSimple, O=Simple Org, C=US" `
    -KeyUsage DigitalSignature -FriendlyName "CSimpleInstallerNewCert" `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}") `
    -NotAfter (Get-Date).AddYears(5)

# Verify certificate creation
if (-not $newCert) {
    Write-Host "New certificate creation failed."
    exit 1
}
Export-Certificate -Cert $newCert -FilePath "C:\Users\Aries\Documents\CSimple\Certificates\SimpleCert.cer"
# Ensure the directory for the new certificate path exists
$newCertDir = [System.IO.Path]::GetDirectoryName($newCertPath)
if (-not (Test-Path $newCertDir)) { New-Item -ItemType Directory -Path $newCertDir }

# Export new certificate to .pfx file
Write-Host "Exporting new certificate to $newCertPath..."
Export-PfxCertificate -Cert $newCert -FilePath $newCertPath -Password (ConvertTo-SecureString -String $newCertPassword -Force -AsPlainText)

# Verify certificate export
if (-not (Test-Path $newCertPath)) {
    Write-Host "New certificate export failed."
    exit 1
}

# Sign the published output using the new .pfx file
Write-Host "Signing the published output with the new certificate..."
$filesToSign = Get-ChildItem -Path $OUTPUT_DIR -Recurse | Where-Object { $_.Extension -eq ".exe" -or $_.Extension -eq ".dll" }
foreach ($file in $filesToSign) {
    & "C:\Program Files (x86)\Windows Kits\10\bin\10.0.19041.0\x64\signtool.exe" sign /f $newCertPath /p $newCertPassword /fd SHA256 /t http://timestamp.digicert.com "$($file.FullName)"
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Signing $($file.FullName) failed. Please check errors above."
        exit 1
    }
}

Write-Host "All files signed successfully with the new certificate."

# Create AppxManifest.xml if it doesn't exist
if (-not (Test-Path $MSIX_MANIFEST_PATH)) {
    Write-Host "Creating AppxManifest.xml..."
    @"
<?xml version="1.0" encoding="utf-8"?>
<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10" xmlns:mp="http://schemas.microsoft.com/appx/2014/phone/manifest" xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10" xmlns:uap2="http://schemas.microsoft.com/appx/manifest/uap/windows10/2" xmlns:uap3="http://schemas.microsoft.com/appx/manifest/uap/windows10/3">
  <Identity Name="ContosoSoftware.Simple" Publisher="CN=Contoso Software, O=Contoso Corporation, C=US" Version="1.0.0.0" />
  <Properties>
    <DisplayName>Simple</DisplayName>
    <PublisherDisplayName>Contoso Corporation</PublisherDisplayName>
    <Logo>Assets\StoreLogo.png</Logo>
  </Properties>
  <Dependencies>
    <TargetDeviceFamily Name="Windows.Universal" MinVersion="10.0.0.0" MaxVersionTested="10.0.19041.0" />
  </Dependencies>
  <Resources>
    <Resource Language="en-us" />
  </Resources>
  <Applications>
    <Application Id="App" Executable="Simple.exe" EntryPoint="Simple.App">
      <uap:VisualElements DisplayName="Simple" Description="Simple App" BackgroundColor="transparent" Square150x150Logo="Assets\Square150x150Logo.png" Square44x44Logo="Assets\Square44x44Logo.png">
        <uap:DefaultTile Wide310x150Logo="Assets\Wide310x150Logo.png" Square310x310Logo="Assets\Square310x310Logo.png">
          <uap:ShowNameOnTiles>
            <uap:ShowOn Square150x150Logo="true" Wide310x150Logo="true" Square310x310Logo="true" />
          </uap:ShowNameOnTiles>
        </uap:DefaultTile>
        <uap:SplashScreen Image="Assets\SplashScreen.png" />
      </uap:VisualElements>
    </Application>
  </Applications>
</Package>
"@ | Out-File -FilePath $MSIX_MANIFEST_PATH -Encoding utf8
}

# Create MSIX package
Write-Host "Creating MSIX package..."
$makeAppxPath = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.19041.0\x64\makeappx.exe"
& $makeAppxPath pack /d $OUTPUT_DIR /p $MSIX_OUTPUT_PATH /m $MSIX_MANIFEST_PATH
if ($LASTEXITCODE -ne 0) {
    Write-Host "MSIX package creation failed."
    exit 1
}

Write-Host "Process complete! The MSIX package is saved at $MSIX_OUTPUT_PATH."