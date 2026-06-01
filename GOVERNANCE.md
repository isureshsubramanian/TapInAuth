# Governance

## Current state (June 2026 →)

TapInAuth is a solo-maintainer project led by [@sureshsubramanian](https://github.com/isureshsubramanian). Decisions are made by the maintainer with public input via GitHub Issues and Discussions.

## Decision-making

- **Technical direction**: maintainer decides, after public discussion.
- **Backwards compatibility**: governed by SemVer. Breaking changes require a major version bump.
- **Security issues**: handled privately (see [SECURITY.md](SECURITY.md)).
- **Community standards**: governed by [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).

## Path to .NET Foundation incubation

We are targeting incubation under the [.NET Foundation](https://dotnetfoundation.org/) once these prerequisites are met:

- [x] MIT license
- [x] DCO sign-off (no CLA)
- [x] CODE_OF_CONDUCT.md (Contributor Covenant)
- [x] CONTRIBUTING.md
- [x] SECURITY.md
- [x] GOVERNANCE.md
- [ ] At least one external contributor merged
- [ ] Tagged release with signed packages
- [ ] At least one independent maintainer with merge rights (bus-factor mitigation)
- [ ] No copyleft transitive dependencies

When all boxes are checked, we will file the .NET Foundation incubation proposal. Post-incubation, the repository will move to the `dotnetfoundation` org and governance will evolve toward a small steering committee.

## Adding maintainers

After 1.0 and once external contributions are flowing, we will publish a maintainer ladder. Until then, the maintainer may invite collaborators by direct outreach.
