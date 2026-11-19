# ForkForward.Mapper

A community fork of [AutoMapper](https://github.com/AutoMapper/AutoMapper.Archive), the convention-based object-object mapper for .NET, continuing the last MIT-licensed code (14.0.0) under the MIT licence. Independent project: not affiliated with, sponsored by, or endorsed by the original authors. The official AutoMapper is at [automapper.io](https://automapper.io).

```csharp
using ForkForward.Mapper;

var configuration = new MapperConfiguration(cfg =>
{
    cfg.CreateMap<Order, OrderDto>();
});
var mapper = configuration.CreateMapper();
var dto = mapper.Map<OrderDto>(order);
```

## Moving from AutoMapper 14

1. Replace the `AutoMapper` package reference with `ForkForward.Mapper`.
2. Replace `using AutoMapper` with `using ForkForward.Mapper` (same for `AutoMapper.QueryableExtensions`, `AutoMapper.Internal`, and so on).

That is all: the types and their members are those of AutoMapper 14.0.0, and so is the behaviour, apart from the corrections the guide lists. Type names that carry the old name, such as `AutoMapperMappingException` and `AddAutoMapper`, are unchanged. Full guide: [Moving from AutoMapper 14](https://forkforward.dev/dotnet-mapper/Moving-from-AutoMapper-14/).

## What this fork adds

- Security fixes written independently from the public advisories; so far [CVE-2026-32933](https://github.com/ForkForward/dotnet-mapper/blob/master/docs/security/CVE-2026-32933.md). The two advisories of November 2026 about routes around `MaxDepth` [do not crash this library](https://github.com/ForkForward/dotnet-mapper/blob/master/docs/security/GHSA-6vhm-j9h7-f9gh-and-GHSA-gpwp-5797-mjff.md).
- From the release that follows 14.1.1: conditions, preconditions and destination factories as classes that take services from the container, a switch for polymorphic projection, plan errors that name the map and the member, and fixes for bugs inherited from AutoMapper 14. Four of the fixes change behaviour; they are listed in [Moving from AutoMapper 14](https://forkforward.dev/dotnet-mapper/Moving-from-AutoMapper-14/#what-behaves-differently-from-automapper-14).
- Builds and tests on current .NET SDKs, on Windows and Linux; every change is measured against the previous release and must not be slower or allocate more.

## Native AOT and trimming

Not supported. A project that is published trimmed or with Native AOT gets the warnings `IL2026` and `IL3050` where it creates a mapper configuration. Details: https://forkforward.dev/dotnet-mapper/Native-AOT-and-trimming/

## Links

- Documentation: https://forkforward.dev/dotnet-mapper/
- Source: https://github.com/ForkForward/dotnet-mapper
- Changelog: https://github.com/ForkForward/dotnet-mapper/blob/master/CHANGELOG.md
- Licence: MIT. Copyright (c) 2010 Jimmy Bogard and AutoMapper contributors; modifications copyright (c) 2026 ForkForward contributors. See NOTICE.md in the package.
