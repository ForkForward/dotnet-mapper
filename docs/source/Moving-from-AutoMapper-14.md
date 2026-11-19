# Moving from AutoMapper 14

ForkForward.Mapper is AutoMapper 14.0.0 under a new name. The types and their members are the same, and so is the behaviour, with a few corrections listed below; what changes is where they live.

## Two steps

1. **Package.** Remove the `AutoMapper` package reference and add `ForkForward.Mapper`:

   ```
   dotnet remove package AutoMapper
   dotnet add package ForkForward.Mapper
   ```

   If the project also references `AutoMapper.Extensions.Microsoft.DependencyInjection`, remove it too: `AddAutoMapper` is part of `ForkForward.Mapper`.

2. **Namespaces.** Replace `AutoMapper` with `ForkForward.Mapper` in `using` directives, including the sub-namespaces:

   | Before | After |
   |---|---|
   | `using AutoMapper;` | `using ForkForward.Mapper;` |
   | `using AutoMapper.QueryableExtensions;` | `using ForkForward.Mapper.QueryableExtensions;` |
   | `using AutoMapper.Configuration.Annotations;` | `using ForkForward.Mapper.Configuration.Annotations;` |
   | `using AutoMapper.Internal;` | `using ForkForward.Mapper.Internal;` |

   A global using (`<Using Include="ForkForward.Mapper" />` in the project file) does it once for a whole project. For a solution, a search and replace of `using AutoMapper` with `using ForkForward.Mapper` is safe: no other identifier starts that way.

That is all. The code compiles and behaves as it did with AutoMapper 14.0.0, apart from the corrections listed under [What behaves differently from AutoMapper 14](#what-behaves-differently-from-automapper-14).

## What stays as it was

- **Type names.** `IMapper`, `MapperConfiguration`, `Profile`, `AutoMapperMappingException`, `AutoMapperConfigurationException`, `AutoMapAttribute`, `AddAutoMapper` and every other type and member keep their names, including the ones that carry the old project's name.
- **Behaviour.** Conventions, configuration, validation, projection: unchanged in 14.1.0 and 14.1.1. The only difference in behaviour there is a security fix: a source graph nested deeper than the thread's stack allows now ends in an `AutoMapperMappingException` instead of a process crash ([CVE-2026-32933](https://github.com/ForkForward/dotnet-mapper/blob/master/docs/security/CVE-2026-32933.md)). The release that follows 14.1.1 changes four more things; see the next section.
- **Documentation.** These pages are AutoMapper's, with the namespace updated. Sections written for this fork say so.

## What behaves differently from AutoMapper 14

From the release that follows 14.1.1. Each of these is a correction of something AutoMapper 14 did, and each can be noticed by code that relied on the old behaviour:

| What | AutoMapper 14 | This fork | Where |
|---|---|---|---|
| The source member a condition gets when it declares it as `object`: `ForAllMembers`, `ForMember` with a member name, maps created from `Type` objects | The value after conversion to the destination member's type: `0` for a `null` in an `int?` that goes into an `int`, an empty collection for a `null` collection | The value as it was resolved: `null` | [What a condition sees](Conditional-mapping.md#what-a-condition-sees) |
| A plan that cannot be built | The bare `ArgumentException` or `InvalidOperationException` from the expression tree library | An `AutoMapperMappingException` that names the map and the member, with the original inside | [When a plan cannot be built](Understanding-your-mapping.md#when-a-plan-cannot-be-built) |
| `MaxDepth` on a map whose destination takes the next level through its constructor | Ignored: the whole graph is mapped | Applied | [`MaxDepth`](Configuration.md#maxdepth) |
| `MaxDepth` on a base map, objects of a derived type | Ignored for the derived map | Inherited by the derived map, unless it sets its own | [`MaxDepth`](Configuration.md#maxdepth) |

Fixed without a change for code that worked: a destination member such as `FormXML` is matched to `form_xml`, `MapAtRuntime` works on a member that goes into a nullable value type, an open generic map can be registered after the map that includes it, and a map with `MaxDepth` no longer makes another map fail with "Context.Items are only available...". The full list is in the [changelog](https://github.com/ForkForward/dotnet-mapper/blob/master/CHANGELOG.md).

## What this fork adds to AutoMapper 14

Also from the release that follows 14.1.1:

- [Conditions and preconditions as classes](Conditional-mapping.md#conditions-as-classes), which can take services from the dependency injection container.
- [Destination factory classes](Construction.md#creating-the-destination-with-a-factory-class), likewise.
- [A switch for polymorphic projection](Queryable-Extensions.md#polymorphic-projection).

## What else changes

- **Assembly identity.** The assembly is `ForkForward.Mapper.dll`, signed with the fork's own key (public key token `d1f0d2ab1c32618c`). A library compiled against `AutoMapper.dll` does not load against this assembly; it has to be recompiled against `ForkForward.Mapper`. Assembly binding redirects cannot bridge the two, as the names differ.
- **Both packages at once.** `AutoMapper` and `ForkForward.Mapper` can be referenced in the same project without a conflict, since nothing overlaps, but a type from one is not the type from the other: an `IMapper` registered by `AutoMapper` does not satisfy a constructor that asks for `ForkForward.Mapper.IMapper`. Move a project as a whole.
- **.NET version.** `14.1.0` and `14.1.1` target .NET 8, like AutoMapper 14, and run on every later .NET. From the release that follows 14.1.1 the package targets .NET 10 and .NET 11, the versions that are in support. An application on .NET 8 or .NET 9 cannot install that release: it stays on `14.1.1`, without the additions and fixes listed above, until it moves to .NET 10.
- **Version numbers** continue from 14: `ForkForward.Mapper 14.1.0` is the first stable release. The previews `14.1.0-preview.2` and `14.1.0-preview.3` still used the `AutoMapper` names.

## Moving from AutoMapper 15 or later

AutoMapper 15 and later are under a different licence and were never part of this fork; their code is not here. Code written for 14 that was later moved to 15 or later may use API that 14 does not have; the [AutoMapper upgrade guides](https://docs.automapper.io) list what changed. Everything described on these pages is available.

What those versions can do that 14 could not has a counterpart here, designed and written in this fork: the three additions above, and the corrections in the table. The names and signatures are this fork's own and may differ from AutoMapper's, so code that uses the newer AutoMapper API for them has to be adapted. There is no licence key to set, `MapperConfiguration` takes no logger factory, and there is no default `MaxDepth`: a graph too deep for the stack ends in an exception without one.
