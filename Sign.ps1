<#
.SYNOPSIS
  Adds the author signature to the NuGet packages of a release and checks it.

.DESCRIPTION
  Signs every .nupkg and .snupkg in a folder with a code-signing certificate, using the Sign CLI
  (https://github.com/dotnet/sign, the dotnet tool "sign"), which runs on Windows only, and then checks
  the result with "dotnet nuget verify".

  The certificate comes from one of two places:

    certificate-file   a .pfx file. The password is read from the environment variable
                       SIGNING_CERTIFICATE_PASSWORD, so that it is not on a command line of this script.
    azure-key-vault    a certificate in Azure Key Vault. The script uses the Azure CLI login of the
                       machine ("az login", or the azure/login action in a workflow).

  The release workflow calls this script when the repository variable SIGNING_PROVIDER is set.
  docs/security/package-signing.md says what has to be configured and what nuget.org expects.

  Exit code 0: every package is signed and verified. Anything else: an exception with the reason.

.EXAMPLE
  $env:SIGNING_CERTIFICATE_PASSWORD = '...'
  ./Sign.ps1 -Provider certificate-file -CertificateFile ./signing.pfx -CertificateFingerprint <SHA-256>

.EXAMPLE
  ./Sign.ps1 -Provider azure-key-vault -KeyVaultUrl https://example.vault.azure.net -KeyVaultCertificate signing

.EXAMPLE
  # Trying the script out with a certificate that no machine trusts: sign, and skip the trust check.
  ./Sign.ps1 -Provider certificate-file -CertificateFile ./test.pfx -CertificateFingerprint <SHA-256> -SkipVerification
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('certificate-file', 'azure-key-vault')]
    [string]$Provider,
    # The folder with the packages. They are signed in place.
    [string]$PackagesDirectory = './artifacts/package/release',
    # An RFC 3161 timestamp server. nuget.org accepts only signatures that carry a timestamp.
    [string]$TimestampUrl = 'http://timestamp.digicert.com',
    # certificate-file: the .pfx file.
    [string]$CertificateFile,
    # The SHA-256 fingerprint of the signing certificate. Required for certificate-file. When given, the
    # packages are also checked to be signed with exactly this certificate.
    [string]$CertificateFingerprint,
    # azure-key-vault: the URL of the vault and the name of the certificate in it.
    [string]$KeyVaultUrl,
    [string]$KeyVaultCertificate,
    # The Sign CLI: a command on the path, or the path of the executable.
    [string]$SignTool = 'sign',
    # Sign only. For trying the script out with a test certificate, which "dotnet nuget verify" rejects.
    [switch]$SkipVerification
)

$ErrorActionPreference = 'Stop'

function Assert-Value([string]$value, [string]$what) {
    if ([string]::IsNullOrWhiteSpace($value)) { throw "$what is required for the provider '$Provider'." }
}

$directory = (Resolve-Path $PackagesDirectory).Path
$packages = @(Get-ChildItem $directory -File | Where-Object { $_.Extension -in '.nupkg', '.snupkg' })
if ($packages.Count -eq 0) { throw "No .nupkg or .snupkg in $directory." }

# The same for every provider. The patterns are relative to the base directory.
$arguments = @(
    '*.nupkg', '*.snupkg',
    '--base-directory', $directory,
    '--timestamp-url', $TimestampUrl,
    '--description', 'ForkForward.Mapper',
    '--description-url', 'https://forkforward.dev/dotnet-mapper/'
)

switch ($Provider) {
    'certificate-file' {
        Assert-Value $CertificateFile '-CertificateFile'
        Assert-Value $CertificateFingerprint '-CertificateFingerprint'
        $arguments = @('code', 'certificate-store') + $arguments + @(
            '--certificate-file', (Resolve-Path $CertificateFile).Path,
            '--certificate-fingerprint', $CertificateFingerprint)
        if (-not [string]::IsNullOrEmpty($env:SIGNING_CERTIFICATE_PASSWORD)) {
            $arguments += @('--password', $env:SIGNING_CERTIFICATE_PASSWORD)
        }
    }
    'azure-key-vault' {
        Assert-Value $KeyVaultUrl '-KeyVaultUrl'
        Assert-Value $KeyVaultCertificate '-KeyVaultCertificate'
        $arguments = @('code', 'azure-key-vault') + $arguments + @(
            '--azure-credential-type', 'azure-cli',
            '--azure-key-vault-url', $KeyVaultUrl,
            '--azure-key-vault-certificate', $KeyVaultCertificate)
    }
}

Write-Host "== Signing $($packages.Count) package(s) in $directory ($Provider)"
& $SignTool @arguments
if ($LASTEXITCODE -ne 0) { throw "The Sign CLI failed with exit code $LASTEXITCODE." }

# The Sign CLI reports success per file it recognises; this makes sure none was left out.
Add-Type -AssemblyName System.IO.Compression.FileSystem
foreach ($package in $packages) {
    $archive = [IO.Compression.ZipFile]::OpenRead($package.FullName)
    try { $signed = [bool]($archive.Entries | Where-Object FullName -eq '.signature.p7s') }
    finally { $archive.Dispose() }
    if (-not $signed) { throw "$($package.Name) was not signed." }
}

if ($SkipVerification) {
    Write-Host '== Signed. Verification skipped, as asked.'
    return
}

$paths = $packages | ForEach-Object FullName
Write-Host '== Verifying'
& dotnet nuget verify --all $paths
if ($LASTEXITCODE -ne 0) { throw 'A package does not carry a valid signature from a certificate this machine trusts.' }
if (-not [string]::IsNullOrWhiteSpace($CertificateFingerprint)) {
    & dotnet nuget verify $paths --certificate-fingerprint $CertificateFingerprint
    if ($LASTEXITCODE -ne 0) { throw "A package is not signed with the certificate $CertificateFingerprint." }
}
Write-Host '== Signed and verified.'
