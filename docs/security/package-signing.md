# Package signing

What a released `ForkForward.Mapper` package carries to show where it comes from, how to check it, and how to switch on the author signature. For maintainers; the release steps themselves are in [ROADMAP.md](../../ROADMAP.md), phase 2.

## What a package carries

| | Made by | On which packages | Needs |
|---|---|---|---|
| Repository signature | nuget.org, when it accepts a package | every package on nuget.org | nothing |
| Build provenance attestation | the release workflow, signed through GitHub's Sigstore instance and kept by GitHub | every release from 15.0.0 | nothing |
| Author signature | the release workflow, with the project's code-signing certificate | releases made after a certificate is configured | a certificate, which the project does not have yet |

The repository signature says that the package is the one nuget.org received. The attestation says which workflow run, from which commit and tag of this repository, built the file. The author signature says that the holder of a certificate issued to the project signed it; it is the only one of the three that NuGet clients can be told to require.

## Checking a package

The attestation, for a file taken from the [GitHub release](https://github.com/ForkForward/dotnet-mapper/releases):

```
gh attestation verify ForkForward.Mapper.<version>.nupkg -R ForkForward/dotnet-mapper
```

Use the file from the GitHub release, not the one from nuget.org: nuget.org adds its repository signature to the file, so its bytes, and with them the digest the attestation is about, are no longer the ones that were built.

The signatures, for a file from either place:

```
dotnet nuget verify --all ForkForward.Mapper.<version>.nupkg
```

It prints one block for each signature, with its type (`Author`, `Repository`) and the certificate.

## Switching on the author signature

Three things, and the first two are the owner's to do.

**1. A code-signing certificate.** nuget.org accepts an author signature only from a certificate that chains to a root Windows trusts, is valid for code signing and has an RSA key of 2048 bits or more; a self-issued certificate is rejected. Certificate authorities issue such certificates with the private key in a hardware token or a cloud key store, from which it cannot be exported. For a release workflow that means a cloud key store; the one wired here is Azure Key Vault. A `.pfx` file works too, where one exists.

**2. The certificate registered on nuget.org.** Account settings (or the organization's settings), "Certificates", "Register new", with the certificate as a `.cer` file in binary DER format. From then on nuget.org rejects every package for that account that is not signed with a registered certificate, and it rejects a signed package whose certificate is not registered. So register the certificate and set the variables below between two releases, not during one.

**3. The repository settings.** The release workflow signs when the repository variable `SIGNING_PROVIDER` is set. It has to be a repository variable, not one of the `release` environment: it decides whether the job runs at all. The other settings can live in the `release` environment, which is where the secrets belong.

| Setting | Kind | For | Value |
|---|---|---|---|
| `SIGNING_PROVIDER` | repository variable | both | `azure-key-vault` or `certificate-file`. Empty or absent: packages are not signed |
| `SIGNING_CERTIFICATE_FINGERPRINT` | variable | required for `certificate-file`, recommended for `azure-key-vault` | The SHA-256 fingerprint of the certificate. The packages are then also checked to be signed with exactly this certificate |
| `SIGNING_TIMESTAMP_URL` | variable | optional | An RFC 3161 timestamp server. Default: `http://timestamp.digicert.com` |
| `SIGNING_KEY_VAULT_URL` | variable | `azure-key-vault` | The URL of the vault |
| `SIGNING_KEY_VAULT_CERTIFICATE` | variable | `azure-key-vault` | The name of the certificate in the vault |
| `AZURE_CLIENT_ID`, `AZURE_TENANT_ID` | variables | `azure-key-vault` | An app registration or managed identity that may read the certificate and sign with its key, with a federated credential for this repository and the environment `release`. No Azure secret is stored: the job signs in with its GitHub OIDC token |
| `SIGNING_CERTIFICATE` | secret | `certificate-file` | The `.pfx` file, Base64-encoded |
| `SIGNING_CERTIFICATE_PASSWORD` | secret | `certificate-file` | Its password |

The roles the identity needs in the vault are in the documentation of the [Sign CLI](https://github.com/dotnet/sign).

## What the workflow does

`release.yml` has a job `sign` between `build` and `publish`:

- It runs only when `SIGNING_PROVIDER` is set, in the `release` environment, on Windows (the Sign CLI runs nowhere else).
- It installs a fixed version of the Sign CLI and calls [`Sign.ps1`](../../Sign.ps1), which signs every `.nupkg` and `.snupkg`, with a timestamp, and then runs `dotnet nuget verify` on each.
- A package that does not verify as signed by a certificate the runner trusts fails the job. `publish` does not run then, and nothing is published.
- When the job is skipped, `publish` takes the packages as they were built.

`publish` attests the packages it is about to push, signed or not, and then pushes them.

With required reviewers on the `release` environment, a release that signs asks for approval twice: once for `sign` and once for `publish`.

`Sign.ps1` can be run by hand, for instance to try a new certificate before a release:

```powershell
dotnet tool install --tool-path sign-tool sign --version 0.9.1-beta.26552.4
./Build.ps1
./Sign.ps1 -Provider azure-key-vault -KeyVaultUrl https://<vault>.vault.azure.net -KeyVaultCertificate <name> -SignTool ./sign-tool/sign.exe
```

## What has been tried, and what has not

- `Sign.ps1` with `certificate-file` was run on Windows on 2026-11-19 with a throw-away, self-issued certificate: the `.nupkg` and the `.snupkg` both got an author signature with a DigiCert timestamp, and the verification rejected them because nothing trusts that certificate, which is what it is there for. A wrong password, a missing fingerprint and a folder without packages each stop the script with a message.
- `azure-key-vault` has never been run. Its command line is written from the help of the Sign CLI at the version above and from that project's sample workflow. Its first release is its first test; it fails before anything is published if it is wrong.
- The `sign` job and the attestation step have not run: a workflow that starts from a version tag cannot be tried without a release.
- Azure Artifact Signing (formerly Trusted Signing) is not wired. The Sign CLI can sign with it, but its certificates live for a few days, and nuget.org registers certificates one by one; whether nuget.org accepts them was not confirmed.
- No SBOM is produced yet.
