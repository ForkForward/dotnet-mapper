# Roadmap

Where ForkForward.Mapper is going, and the decisions that are still open. Open decisions are marked **Decision needed**; nothing depending on them is done until a maintainer has recorded the answer here.

## Phase 0: identity and housekeeping

- [x] State clearly that this is a fork of AutoMapper, who wrote the original, and under what licence ([README.md](README.md), [NOTICE.md](NOTICE.md))
- [x] Own name and logo; remove claims that applied only to the original project
- [x] Rules for AI-assisted development ([AI_POLICY.md](AI_POLICY.md))
- [x] Issue forms, pull request template, code owners, label set
- [x] Tests compile and pass with the .NET 10 SDK
- [x] CI on Windows (SQL Server LocalDB) and on Ubuntu (SQL Server in a container), with its SDKs installed explicitly
- [x] CI green on GitHub on both systems
- [x] Repository description and topics `forkforward`, `dotnet`, `object-mapping`
- [ ] Repository settings: issues enabled, labels applied

## Phase 1: keep it building

- [x] Performance gate: speed and allocations are compared with the last release on pull requests and before publishing (`Benchmark.ps1`, the Benchmark workflow)
- [x] Dependency review on pull requests: known vulnerabilities and licences of added or updated dependencies
- [x] Dependabot version updates switched on in the repository settings. This repository is a fork, and on a fork `dependabot.yml` does nothing until they are enabled there
- [ ] Make the CI, Benchmark and Dependency review checks required on `master`
- [ ] Work through the Dependabot updates
- [x] Fix CVE-2026-32933 independently: a source graph too deep for the stack ends in an exception, not in a process crash ([docs/security/CVE-2026-32933.md](docs/security/CVE-2026-32933.md))
- [ ] Publish a GitHub security advisory for `ForkForward.Mapper` up to 14.1.0-preview.3, so that scanners flag the old previews (repository Security tab; only a maintainer can do this)
- [x] ~~Make the API comparison a hard gate~~ Removed with the namespace rename in 14.1.0: the comparison with the `AutoMapper` assembly no longer applies. Replacement planned in phase 3
- [x] Tests, benchmark and sample app target `net10.0`; CI builds with the .NET 10 SDK only
- [x] Solution in the `.slnx` format; build output in the artifacts layout
- [ ] Decide which .NET versions the library targets (today `net8.0`)
- [x] Review the inherited documentation for references to infrastructure that the fork does not have (the MyGet page removed, the index points at this fork's NuGet package, changelog and issues)

## Phase 2: first release of `ForkForward.Mapper`

The previews `14.1.0-preview.2` and `14.1.0-preview.3` were published with the `AutoMapper` names. The three decisions below were taken by the maintainer on 2026-11-10 and apply from `14.1.0`, the first stable release.

1. **Decided: own namespace and assembly name.** The assembly is `ForkForward.Mapper.dll` and the namespaces start with `ForkForward.Mapper`. The fork cannot be confused with the original, and moving a project is a package reference and a `using` directive ([Moving from AutoMapper 14](docs/source/Moving-from-AutoMapper-14.md)). Libraries compiled against `AutoMapper.dll` have to be recompiled. Type names that carry the old name (`AutoMapperMappingException`, `AutoMapperConfigurationException`, `AutoMapAttribute`, `AddAutoMapper`) are kept; renaming them is a phase 3 decision.
2. **Decided: own strong-name key.** `ForkForward.Mapper.snk`, public key token `d1f0d2ab1c32618c`. The original key is no longer in the repository.
3. **Decided: version numbers continue from 14.** `14.1.0` is the first stable release; the line signals compatibility with AutoMapper 14. Tags `v*.*.*` drive MinVer.
4. Publishing from the release workflow: working since `14.1.0-preview.2`. The workflow publishes through NuGet Trusted Publishing, with no stored API key, and since `14.1.0` also creates the GitHub release with the changelog entry as its notes. It needs three things outside this repository: a Trusted Publishing policy on nuget.org, owned by the `ForkForward` organization, for `ForkForward/dotnet-mapper`, workflow file `release.yml`, environment `release`; the `NUGET_USER` secret with the nuget.org user name of the person who created that policy (not the organization); and, recommended, required reviewers on the `release` environment. Nothing is published until a version tag is pushed.
5. [ ] Reserve the `ForkForward` prefix on nuget.org (a request to the NuGet team; only the organization owner can make it).
6. [x] NuGet-specific readme (`src/ForkForward.Mapper/PACKAGE.md`) and the guide [Moving from AutoMapper 14](docs/source/Moving-from-AutoMapper-14.md).
7. [x] `CHANGELOG.md`, linked from the package as release notes.

- [x] `14.1.0` released on 2026-11-10: tag `v14.1.0`, [package on nuget.org](https://www.nuget.org/packages/ForkForward.Mapper/14.1.0), [GitHub release](https://github.com/ForkForward/dotnet-mapper/releases/tag/v14.1.0) created by the workflow; the benchmark baseline is now `14.1.0`.

Release steps for the next version: merge what goes in; push the tag `v<version>`; the release workflow builds, runs the benchmark against the baseline, publishes to nuget.org and creates the GitHub release; then raise `BaselineVersion` in `src/Benchmark.Baseline/Benchmark.Baseline.csproj` (and `BaselineNamespace`, should the namespace ever change again).

## Phase 3: beyond the first release

- [ ] **Decision needed: type names that carry the old name.** `AutoMapperMappingException`, `AutoMapperConfigurationException`, `AutoMapAttribute`, `AddAutoMapper`. Renaming them completes the identity change but breaks source compatibility; the usual way is new names with the old ones kept as `[Obsolete]` forwarders for a release or two.
- [ ] Compare the public API with the previous ForkForward.Mapper release on every pull request (for example with `Microsoft.DotNet.ApiCompat.Tool`), replacing the comparison with AutoMapper 14.0.0 that the rename made obsolete
- [ ] Publish the documentation
- [x] No separate dependency injection package or repository: `AddAutoMapper` has been part of the main library since before the fork point (`src/AutoMapper/ServiceCollectionExtensions.cs`)
- [ ] Organisation profile with a catalogue of ForkForward projects by technology
