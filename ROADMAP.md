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
- [x] The unit tests also run with mapping plans interpreted (dynamic code switched off), and built with the .NET 11 SDK on the .NET 11 runtime, on Windows and Ubuntu. Both are new CI jobs, to be added when the checks are made required
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
- [ ] **Decision needed: Native AOT and trimming.** Not supported today. The build says so, and [Native AOT and trimming](docs/source/Native-AOT-and-trimming.md) has what was measured: nothing works without keeping assemblies from trimming by hand, 16 of 25 scenarios with it, plans interpreted and several times slower. Supporting it means annotating the public API so that mapped types survive trimming, keeping the library's own reflection targets, a clear error for interface proxies, and a Native AOT test in CI; plans would still be interpreted. Real ahead-of-time speed would need generated code instead of expression trees, which is a different library
- [ ] Under Native AOT a deeply nested source graph ends the process, although the fix for CVE-2026-32933 holds everywhere else ([docs/security/CVE-2026-32933.md](docs/security/CVE-2026-32933.md#native-aot)). Find the cause, whatever is decided about Native AOT
- [ ] Differences when plans are interpreted: an error from a member that maps through the context is not wrapped, and `ForPath` through fields of nested value types does not set the values. Find out whether they can be fixed here; the tests are marked `CompiledPlansFact`
- [ ] Compare the public API with the previous ForkForward.Mapper release on every pull request (for example with `Microsoft.DotNet.ApiCompat.Tool`), replacing the comparison with AutoMapper 14.0.0 that the rename made obsolete
- [x] Publish the documentation: [forkforward.dev/dotnet-mapper](https://forkforward.dev/dotnet-mapper/), built with Zensical from `docs/source` and published by the Docs workflow on every change to `master`
- [x] No separate dependency injection package or repository: `AddAutoMapper` has been part of the main library since before the fork point (`src/AutoMapper/ServiceCollectionExtensions.cs`)
- [ ] Organisation profile with a catalogue of ForkForward projects by technology

## Phase 4: what AutoMapper gained after 14

AutoMapper 15, 16 and 17 were compared with this fork on 2026-11-19, from public descriptions only: [gap analysis](docs/analysis/upstream-gap-analysis-2026-11.md). What applies to an MIT-licensed library was designed and written here. The items below are in the working tree for the release that follows 14.1.1 and are described in [CHANGELOG.md](CHANGELOG.md).

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

Decisions for a maintainer before these are released:

- [ ] **Decision needed: the behaviour changes.** Four of the items above change what existing code can observe, which [AI_POLICY.md](AI_POLICY.md) leaves to a maintainer: what a condition on `ForAllMembers` sees (the widest of the four: it also reaches a source collection that is `null`), the exception type when a plan cannot be built, and `MaxDepth` through constructors and in derived maps. Each is isolated and can be taken out on its own. The condition change can also be narrowed to members whose source and destination types differ, which is one condition in `TypeMapPlanBuilder.ConditionSourceMember`
- [ ] **Decision needed: new members on public interfaces.** `IMemberConfigurationExpression`, `IMappingExpressionBase`, `IProjectionExpressionBase`, `IGlobalConfigurationExpression` and `IGlobalConfiguration` gained members without a default implementation. Nothing that calls them is affected; a type outside the library that implements one of them no longer compiles
- [ ] **Decision needed: two plan errors that stay unnamed.** A `ForPath` member without `MapFrom` still fails with a bare `NullReferenceException`, and a request for a map to an open generic type with a bare `ArgumentException`, because existing tests pin those types (`ForPathWithNullExpressionShouldFail`, `GenericMapWithUntypedMap`). Naming the first one too means changing that test
- [ ] **Decision needed: target frameworks**, together with the open item in phase 1. AutoMapper 17.0.0 targets .NET 10, .NET 11, .NET Standard 2.0 and .NET Framework 4.7.1; .NET 8 and .NET 9 reached the end of support on 2026-11-10. The `net8.0` build here runs on every later .NET
- [ ] Signed packages and an SBOM attached to each release, as AutoMapper has since 16.0.0 and 16.2.0. Signing needs a code-signing certificate

Not taken over, by decision or because it does not apply: a default `MaxDepth` of 64 (this fork protects the process with the stack check instead, and every graph that mapped before maps the same); licence keys and the logger factory that reports on them; the dependency on a JWT library.

Found while doing this, all present in 14.0.0 as well, none fixed yet:

- [ ] Registration order still matters for some open generic configurations: an open generic base map that itself includes a base, two chained open generic maps, and `IncludeMembers` of a closed generic map that has its own `IncludeMembers`. The closed map an include creates is sealed while profiles are still being configured. Deferring that broke configurations that work today, so it needs its own design
- [ ] `IncludeAllDerived()` alone on an open generic map does not reach closed derived maps, and a non-generic base map that includes an open generic map is not inherited by the closed map `IncludeBase` creates
- [ ] `ProjectTo` with included derived maps throws a `NullReferenceException` when the base map reaches its `MaxDepth`; and with `AllowNullDestinationValues = false`, constructor parameters are projected past `MaxDepth`
- [ ] The plan of a member that is mapped through the context is cached by its types alone, so members of different maps with the same types share the plan built for the first of them: at a `MaxDepth` limit the collection members of the last level are filled or left empty depending on which map was used first
- [ ] Flattening over three levels across two different naming conventions is not matched, in either direction (`CustomerAddressCity` from `customer.address.city`)
- [ ] An inlined member with a user-defined map from `T` to `T?` that has a type converter, and a value converter on a flattened member, throw "Nullable object must have a value" for a null on the way
- [ ] `PreserveReferences()` on a map whose source is a value type fails when the configuration is created
- [ ] The non-generic `CreateMap(typeof(S), typeof(D))` with a value type fails when the configuration is created for `ConstructUsing`, `BeforeMap` and `AfterMap`
- [ ] A type map whose plan could not be built stays sealed without a plan, and a later request of another shape for it fails with a `NullReferenceException` inside the plan error
- [ ] `MaxDepth` has no effect on a map with `ConvertUsing`, and `ReverseMap` does not carry `MaxDepth` to the reverse map. Documented; decide whether either should change

Left open by the review of the changes above, all in the new code:

- [ ] A derived map that inherits from two maps that are not related to each other (two interfaces) takes the `MaxDepth` of the one included first
- [ ] When creating a destination throws and code inside the same `Map` call catches the error and maps on, the level stays counted for the rest of that call. A failure that ends the `Map` call is not affected
- [ ] Open generic classes are closed by two rules, each taken from its counterpart in 14: a condition class like an open generic value resolver, a destination factory like an open generic type converter. On a map with one closed and one open side they give different type arguments. `ForAllMaps` cannot give a closed map an open generic factory
- [ ] `ConstructUsing(Type)` with a factory that implements `IDestinationFactory` for several compatible type pairs takes the first one it finds; a condition class in the same situation is refused
