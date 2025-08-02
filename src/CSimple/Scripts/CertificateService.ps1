# Certificate Management Service
# This service handles all certificate-related operations for the C-Simple application

# Function to check if a certificate with the same CN and O exists
function Test-CertificateExists {
    param (
        [string]$certPath,
        [string]$subject,
        [string]$password
    )
    if (Test-Path $certPath) {
        try {
            # Create secure string for the password
            $securePassword = ConvertTo-SecureString -String $password -Force -AsPlainText
            
            # Use X509Certificate2 class instead which supports password
            $cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($certPath, $securePassword)
            return $cert.Subject -eq $subject
        }
        catch {
            Write-Host "Error loading certificate: $_"
            return $false
        }
    }
    return $false
}

# Function to create a new self-signed certificate
function New-AppCertificate {
    param (
        [string]$subject,
        [string]$friendlyName = "CSimpleInstallerNewCert",
        [int]$validYears = 5
    )
    
    try {
        $cert = New-SelfSignedCertificate -Type Custom -Subject $subject `
            -KeyUsage DigitalSignature -FriendlyName $friendlyName `
            -CertStoreLocation "Cert:\CurrentUser\My" `
            -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}") `
            -NotAfter (Get-Date).AddYears($validYears)
        
        if (-not $cert) {
            throw "Certificate creation failed"
        }
        
        return $cert
    }
    catch {
        Write-Host "Failed to create certificate: $_" -ForegroundColor Red
        throw
    }
}

# Function to export certificate to both .cer and .pfx formats
function Export-AppCertificate {
    param (
        [System.Security.Cryptography.X509Certificates.X509Certificate2]$certificate,
        [string]$cerPath,
        [string]$pfxPath,
        [string]$password
    )
    
    try {
        # Export the certificate to .cer file
        Write-Host "Exporting certificate to $cerPath..."
        Export-Certificate -Cert $certificate -FilePath $cerPath
        
        # Export certificate to .pfx file
        Write-Host "Exporting certificate to $pfxPath..."
        $securePassword = ConvertTo-SecureString -String $password -Force -AsPlainText
        Export-PfxCertificate -Cert $certificate -FilePath $pfxPath -Password $securePassword
        
        Write-Host "Certificate exported successfully" -ForegroundColor Green
    }
    catch {
        Write-Host "Failed to export certificate: $_" -ForegroundColor Red
        throw
    }
}

# Function to sign files with the certificate
function Invoke-FilesSigning {
    param (
        [string]$outputDir,
        [string]$pfxPath,
        [string]$password,
        [string]$signtoolPath
    )
    
    try {
        Write-Host "Signing published files with certificate..."
        $filesToSign = Get-ChildItem -Path $outputDir -Recurse | Where-Object { $_.Extension -eq ".exe" -or $_.Extension -eq ".dll" }
        
        foreach ($file in $filesToSign) {
            & $signtoolPath sign /f "$pfxPath" /p "$password" /fd SHA256 /t http://timestamp.digicert.com "$($file.FullName)"
            if ($LASTEXITCODE -ne 0) {
                throw "Signing $($file.FullName) failed"
            }
        }
        
        Write-Host "All files signed successfully" -ForegroundColor Green
    }
    catch {
        Write-Host "File signing failed: $_" -ForegroundColor Red
        throw
    }
}

# Function to sign MSIX package
function Invoke-MsixSigning {
    param (
        [string]$msixPath,
        [string]$pfxPath,
        [string]$password,
        [string]$signtoolPath
    )
    
    try {
        Write-Host "Signing MSIX package with certificate..."
        & $signtoolPath sign /f "$pfxPath" /p "$password" /fd SHA256 /t http://timestamp.digicert.com "$msixPath"
        
        if ($LASTEXITCODE -ne 0) {
            throw "MSIX package signing failed"
        }
        
        Write-Host "MSIX package signed successfully" -ForegroundColor Green
    }
    catch {
        Write-Host "MSIX signing failed: $_" -ForegroundColor Red
        throw
    }
}

# Main certificate management function
function Initialize-AppCertificate {
    param (
        [string]$certDir,
        [string]$subject,
        [string]$password
    )
    
    $cerPath = Join-Path $certDir "SimpleCert.cer"
    $pfxPath = Join-Path $certDir "CSimple_NewCert.pfx"
    
    # Create certificate directory if it doesn't exist
    if (-not (Test-Path $certDir)) { 
        New-Item -ItemType Directory -Path $certDir -Force | Out-Null
    }
    
    # Check if certificate already exists
    Write-Host "Checking if certificate with the same CN and O already exists..."
    $certificateExists = (Test-CertificateExists -certPath $cerPath -subject $subject -password $password) -or 
    (Test-CertificateExists -certPath $pfxPath -subject $subject -password $password)
    
    if ($certificateExists) {
        Write-Host "Certificate with the same CN and O already exists. Using existing certificate." -ForegroundColor Green
        return @{
            CerPath = $cerPath
            PfxPath = $pfxPath
            IsNew   = $false
        }
    }
    else {
        Write-Host "Creating new self-signed certificate..."
        $newCert = New-AppCertificate -subject $subject
        
        # Export the certificate
        Export-AppCertificate -certificate $newCert -cerPath $cerPath -pfxPath $pfxPath -password $password
        
        return @{
            CerPath = $cerPath
            PfxPath = $pfxPath
            IsNew   = $true
        }
    }
}