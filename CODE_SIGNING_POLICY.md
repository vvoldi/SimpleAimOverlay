# Code signing policy

Releases use the maintainer's self-signed code-signing certificate. This is a personal/friends distribution model, not a publicly trusted CA signature. No SignPath endorsement or signing is claimed.

The EXE installer explicitly asks for consent before trusting the public certificate in the local machine Root and TrustedPublisher stores. The UIAccess app is installed in Program Files. The installer pins the certificate thumbprint. Updates reuse the same certificate; trusting a certificate does not eliminate Windows administrator or SmartScreen prompts.

The private key stays in the maintainer's Windows certificate store. Neither the repository nor the installer contains a private key. Only the public certificate is embedded in Setup. Generated payloads, installers and certificate exports are excluded from Git.

Build installable releases with scripts/Build-SelfSignedRelease.ps1 using the existing certificate. Do not generate a fresh certificate for every release. Changing the signing identity requires a deliberate installer pin update and renewed user trust.

Unsigned GitHub Actions artifacts are for build checks, not ready-to-install UIAccess releases. Published releases include a signed app, a signed installer and a SHA-256 checksum.

Maintainer: [@vvoldi](https://github.com/vvoldi).
Settings and images stay locally in %LOCALAPPDATA%/SimpleAimOverlay. The app does not require an account or send telemetry.
