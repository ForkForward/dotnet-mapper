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

That is all: the types, their members and the behaviour are those of AutoMapper 14.0.0. Type names that carry the old name, such as `AutoMapperMappingException` and `AddAutoMapper`, are unchanged. Full guide: [Moving from AutoMapper 14](https://github.com/ForkForward/dotnet-mapper/blob/master/docs/source/Moving-from-AutoMapper-14.md).

## What this fork adds

- Security fixes written independently from the public advisories; so far [CVE-2026-32933](https://github.com/ForkForward/dotnet-mapper/blob/master/docs/security/CVE-2026-32933.md).
- Builds and tests on current .NET SDKs, on Windows and Linux; every change is measured against the previous release and must not be slower or allocate more.

## Links

- Source and documentation: https://github.com/ForkForward/dotnet-mapper
- Changelog: https://github.com/ForkForward/dotnet-mapper/blob/master/CHANGELOG.md
- Licence: MIT. Copyright (c) 2010 Jimmy Bogard and AutoMapper contributors; modifications copyright (c) 2026 ForkForward contributors. See NOTICE.md in the package.
