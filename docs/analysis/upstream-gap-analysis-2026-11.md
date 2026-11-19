# Gap analysis: AutoMapper 17.0.0 and ForkForward.Mapper 14.1.1

Date: 2026-11-19. Compared: AutoMapper 17.0.0 (released 2026-11-12, the newest release) and this repository at `fad68358` (14.1.1 plus the unreleased changes on `master`).

This document lists what AutoMapper gained after 14.0.0, the code this fork starts from, says for each item whether this fork lacks it, and turns the gaps into features with our own design. It also records how the comparison was made, because [AI_POLICY.md](../../AI_POLICY.md) limits where knowledge about the newer versions may come from.

## Summary

- AutoMapper 15, 16 and 17 are mostly about licensing, packaging and supported frameworks. The functional difference from 14.0.0 is small: 5 capabilities, 4 bug fixes and a series of security fixes around recursion.
- All 4 bugs were reproduced in this fork. All 5 capabilities were missing.
- The two security advisories of November 2026 (GHSA-6vhm-j9h7-f9gh and GHSA-gpwp-5797-mjff) do not crash this fork: the stack guard added for CVE-2026-32933 stopped every one of the routes they describe. Two things they describe are real here as functional defects: `MaxDepth` is ignored through constructor parameters, and derived maps do not inherit it.
- Licence keys, the logger factory that reports on them, and the package's dependency on a JWT library have no counterpart here and need none.
- Target frameworks, package signing and a software bill of materials are decisions or tasks for a maintainer, not code an AI tool can settle.

## How the comparison was made

The newer AutoMapper versions are under a different licence. Nothing from them may be copied here, and their code may not be given to an AI tool. The comparison therefore used public descriptions only:

| Source | Used for |
|---|---|
| Release notes on the GitHub releases page of the upstream repository, 15.0.0 to 17.0.0 | the list of changes per release |
| The 15.0 and 17.0 upgrade guides, the table of contents and the section headings of the upstream documentation | what a user must change; which topics are documented now |
| The author's release announcements, 15.0 to 17.0 | the intent behind a release |
| The package page on nuget.org | versions, dates, target frameworks, dependencies |
| The text of bug reports and feature requests (#4558, #4562, #4563, #4567, #4593, #4597, #4650) and the conversation page of #4596 and #4655 | the symptom and a reproduction |
| GitHub advisories GHSA-rvv3-g6hj-g44x, GHSA-6vhm-j9h7-f9gh, GHSA-gpwp-5797-mjff | the vulnerable scenarios |

Not opened, by rule: any source file, test, commit or "Files changed" view of the upstream repository after the fork point. The package was not decompiled. Web pages were read by separate agents that reported facts in their own words; the agents that wrote code received only the specifications in this document and this repository, and had no web access for the task.

Every gap below was then checked against this repository's code with a probe program (built in the gitignored `artifacts/gap-probe`, not part of the repository): one scenario per run, each deep-recursion scenario in its own process.

The API in this document is designed here, from this code base's conventions. Where a name matches an upstream name, it is because both follow the same 14.0.0 conventions; no upstream signature was looked up.

## What changed upstream after 14.0.0

| Release | Date | Functional changes | Everything else |
|---|---|---|---|
| 15.0.0, 15.0.1 | 2025-07 | none | Dual licence (RPL 1.5 or commercial), licence key, `MapperConfiguration` requires a logger factory, .NET Standard 2.0 target, strong-name signing removed |
| 15.1.0 | 2025-10 | An error while a mapping plan is built names the map and the member | .NET Framework target, dependency bump for a JWT library advisory |
| 16.0.0 | 2025-12 | none | .NET 10 target, signed packages |
| 16.1.0 | 2026-03 | Conditions and preconditions that use dependency injection; destination factories that use dependency injection; polymorphic LINQ projection can be switched off. Fixes: nullable conversion with `MapAtRuntime`, runs of capital letters in the PascalCase naming convention, order of open generic map registration | Perpetual licences, `.slnx` |
| 16.1.1, 15.1.3 | 2026-03 | Security: default `MaxDepth` of 64 for self-referential types (CVE-2026-32933) | Licence validation no longer blocks a thread |
| 16.2.0 | 2026-07 | none | Licence key from an environment variable, SBOM attached to releases |
| 16.3.0, 15.1.4 | 2026-11 | Security: `MaxDepth` bypasses (two advisories); a condition on `ForAllMembers` receives the source value; fix for a `Context.Items` exception that depended on which map was used first | Licence key grace period and clearer licence messages |
| 17.0.0 | 2026-11 | none | Targets .NET 10, .NET 11, .NET Standard 2.0 and .NET Framework 4.7.1; the .NET 8 and .NET 9 builds are removed |

## The gaps

"Verified" says what the probe showed on this repository before any change.

### Capabilities this fork lacked

| Id | Capability | Upstream | Verified here | Outcome |
|---|---|---|---|---|
| F1 | An error while a mapping plan is built says which map and member | 15.1.0 | A bare `ArgumentException` from `System.Linq.Expressions` escapes the `MapperConfiguration` constructor, with no map or member in it | Feature |
| F2 | Polymorphic projection can be switched off | 16.1.0 | `ProjectTo` always emits the type tests for included derived maps | Feature |
| F3 | Conditions and preconditions as classes created through the service constructor | 16.1.0 | Only lambdas exist | Feature |
| F4 | Destination factories as classes created through the service constructor | 16.1.0 | Only lambdas and `ConstructUsingServiceLocator` exist | Feature |
| F5 | A condition that declares the source member as `object` sees the source value | 16.3.0 | `ForAllMembers(o => o.Condition((s, d, m) => m != null))` with `int?` to `int`: the condition sees boxed `0`, the destination is overwritten | Feature, behaviour change |

### Bugs this fork shares with 14.0.0

| Id | Bug | Fixed upstream | Verified here | Outcome |
|---|---|---|---|---|
| B1 | `MapAtRuntime` on a member fed by an extension method call into a nullable value type | 16.1.0 | `ArgumentException` from the `MapperConfiguration` constructor; without `MapAtRuntime` the map works | Fix |
| B2 | PascalCase destination member with a run of capitals is not matched to a lower_underscore source member | 16.1.0 | `FormXML` is not matched to `form_xml`; the name is split into `Form`, `X`, `M`, `L` | Fix |
| B3 | An open generic map registered after a closed map that includes it as its base | 16.1.0 | `NullReferenceException` from the `MapperConfiguration` constructor; the other order works | Fix |
| B4 | "Context.Items are only available..." from a map that does not use them, depending on which map ran first | 16.3.0 | Reproduced with an explicit `MaxDepth`; without it both orders work | Fix |

### Recursion and `MaxDepth`

| Id | Item | Upstream | Verified here | Outcome |
|---|---|---|---|---|
| S1 | A deep graph crashes the process through routes that `MaxDepth` does not cover: constructor parameters, positional records, derived maps, `object` and interface members, nested dictionaries and `ExpandoObject`, `ConvertUsing` and `ConstructUsing` that map through the context | 15.1.4, 16.3.0 | Not affected. 100,000 levels on a 256 KB stack end in `SourceGraphTooDeepException` on all 10 routes tried | Regression tests only |
| M1 | `MaxDepth` is ignored when the next level goes through a constructor parameter | 15.1.4, 16.3.0 | `MaxDepth(10)`: 10 levels through a property, 100 of 100 through a constructor or a positional record | Fix, behaviour change |
| M2 | Derived maps do not inherit `MaxDepth` | 15.1.4, 16.3.0 | `MaxDepth(10)` on the base map, chain of derived objects: 100 of 100, with `Include` and with `IncludeBase` | Fix, behaviour change |
| M3 | Default `MaxDepth` of 64 for self-referential types | 15.1.3, 16.1.1 | This fork has no default limit, by decision: see [CVE-2026-32933](../security/CVE-2026-32933.md) | Not adopted |

### Not applicable

| Item | Why |
|---|---|
| Licence key, environment variables for it, grace period, perpetual licences, licence log messages | This fork is MIT-licensed and has no keys |
| `MapperConfiguration` requires `ILoggerFactory` | Upstream uses it to report on the licence. Here the constructor is unchanged, which is also what compatibility with 14 requires |
| Dependency on `Microsoft.IdentityModel.JsonWebTokens` and the advisories against it | Used for licence keys only. This fork depends on `Microsoft.Extensions.Options` and nothing else |
| Strong-name signing removed | This fork signs with its own key, by a recorded decision |

### For a maintainer to decide or do

| Item | Upstream | Here today | Note |
|---|---|---|---|
| Target frameworks | 17.0.0: `net10.0`, `net11.0`, `netstandard2.0`, `net471` | `net8.0` | Already open in [ROADMAP.md](../../ROADMAP.md). .NET 8 and .NET 9 reached the end of support on 2026-11-10. The `net8.0` build runs on every later .NET, so nothing breaks today; .NET Framework and .NET Standard would need the code that 14.0.0 had already dropped |
| Signed packages | since 16.0.0 | not signed | Needs a code-signing certificate |
| SBOM attached to releases | since 16.2.0 | none | A step in the release workflow |
| API comparison with the previous release | SDK package validation | planned in the roadmap | `EnablePackageValidation` with the last release as the baseline would cover it |
| Optional diagnostics through `Microsoft.Extensions.Logging` | exists for licence messages only | none | Would add a dependency. No user need is known |

## The features

Each one is our own design. "Changes behaviour" marks what an existing application could notice; everything else is additive.

### F1. Plan errors name the map and the member

**Need.** In an application with hundreds of maps, a failure while a plan is built gives an exception from the expression tree library and nothing to find the map by.

**Design.** A failure that is not already one of the library's own exceptions surfaces as an `AutoMapperMappingException` that carries the `TypeMap` and, when it happened inside one member, the `MemberMap` (a property, a `ForPath` path or a constructor parameter), with the original exception as the inner one. It covers plans built when the configuration is created, plans built on first use, nested maps and LINQ projection. Exceptions the library throws on purpose keep their type and message. No public API is added.

**Changes behaviour.** Code that caught the bare framework exception from the `MapperConfiguration` constructor or from the first `Map` call now gets an `AutoMapperMappingException` wrapping it.

### F2. A switch for polymorphic projection

**Need.** With included derived maps, `ProjectTo<BaseDto>()` tests the type of every row and builds the derived destination. A query that needs only the base columns then drags every derived column or table into the SQL, and some LINQ providers cannot translate a type test.

**Design.**

```csharp
cfg.Internal().PolymorphicProjection = false;                  // every map
cfg.CreateMap<Animal, AnimalDto>().Include<Cat, CatDto>()
   .PolymorphicProjection(false);                              // one map; its own setting wins over the global one
```

Off means: that map projects every row as its own destination type, with no type test. The setting of the map being expanded counts, for the root of a query and for a member alike. It is not inherited. An abstract destination type with the switch off is an error when the projection is built. In-memory `Map` is not affected. Default: on, as today.

### F3. Conditions and preconditions as classes

**Need.** A lambda condition in a profile cannot use a service and cannot be reused across maps. Resolvers, converters and mapping actions already have a class form.

**Design.**

```csharp
public interface IMemberCondition<in TSource, in TDestination, in TMember>
{
    bool ShouldMap(TSource source, TDestination destination, TMember sourceMember, TMember destinationMember, ResolutionContext context);
}
public interface IMemberPreCondition<in TSource, in TDestination>
{
    bool ShouldMap(TSource source, TDestination destination, ResolutionContext context);
}

opt.Condition<TCondition>();        opt.PreCondition<TPreCondition>();        // generic member options
opt.Condition(Type conditionType);  opt.PreCondition(Type preConditionType);  // non-generic member options
```

The instance comes from the service constructor on every evaluation, like a class-based value resolver, and `AddAutoMapper` registers the implementations it finds as transient. Arguments are converted to the type arguments the class declares, so one class written against `object` serves every member.

### F4. Destination factories as classes

**Need.** `ConstructUsing` takes a lambda, which cannot use a service; `ConstructUsingServiceLocator` needs every destination type registered in the container.

**Design.**

```csharp
public interface IDestinationFactory<in TSource, out TDestination>
{
    TDestination Create(TSource source, ResolutionContext context);
}

cfg.CreateMap<Order, OrderDto>().ConstructUsing<OrderDtoFactory>();
cfg.CreateMap(typeof(Order), typeof(OrderDto)).ConstructUsing(typeof(OrderDtoFactory));
```

Used exactly where the `Func` overload of `ConstructUsing` is used, created through the service constructor each time a destination is needed, registered by `AddAutoMapper`. Not used by `ProjectTo`.

### F5. What a condition sees

**Need.** The "skip nulls" condition on `ForAllMembers` does not skip a null that goes into a non-nullable value type, because the condition is handed the converted value.

**Design.** With P the type the condition declares for the source member, D the destination member's type and S the type of the resolved source value: when P is not D and S fits P without conversion, the condition gets the resolved source value. That is `ForAllMembers`, the non-generic API, and class-based conditions written against `object` or a base type. A typed `ForMember` condition is unchanged, because there P is D.

**Changes behaviour.** A `ForAllMembers` condition whose source and destination member types differ now sees `null` instead of `0` or `false`, the string instead of the parsed number, an `int` instead of the `long` it becomes. A condition that tested the converted type has to be rewritten. This is the one change in this set that silently alters what existing code observes; it is isolated in one place and can be taken out without touching the rest.

### B1 to B4

Fixed at their cause, each starting from the reproduction in the table above. B2 is additive by design: the public `PascalCaseNamingConvention.Split` returns what it returned, every name that was matched is still matched to the same member, and a run of capitals read as one word is an additional attempt.

### M1, M2 and S1

`MaxDepth` now also applies when the next level is passed through a constructor parameter, and a derived map takes the `MaxDepth` of the map it includes unless it sets its own. Depth stays counted per type map.

**Changes behaviour.** A graph that was mapped past the configured depth through a constructor or through derived maps is now cut off at it, which is what the setting says.

S1 adds a regression test for each route of the two November advisories. The stack guard is what protects the process; `MaxDepth` is a functional limit.

## How the work was delegated

| Package | Content | Main files |
|---|---|---|
| A | F1 | `TypeMap.cs`, `TypeMapPlanBuilder.cs`, `ProjectionBuilder.cs` |
| B | F3, F5 | `IMemberConfigurationExpression.cs`, `MemberConfigurationExpression.cs`, `TypeMapPlanBuilder.cs`, `ServiceCollectionExtensions.cs` |
| C | F4 | `IMappingExpressionBase.cs`, `TypeMapConfiguration.cs`, `TypeMap.cs`, `ServiceCollectionExtensions.cs` |
| D | F2 | `ProjectionBuilder.cs`, `InternalApi.cs`, `TypeMap.cs` |
| E | B1, B2 | `ExpressionBuilder.cs`, `Conventions.cs`, `INamingConvention.cs` |
| F | B3 | `MapperConfiguration.cs`, `ProfileMap.cs` |
| G | B4, M1, M2, S1 | `TypeMapPlanBuilder.cs`, `ExpressionBuilder.cs`, `TypeMap.cs`, `DeeplyNestedSourceGraphs.cs` |

Seven agents, one per package, each in its own git worktree, with the same rules: clean room, no change to an existing test, nothing new on the hot path or in the allocations of a configuration, tests and documentation for its own package. The results were merged, tested together, measured against the last release with `Benchmark.ps1`, and reviewed by an agent that had not written any of it.

## Result

All packages were merged; what each one changed is in the "Unreleased" section of [CHANGELOG.md](../../CHANGELOG.md). The merge had one textual conflict (the list of interfaces `AddAutoMapper` scans for) and one test to adapt (a new test expected the bare exception that package A now wraps). No existing test was changed.

Measured on the merged tree on 2026-11-19, Windows, .NET 11 SDK, tests on .NET 10:

| Run | Passed | Failed | Skipped |
|---|---:|---:|---:|
| Unit tests, plans compiled | 1,513 | 0 | 0 |
| Unit tests, plans interpreted (`-p:DynamicCodeSupport=false`) | 1,486 | 0 | 27 |
| Dependency injection tests | 66 | 0 | 0 |
| Integration tests (EF Core, SQL Server LocalDB) | 129 | 0 | 0 |

Before the change the numbers were 1,192, 1,166 with 26 skipped, 45 and 128. Of the 321 new unit tests, 303 came with the packages, 9 use several features in one configuration (`src/UnitTests/NewFeaturesTogether.cs`) and 9 cover what the review found. The one new skip in the interpreted run is a known difference: an error thrown by a condition class arrives without the wrapper that names the member.

`Benchmark.ps1` against 14.1.0, three rounds, machine noise 1.6 %: no regression. Mapping benchmarks between -2.2 % and +3.7 % in time (limit +10 %) and 0.0 % in allocations; building a configuration allocates 0.4 % less. It was measured before the three fixes from the review, which touch error paths and the inheritance of `MaxDepth` when a map is sealed.

## What the review found

An agent that had written none of the code read the merged library changes. It found no crash and nothing on the mapping path or in the allocations of a configuration. Three defects in the new code were fixed before the changes were proposed, each with a test:

- A "Missing map" error for the elements of a collection member in `ProjectTo` had become an `AutoMapperMappingException`, while the same error for the types of the query itself had not. It is the library's own error with a framework type, and is passed on as it is again.
- A derived map did not inherit `MaxDepth` through a map in between in two of the six orders in which three maps can be registered: the map in between was still being sealed when it was asked.
- A collection passed through a constructor is now left empty on the last level of `MaxDepth`, as a collection in a property always was. That follows from counting a level before its destination is created; it was not tested and not documented.

One more consequence of the condition change was documented and pinned: a condition that skips a value that has not changed no longer finds an `int` equal to a `long`.

Four smaller points in the new code were left open and are in the roadmap.

## What was found and left

The agents reported defects that are outside what the comparison asked for and that 14.0.0 has as well: registration order for chained open generic maps, `MaxDepth` in projections with derived maps, the plan of a member mapped through the context being shared by maps with the same member types, flattening over three levels across two naming conventions, and others. They are listed in [ROADMAP.md](../../ROADMAP.md), phase 4, and none of them was touched.

Two plan errors stay without a map name because existing tests pin the bare exception, and the behaviour changes are waiting for a maintainer's decision; both are in the roadmap too.
