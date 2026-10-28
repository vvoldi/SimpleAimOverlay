param([string]$CertificateThumbprint = "EA96448AEE620904B58AFD355ACC6657BF16828E")
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    $cert = Get-Item "Cert:\CurrentUser\My\$CertificateThumbprint"
    if (!$cert.HasPrivateKey) { throw "The signing certificate requires its private key in the local Windows certificate store." }
    $installerSource = Get-Content packaging/Installer/Program.cs -Raw
    if (!$installerSource.Contains($cert.Thumbprint)) { throw "Installer certificate pin does not match. Update the pin deliberately before changing signing identity." }
    dotnet publish SimpleAimOverlay.csproj -c Release -r win-x64 -p:UseUiAccess=true
    if ($LASTEXITCODE) { throw "Application publish failed." }
    $payload = Join-Path $root "packaging/Installer/SimpleAimOverlay.payload.exe"
    Copy-Item bin/Release/net10.0-windows/win-x64/publish/SimpleAimOverlay.exe $payload -Force
    $signature = Set-AuthenticodeSignature $payload $cert -HashAlgorithm SHA256 -TimestampServer "http://timestamp.digicert.com"
    if ($signature.Status -ne "Valid") { throw "Payload signature failed: $($signature.Status)" }
    Export-Certificate -Cert $cert -FilePath packaging/Installer/SimpleAimOverlay.Test.cer | Out-Null
    dotnet publish packaging/Installer/Setup.csproj -c Release -r win-x64
    if ($LASTEXITCODE) { throw "Installer publish failed." }
    New-Item -ItemType Directory -Force dist | Out-Null
    $version = ([xml](Get-Content SimpleAimOverlay.csproj)).Project.PropertyGroup.Version
    $output = Join-Path $root "dist/SimpleAimOverlay-$version-Setup.exe"
    Copy-Item packaging/Installer/bin/Release/net10.0-windows/win-x64/publish/SimpleAimOverlay-Setup.exe $output -Force
    $signature = Set-AuthenticodeSignature $output $cert -HashAlgorithm SHA256 -TimestampServer "http://timestamp.digicert.com"
    if ($signature.Status -ne "Valid") { throw "Installer signature failed: $($signature.Status)" }
    Get-FileHash $output -Algorithm SHA256
} finally { Pop-Location }
