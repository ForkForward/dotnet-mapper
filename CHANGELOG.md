# Changelog

All notable changes to ForkForward.Mapper. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/); versions follow [SemVer](https://semver.org/) and continue AutoMapper's 14.x line, which is the code this fork starts from.

## [Unreleased]

The first changes to mapping itself since the fork. They close the functional gap to AutoMapper 17.0.0, the newest release, as far as it applies to an MIT-licensed library: what was compared, what was left out and why is in [the gap analysis](docs/analysis/upstream-gap-analysis-2026-11.md). Everything was designed and written here, from public descriptions of what the newer versions do; none of their code, tests or documentation was used.

Two entries under "Changed" and two under "Fixed" alter what existing code can observe. They are marked **Behaviour change**.

### Added

- **Member conditions and preconditions can be classes.** `opt.Condition<TCondition>()` and `opt.PreCondition<TPreCondition>()` take a class that implements the new `IMemberCondition<TSource, TDestination, TMember>` or `IMemberPreCondition<TSource, TDestination>`; `Condition(Type)` and `PreCondition(Type)` do the same where the type is only known at run time. The class is created through the service constructor each time it is evaluated, like a value resolver class, so with `AddAutoMapper` it can take services in its constructor, and `AddAutoMapper` registers the classes it finds as transient. One class written against `object` can be used for every member of every map. A class that implements the interface more than once is refused when the configuration is built. See [Conditional mapping](docs/source/Conditional-mapping.md#conditions-as-classes).
- **The destination object of a map can be created by a factory class.** `ConstructUsing<TDestinationFactory>()` and `ConstructUsing(Type)` take a class that implements the new `IDestinationFactory<TSource, TDestination>`. The mapper creates it through the service constructor each time it needs a destination object, so with `AddAutoMapper` the factory comes from the container and can take dependencies, which a `ConstructUsing` lambda cannot. `AddAutoMapper` registers the factories it finds as transient. It replaces constructor mapping for that map and otherwise behaves like the `ConstructUsing((source, context) => ...)` overload, including that `ProjectTo` does not use it and derived maps do not inherit it. Open generic maps take an open generic factory type. See [Construction](docs/source/Construction.md#creating-the-destination-with-a-factory-class).
- **Polymorphic projection can be switched off.** When a map includes derived maps, `ProjectTo` tests the type of every row and creates the derived destination type; with Entity Framework Core that brings the discriminator, the derived columns or a join per derived table into the SQL, and some LINQ providers cannot translate it. `PolymorphicProjection(false)` on a map projects with that map alone, and `cfg.Internal().PolymorphicProjection = false` does so for every map that has no setting of its own; a map's own setting wins in both directions. The default is on and the projection is unchanged. `Map` is not affected, derived maps do not inherit the setting, and a map with an abstract destination type throws an `AutoMapperMappingException` when the projection is built with it off. See [Queryable Extensions](docs/source/Queryable-Extensions.md#polymorphic-projection).

The new members on `IMemberConfigurationExpression`, `IMappingExpressionBase`, `IProjectionExpressionBase`, `IGlobalConfigurationExpression` and `IGlobalConfiguration` have no default implementation. Code that calls these interfaces is not affected; a type outside the library that implements one of them has to add the members.

### Fixed

- **`MapAtRuntime` on a member that goes into a nullable value type no longer breaks the configuration.** When the value came from a chain (`s.Inner.Value`, `s.Inner.GetValue()`, an extension method call such as `s.Text.ToInt()`, or a member flattened by name) and the destination member was the nullable of its type, creating the configuration threw `ArgumentException: Expression of type 'System.Nullable`1[System.Int32]' cannot be used for parameter of type 'System.Int32' of method '... MapInternal ...'`. The same happened without `MapAtRuntime` for a member whose own type map lies beyond `MaxExecutionPlanDepth`. Such a member is now mapped and gives the value the inlined member gives, including null for a null on the way. Inherited from AutoMapper 14.
- **A PascalCase destination member with a run of capital letters is matched to a lower_underscore source member.** With `LowerUnderscoreNamingConvention` on the source and `PascalCaseNamingConvention` on the destination, `FormXML` was looked for as `form_x_m_l` and left unmapped; `XMLFile`, `UserID` and `HTTPSConnection` likewise. When the reading in which every capital starts a word finds nothing, a run of capitals is now read as one word, so `FormXML` is matched to `form_xml`. What was matched before is matched to the same member (`ABCount` to `a_b_count`), `PascalCaseNamingConvention.Split` returns what it did, and the default conventions are not affected. A member that was left unmapped for this reason now gets a value. Inherited from AutoMapper 14.
- **An open generic map can be registered after the map that includes it as its base.** `CreateMap<SourceDerived, DestinationDerived>().IncludeBase<SourceBase<FooBar>, DestinationBase>()` followed by `CreateMap(typeof(SourceBase<>), typeof(DestinationBase))` threw a `NullReferenceException` from the `MapperConfiguration` constructor, while the same two maps, or the same two profiles, in the other order worked. Inherited from AutoMapper 14.0.0; the configuration worked in 13.0.1. Some configurations with chained open generic bases still depend on the order; they are listed in [ROADMAP.md](ROADMAP.md).
- **No "Context.Items are only available..." from a map that does not use `Context.Items`.** When a collection member of a map with `MaxDepth` was mapped at run time (`MapAtRuntime`, or inlining used up) and a map without `MaxDepth` had a member with the same collection types, the second map threw that `InvalidOperationException`, but only when the first had been used before it. Inherited from AutoMapper 14.
- **Behaviour change: `MaxDepth` is applied when the next level is passed through a constructor.** A type map with `MaxDepth` whose destination takes the next level as a constructor parameter (a positional record, for example), or creates it in `ConstructUsing` by mapping with the context, ignored the limit and mapped the whole graph: a level was counted only after its destination had been created. It is now counted before, and such a graph stops at the same depth as one that continues through a property. Graphs that were mapped past the configured depth are now cut off at it. That includes the collections of the last level, which `MaxDepth` has always left empty in a property and now also leaves empty in a constructor parameter: with `MaxDepth(1)` on a map to `record OrderDto(List<LineDto> Lines)`, `Lines` used to be filled and is now empty, as a `Lines` property already was. Inherited from AutoMapper 14.
- **Behaviour change: a derived map inherits `MaxDepth`.** With `MaxDepth` on a base map only, objects of a derived type were mapped to any depth. A derived map that sets no `MaxDepth` now takes it from the map it inherits from, through `Include`, `IncludeBase` and `IncludeAllDerived`; the nearest map that sets one wins, and the derived map's own value wins over it. Depth is still counted per map, and projections are not affected. To keep a derived map without a limit, give it `MaxDepth(int.MaxValue)`. Inherited from AutoMapper 14. See [Configuration](docs/source/Configuration.md#maxdepth).
- **A mapping error is reported as itself where mapping plans are interpreted.** On a runtime that cannot generate code, plans are interpreted instead of compiled, and there every error while mapping a member surfaced as `NotSupportedException: Specified method is not supported` instead of an `AutoMapperMappingException` that names the member. The plan created the exception with a method declared on a `ref struct`, which reflection cannot invoke. Inherited from AutoMapper 14. Applications whose plans are compiled, which is the ordinary case, were not affected.

### Changed

- **Behaviour change: a condition that takes the source member as `object` gets the source value, not the converted one.** In AutoMapper 14 the third argument of a condition was always the value after it had been mapped to the type of the destination member, so `ForAllMembers(opt => opt.Condition((src, dest, sourceMember) => sourceMember != null))` never skipped a `null` that goes into an `int`, a `bool` or another value type: the condition got `0`, and the destination was overwritten with it. Such a condition now gets the value as it was resolved: `null`, the string instead of the `int` it is converted to, the source object instead of the mapped one, and the value before value transformers. A source collection that is `null` now arrives as `null` as well, where the condition used to get the empty collection it is mapped to: a condition that skips nulls now leaves that destination collection as it is instead of replacing it with an empty one. A condition that compares the source member with the destination member no longer finds them equal when the two members differ in type (an `int` and a `long`), and assigns the value again instead of skipping it. This applies where the condition declares `object` or a base type: `ForAllMembers`, `ForMember` with a member name, maps created from `Type` objects, `ForAllPropertyMaps`, and condition classes written that way. A typed `ForMember(dest => dest.Count, opt => opt.Condition(...))` is not affected, and neither are preconditions, `NullSubstitute`, or the order in which a member is resolved, mapped and checked. A condition that relied on the converted value has to move to a typed `ForMember`. See [What a condition sees](docs/source/Conditional-mapping.md#what-a-condition-sees).
- **Behaviour change: an error while a mapping plan is built says which map and which member.** When a part of a plan could not be built, `new MapperConfiguration(...)`, the first `Map` of a closed generic or run-time resolved map, `BuildExecutionPlan` and `ProjectTo` passed on whatever `System.Linq.Expressions` or a custom mapper, resolver or converter had thrown, with nothing that pointed to a map. It is now an `AutoMapperMappingException` that starts with `Error building the mapping plan.` (`Error building the projection.` for `ProjectTo`), carries the `TypeMap` and, when the error belongs to one member, the `MemberMap`, and has the original exception as its `InnerException`. Code that caught the bare `ArgumentException` or `InvalidOperationException` there has to catch `AutoMapperMappingException` instead; the exceptions the library throws on purpose are unchanged, and so are the plans. See [Understanding your mappings](docs/source/Understanding-your-mapping.md#when-a-plan-cannot-be-built).
- **The build warns when a trimmed or Native AOT application creates a mapper configuration.** The constructors of `MapperConfiguration` and every `AddAutoMapper` overload carry `RequiresUnreferencedCode` and `RequiresDynamicCode`. The library does not support trimming or Native AOT; until now such an application built without a word about it and failed when it started. No signature changed, and a project that is not trimmed or compiled ahead of time sees nothing new; a default Blazor WebAssembly application was checked and gets no warning. A library that switches the trim or AOT analyzers on and creates a mapper configuration gets `IL2026` and `IL3050` there.

### Security

- **GHSA-6vhm-j9h7-f9gh and GHSA-gpwp-5797-mjff do not crash this library.** The two advisories of November 2026 describe routes by which a deeply nested source graph gets around `MaxDepth` in AutoMapper and ends the process: constructor parameters and positional records, derived maps, members typed `object` or as an interface, nested dictionaries and `ExpandoObject`, and converters or construction functions that map through the context. This fork does not rely on `MaxDepth` for that protection: the stack check added for CVE-2026-32933 runs on every nested map call, and each of these routes, tried with 100,000 levels on a 256 KB stack, ended in an `AutoMapperMappingException`, in 14.1.0 and 14.1.1 as well. There is now a regression test for each route. The two defects the advisories describe for `MaxDepth` itself are fixed above. Details: [docs/security](docs/security/GHSA-6vhm-j9h7-f9gh-and-GHSA-gpwp-5797-mjff.md).

### Documentation

- [Gap analysis: AutoMapper 17.0.0 and ForkForward.Mapper 14.1.1](docs/analysis/upstream-gap-analysis-2026-11.md): what the newer AutoMapper versions added, what of it applies here, and how the comparison was made without their code.
- New sections: [conditions as classes](docs/source/Conditional-mapping.md#conditions-as-classes), [what a condition sees](docs/source/Conditional-mapping.md#what-a-condition-sees), [destination factory classes](docs/source/Construction.md#creating-the-destination-with-a-factory-class), [polymorphic projection](docs/source/Queryable-Extensions.md#polymorphic-projection), [when a plan cannot be built](docs/source/Understanding-your-mapping.md#when-a-plan-cannot-be-built), [`MaxDepth`](docs/source/Configuration.md#maxdepth).
- New page [Native AOT and trimming](docs/source/Native-AOT-and-trimming.md): neither is supported, what was measured when it was tried, and what differs on a runtime without dynamic code.
- New page [Asynchronous code](docs/source/Asynchronous-code.md): why mapping is synchronous, and how to combine it with code that awaits.
- [CVE-2026-32933](docs/security/CVE-2026-32933.md): the fix does not hold in an application compiled with Native AOT; a deeply nested source graph ends the process there.

### Infrastructure

- CI runs the unit tests twice more on Windows and Ubuntu: with dynamic code switched off, so that mapping plans are interpreted, and built with the .NET 11 SDK on the .NET 11 runtime.

## [14.1.1] - 2026-11-10

A packaging release: the package points at the new documentation site. The library is the same as in 14.1.0; there are no changes to the code.

### Changed

- The documentation is published at [forkforward.dev/dotnet-mapper](https://forkforward.dev/dotnet-mapper/), and the package's project website on nuget.org points there instead of the GitHub repository. The repository is still linked from the package as its source.
- The package readme links the documentation site, and the guide [Moving from AutoMapper 14](https://forkforward.dev/dotnet-mapper/Moving-from-AutoMapper-14/) there.

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

[Unreleased]: https://github.com/ForkForward/dotnet-mapper/compare/v14.1.1...HEAD
[14.1.1]: https://github.com/ForkForward/dotnet-mapper/compare/v14.1.0...v14.1.1
[14.1.0]: https://github.com/ForkForward/dotnet-mapper/compare/v14.1.0-preview.3...v14.1.0
[14.1.0-preview.3]: https://github.com/ForkForward/dotnet-mapper/compare/v14.1.0-preview.2...v14.1.0-preview.3
[14.1.0-preview.2]: https://github.com/ForkForward/dotnet-mapper/compare/v14.0.0...v14.1.0-preview.2
