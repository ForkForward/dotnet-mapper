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
- [x] Tests, benchmark and sample app target `net10.0`, the unit and dependency injection tests also `net11.0`; CI builds with the .NET 11 SDK and runs the tests on the .NET 10 and the .NET 11 runtime
- [x] The unit tests also run with mapping plans interpreted (dynamic code switched off), and built with the .NET 11 SDK on the .NET 11 runtime, on Windows and Ubuntu. Both are new CI jobs, to be added when the checks are made required
- [x] Solution in the `.slnx` format; build output in the artifacts layout
- [x] **Decided: the library targets `net10.0` and `net11.0`.** Decided by the maintainer on 2026-11-19: the .NET versions that are in support. `net8.0` is no longer a target (.NET 8 and .NET 9 reached the end of support on 2026-11-10); `14.1.1` is the last release for them. .NET Standard and .NET Framework are not targets: the code this fork starts from had already dropped them
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
- [ ] **Decision needed: Native AOT and trimming.** Not supported today. The build says so, and [Native AOT and trimming](docs/source/Native-AOT-and-trimming.md) has what was measured: nothing works without keeping assemblies from trimming by hand, 16 of 25 scenarios with it, plans interpreted and several times slower. Supporting it means annotating the public API so that mapped types survive trimming, keeping the library's own reflection targets, a clear error for interface proxies, and a Native AOT test in CI; plans would still be interpreted. Real ahead-of-time speed would need generated code instead of expression trees, which is a different library
- [ ] Under Native AOT a deeply nested source graph ends the process, although the fix for CVE-2026-32933 holds everywhere else ([docs/security/CVE-2026-32933.md](docs/security/CVE-2026-32933.md#native-aot)). Find the cause, whatever is decided about Native AOT
- [ ] Differences when plans are interpreted: an error from a member that maps through the context is not wrapped, and `ForPath` through fields of nested value types does not set the values. Find out whether they can be fixed here; the tests are marked `CompiledPlansFact`
- [ ] Compare the public API with the previous ForkForward.Mapper release on every pull request (for example with `Microsoft.DotNet.ApiCompat.Tool`), replacing the comparison with AutoMapper 14.0.0 that the rename made obsolete
- [x] Publish the documentation: [forkforward.dev/dotnet-mapper](https://forkforward.dev/dotnet-mapper/), built with Zensical from `docs/source` and published by the Docs workflow on every change to `master`
- [x] No separate dependency injection package or repository: `AddAutoMapper` has been part of the main library since before the fork point (`src/AutoMapper/ServiceCollectionExtensions.cs`)
- [ ] Organisation profile with a catalogue of ForkForward projects by technology

## Phase 4: what AutoMapper gained after 14

AutoMapper 15, 16 and 17 were compared with this fork on 2026-11-19, from public descriptions only: [gap analysis](docs/analysis/upstream-gap-analysis-2026-11.md). What applies to an MIT-licensed library was designed and written here. The items below are for the release that follows 14.1.1 and are described in [CHANGELOG.md](CHANGELOG.md).

Capabilities:

- [x] An error while a mapping plan is built names the map and the member
- [x] Member conditions and preconditions as classes, created through the service constructor (`IMemberCondition`, `IMemberPreCondition`)
- [x] Destination factory classes, created through the service constructor (`IDestinationFactory`)
- [x] A switch for polymorphic projection (`PolymorphicProjection`)
- [x] A condition that declares the source member as `object` gets the source value

Fixes for bugs inherited from 14.0.0:

- [x] `MapAtRuntime` into a nullable value type
- [x] A run of capitals in a PascalCase destination member, with a lower_underscore source
- [x] An open generic map registered after the map that includes it as its base
- [x] "Context.Items are only available..." from a map that does not use them
- [x] `MaxDepth` through constructor parameters, and inherited by derived maps
- [x] A regression test for every route of GHSA-6vhm-j9h7-f9gh and GHSA-gpwp-5797-mjff: none of them ends the process here ([docs/security](docs/security/GHSA-6vhm-j9h7-f9gh-and-GHSA-gpwp-5797-mjff.md))

Decided, and what the decision was:

- [x] **The four behaviour changes and the new members on public interfaces** went in with pull request 25, which the maintainer merged on 2026-11-19: what a condition on `ForAllMembers` sees, the exception type when a plan cannot be built, `MaxDepth` through constructors and in derived maps, and the members `IMemberConfigurationExpression`, `IMappingExpressionBase`, `IProjectionExpressionBase`, `IGlobalConfigurationExpression` and `IGlobalConfiguration` gained
- [x] **Target frameworks:** `net10.0` and `net11.0`; see phase 1
- [x] **The defects found on the way are to be fixed**: asked for by the maintainer on 2026-11-19. The `ForPath` member without `MapFrom` was fixed with them, and its test, which pinned a bare `NullReferenceException`, was changed; that is the one existing test this changed. See below

Open, for the owner or a maintainer:

- [x] Every release gets a build provenance attestation, which needs no certificate, and the release workflow signs the packages as soon as a certificate is configured: [package signing](docs/security/package-signing.md)
- [ ] **Owner: a code-signing certificate.** Without one there is no author signature. It has to come from a certificate authority Windows trusts, be registered on nuget.org, and be reachable from the release workflow (Azure Key Vault, or a `.pfx` file where one exists). The first signed release is also the first run of the signing job
- [ ] An SBOM attached to each release, as AutoMapper has since 16.2.0
- [ ] **Decision needed: the version number of the next release.** Dropping `net8.0` breaks an application that updates the package while still on .NET 8 or .NET 9, and several fixes change behaviour. SemVer calls that a new major version; the 14.x line was chosen to say "compatible with AutoMapper 14", and 15 is a number AutoMapper has used for something else

Not taken over, by decision or because it does not apply: a default `MaxDepth` of 64 (this fork protects the process with the stack check instead, and every graph that mapped before maps the same); licence keys and the logger factory that reports on them; the dependency on a JWT library.

Defects found while doing this, all present in 14.0.0 as well, and fixed for the release that follows 14.1.1:

- [x] Registration order of open generic maps, the maps they include and the maps of included members: an open generic base that includes a base of its own, two chained open generic maps, `IncludeMembers` of a closed generic map with included members of its own or inherited ones
- [x] `IncludeMembers` of a map that inherits its own included members failed at every `Map` call
- [x] `ProjectTo` with derived maps at a `MaxDepth` limit, constructor parameters projected past the limit, and `ISet<T>` destination members
- [x] The plan of a member mapped through the context was shared by members of different maps with the same types
- [x] Flattening over three or more levels across two naming conventions
- [x] "Nullable object must have a value" for a null on the way into a nullable value type, with a type converter or a value converter
- [x] `PreserveReferences()` with a value type, and the non-generic `CreateMap` with a value type
- [x] A type map whose plan could not be built answered later requests with a `NullReferenceException`
- [x] A `ForPath` member without `MapFrom` failed with a bare `NullReferenceException`
- [x] The four points the review of the new code left open: `MaxDepth` inherited from unrelated maps, the depth counter after a caught failure, an ambiguous destination factory, open generic classes through `ForAllMaps`

Still open after that. Each was measured or read in the code while fixing the list above; none is new in this fork unless it says so:

- [ ] **Decision needed: a map above the root of an open generic chain** that includes the root with `Include` or `IncludeAllDerived` is inherited only when it is registered before the map that needs the closed map. In 12 of 24 orders its configuration is lost without an error (in 8 of those the configuration could not be created before). An addition of a few lines gives 24 of 24, but changes 4 orders that run today and builds that map's own plan earlier
- [ ] `IncludeAllDerived()` alone on an open generic map does not reach closed derived maps; a non-generic base map that includes an open generic map or the closed pair is not inherited; `Map<DestinationBase>(new SourceDerived())` returns the base type when the base is a closed generic map created by `IncludeBase`; the plan of that closed base map is built before the configured maps are known. Each needs the seal of such a map split from the point where it is created, which changes plans that run today
- [ ] `AfterMap` of an included member's map runs twice in a map that inherits the `IncludeMembers` through `IncludeBase`; `IncludeMembers("name")` on an open generic base map is not inherited by a closed map that includes it; a type cycle from a closed base map through a configured map and back is not detected
- [ ] In `ProjectTo`, `MaxDepth` counts the uses of a map in the whole query, not along one path: of two members of the same type the second can come out empty, and a row of a derived type gets fewer levels than a row of the base type. `MaxDepth` on a map with an abstract destination has no effect there. `Collection<T>` and `ReadOnlyCollection<T>` of mapped elements, and immutable collections, cannot be projected; they fail with a clear error now
- [ ] **Decision needed: a value converter given its source member as a lambda** (`o.ConvertUsing(converter, s => s.Inner.Number)`) is not null checked: a null on the way is a `NullReferenceException`, for every destination type, as for member value resolvers. Checking it would call converters of non-nullable members with a default value
- [ ] `mapper.Map<TSource, TDestination>(source)` on a map created with `CreateMap(Type, Type)` and a lambda `ConvertUsing` throws `InvalidCastException`; a construction function that returns null for a struct destination ends in a `NullReferenceException`
- [ ] A destination name that needs both readings of capitals at once (`ABCustomerXMLAddress` from `a_b_customer.xml_address`) is not matched
- [ ] An open generic type converter given through `ForAllMaps` to a map that was not made from an open map is never closed and fails at mapping time
- [ ] A top-level `Map` call and `CompileMappings` share one plan per pair of types, built by whichever comes first: `Map<List<int>, List<int>>(null, existing)` gives a different result after `CompileMappings()`. A member mapped through the context by a built-in mapper gets the global profile's null handling, not its map's
- [ ] **Decision needed: `CompileMappings` and the plans of members.** Since plans are cached per member, `CompileMappings` also compiles the plan of every member that is not inlined, so that first use stays fast; in a model where every type refers to every other it takes about half as long again. The loop can be dropped at the price of a slower first use. It covers the members of the configured maps; those of closed generic maps created while the configuration is built are still compiled on first use
- [ ] **Decision needed: `ReverseMap` and a member mapped from somewhere else.** A flattened member that the conventions matched to a chain (the default conventions at any depth, or a chain of two across two conventions) keeps that chain when `MapFrom` maps it from somewhere else, and the reverse map writes the member's value both to where `MapFrom` reads it and to the chain, creating the objects on the way; so does AutoMapper 14. A chain that only the new reading of names finds is not written back for such a member, nor for an ignored one
- [ ] Two open generic maps that name each other as base overflow the stack in `MapperConfiguration.GetDerivedTypeMaps`, in 14.1.1 as well
- [ ] `MaxDepth` has no effect on a map with `ConvertUsing`, and `ReverseMap` does not carry `MaxDepth` to the reverse map. Documented; decide whether either should change
- [ ] A request for a map to an open generic type (`typeof(Destination<>)`) fails with a bare `ArgumentException`: an argument error that names the type, pinned by a test, left as it is
