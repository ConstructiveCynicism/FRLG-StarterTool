# Security Policy

## Supported Versions

Only the latest [release](https://github.com/ConstructiveCynicism/FRLG-StarterTool/releases) and any commits afterwards are considered "supported" in terms of security. 

## Reporting a Vulnerability

Private message ConstructiveCynicism at any point of contact:
- [Private vulnerability reporting](https://github.com/ConstructiveCynicism/FRLG-StarterTool/security/advisories/new) on GitHub
- @constructivecynicism on Discord

## Verifying a Download

Every release is built and attested by this repository's GitHub Actions workflow. To check that the exe you have is the one it built:

```
gh attestation verify FRLGStarterTool.exe --repo ConstructiveCynicism/FRLG-StarterTool
```

or compare the hash yourself. The exe is not code-signed, so Windows SmartScreen will warn on the first run of a new release.
