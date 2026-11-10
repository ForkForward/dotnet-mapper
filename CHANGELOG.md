# Changelog

All notable changes to ForkForward.Mapper. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/); versions follow [SemVer](https://semver.org/) and continue AutoMapper's 14.x line, which is the code this fork starts from.

## [Unreleased]

### Changed

- The documentation is published at [forkforward.dev/dotnet-mapper](https://forkforward.dev/dotnet-mapper/), and the package's project website on nuget.org points there instead of the GitHub repository. The repository is still linked from the package as its source.

## [14.1.0] - 2026-11-10

The first stable release of ForkForward.Mapper. It is AutoMapper 14.0.0 (plus the three commits that followed it in the MIT-licensed repository) with its own identity, a security fix, and the build and release infrastructure of this fork. There are no functional changes to mapping.

### Changed

- **Own assembly name, namespace and signing key.** The assembly is `ForkForward.Mapper.dll`, the namespaces start with `ForkForward.Mapper` (`ForkForward.Mapper.QueryableExtensions`, `ForkForward.Mapper.Internal`, and so on), and it is signed with the fork's own key (public key token `d1f0d2ab1c32618c`). Moving from AutoMapper 14 is a package reference and a `using` directive; see [Moving from AutoMapper 14](docs/source/Moving-from-AutoMapper-14.md). Type names that carry the old name, such as `AutoMapperMappingException`, `AutoMapperConfigurationException`, `AutoMapAttribute` and `AddAutoMapper`, are unchanged. The previews (`14.1.0-preview.2`, `14.1.0-preview.3`) still used the `AutoMapper` names.
- The package carries its own readme, this changelog, `LICENSE.txt` and `NOTICE.md`.

### Fixed

- **CVE-2026-32933** ([GHSA-rvv3-g6hj-g44x](https://github.com/advisories/GHSA-rvv3-g6hj-g44x), high): a source graph nested deeper than the thread's stack allows ended in a `StackOverflowException`, which cannot be caught and ends the process. It now ends in an `AutoMapperMappingException` that says the graph is nested too deeply and names the member. On the way, a second route to the same crash was closed: a mapping error a few hundred levels deep used to be caught, wrapped and thrown again at every level, and that alone used up the stack. Written independently from the public advisory; details in [docs/security/CVE-2026-32933.md](docs/security/CVE-2026-32933.md). No new default depth limit: every graph that mapped before maps the same.

### Removed

- The build no longer downloads the official AutoMapper 14.0.0 package to compare public APIs; with the namespace renamed the comparison no longer applies. An API comparison with the previous ForkForward.Mapper release is planned instead.

### Infrastructure

- Builds with the .NET 10 SDK; the library targets `net8.0`, the tests, the benchmark and the sample app target `net10.0`.
- CI on Windows (SQL Server LocalDB) and Ubuntu (SQL Server in a container).
- Every pull request that touches the library is measured against the last release on two runners (`Benchmark.ps1`); it must not be slower and must not allocate more. The release workflow measures once more before publishing.
- Dependency review and Dependabot on every pull request; publishing through NuGet Trusted Publishing, without stored API keys.

## [14.1.0-preview.3] - 2026-11-09

Preview with the `.slnx` solution format and the artifacts output layout; .NET 10 SDK only. Mapping unchanged.

## [14.1.0-preview.2] - 2026-11-09

First preview on nuget.org: AutoMapper 14.0.0 plus three commits, rebranded as a fork, with CI on Windows and Ubuntu. `AutoMapper` assembly name and namespaces, original signing key.

## Before the fork

The code starts at commit [`92816585`](https://github.com/AutoMapper/AutoMapper.Archive/commit/928165859deaf9a59c4433771b3c58a9536284dd) of the AutoMapper repository, which is release 14.0.0 plus three commits, under the MIT licence. Its history is in [docs/source](docs/source) (the upgrade guides) and in the [AutoMapper release notes](https://github.com/AutoMapper/AutoMapper.Archive/releases).

[Unreleased]: https://github.com/ForkForward/dotnet-mapper/compare/v14.1.0...HEAD
[14.1.0]: https://github.com/ForkForward/dotnet-mapper/compare/v14.1.0-preview.3...v14.1.0
[14.1.0-preview.3]: https://github.com/ForkForward/dotnet-mapper/compare/v14.1.0-preview.2...v14.1.0-preview.3
[14.1.0-preview.2]: https://github.com/ForkForward/dotnet-mapper/compare/v14.0.0...v14.1.0-preview.2
