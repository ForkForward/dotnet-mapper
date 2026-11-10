# Moving from AutoMapper 14

ForkForward.Mapper is AutoMapper 14.0.0 under a new name. The types, their members and the behaviour are the same; what changes is where they live.

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

That is all. The code compiles and behaves as it did with AutoMapper 14.0.0.

## What stays as it was

- **Type names.** `IMapper`, `MapperConfiguration`, `Profile`, `AutoMapperMappingException`, `AutoMapperConfigurationException`, `AutoMapAttribute`, `AddAutoMapper` and every other type and member keep their names, including the ones that carry the old project's name.
- **Behaviour.** Conventions, configuration, validation, projection: unchanged. The only difference in behaviour is a security fix: a source graph nested deeper than the thread's stack allows now ends in an `AutoMapperMappingException` instead of a process crash ([CVE-2026-32933](https://github.com/ForkForward/dotnet-mapper/blob/master/docs/security/CVE-2026-32933.md)).
- **Documentation.** These pages are AutoMapper's, with the namespace updated.

## What else changes

- **Assembly identity.** The assembly is `ForkForward.Mapper.dll`, signed with the fork's own key (public key token `d1f0d2ab1c32618c`). A library compiled against `AutoMapper.dll` does not load against this assembly; it has to be recompiled against `ForkForward.Mapper`. Assembly binding redirects cannot bridge the two, as the names differ.
- **Both packages at once.** `AutoMapper` and `ForkForward.Mapper` can be referenced in the same project without a conflict, since nothing overlaps, but a type from one is not the type from the other: an `IMapper` registered by `AutoMapper` does not satisfy a constructor that asks for `ForkForward.Mapper.IMapper`. Move a project as a whole.
- **Version numbers** continue from 14: `ForkForward.Mapper 14.1.0` is the first stable release. The previews `14.1.0-preview.2` and `14.1.0-preview.3` still used the `AutoMapper` names.

## Moving from AutoMapper 15 or later

AutoMapper 15 and later are under a different licence and were never part of this fork; their new features are not here. Code written for 14 that was later moved to 15 may use API that 14 does not have; the [AutoMapper upgrade guides](https://docs.automapper.io) list what 15 added. Everything described on these pages is available.
