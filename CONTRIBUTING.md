# Contributing to TapInAuth

Thanks for your interest! TapInAuth is an MIT-licensed open source project and we welcome contributions of any size — bug reports, fixes, docs, examples, and features.

## Ground rules

1. **DCO sign-off is required.** Every commit must include `Signed-off-by: Your Name <you@example.com>`. Use `git commit -s` to add it automatically. We use DCO (not a CLA) to keep the path to .NET Foundation incubation simple.
2. **MIT-compatible code only.** Don't paste code from copyleft (GPL/LGPL/AGPL) sources or proprietary projects.
3. **Tests required for behavior changes.** New features and bug fixes need accompanying unit or integration tests.
4. **Security issues go to SECURITY.md, not the issue tracker.**

## Development setup

You need:
- .NET SDK **10.0.300** or newer (see `global.json`).
- Any IDE that understands .NET 10 (Visual Studio 2026+, Rider, VS Code with the C# Dev Kit).

```bash
git clone https://github.com/tapinauth/tapinauth.git
cd tapinauth
dotnet restore
dotnet build -c Release
dotnet test  -c Release
```

## Branching & PRs

- Trunk-based development: branch off `main`, PR back into `main`.
- Squash-merge by default. Keep PR titles in Conventional Commits style (`feat:`, `fix:`, `docs:`, `refactor:`, `test:`, `chore:`).
- CI must be green before merge.
- A maintainer reviews every PR. We aim to respond within 5 business days.

## Style

- C# style is enforced by `.editorconfig` + Roslyn analyzers (`TreatWarningsAsErrors=true`). Don't disable analyzers without justification in the PR description.
- File-scoped namespaces. Nullable enabled. Async methods get the `Async` suffix and accept a `CancellationToken` last.
- Public APIs need XML docs. Internal APIs are encouraged but optional.

## Versioning & releases

- Versions are derived from git tags via [MinVer](https://github.com/adamralph/minver). Tag `v0.1.0` produces package version `0.1.0`.
- Releases are cut by a maintainer pushing a `v*` tag; the release workflow handles the rest.
- Breaking changes require a major version bump and a migration note in the changelog.

## Reporting bugs

Use the issue templates. Include:
- TapInAuth version + .NET TFM.
- Minimal reproducer (a failing test is gold).
- Expected vs actual behavior.

## Code of Conduct

This project follows the [Contributor Covenant](CODE_OF_CONDUCT.md). Be kind.
