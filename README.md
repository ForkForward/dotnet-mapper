<p align="center">
  <img src="assets/brand/banner.png" alt="ForkForward.Mapper: community fork of AutoMapper, MIT, developed with AI tools" width="640">
</p>

# ForkForward.Mapper

**ForkForward.Mapper is a community fork of [AutoMapper](https://github.com/AutoMapper/AutoMapper.Archive)**, the convention-based object-object mapper for .NET created by [Jimmy Bogard](https://jimmybogard.com). It continues the last MIT-licensed code of AutoMapper and keeps it building on current .NET. Changes in this fork are written by AI coding tools and automation; people review them and are responsible for releases.

> [!IMPORTANT]
> This is **not** the official AutoMapper. ForkForward is an independent project. It is not affiliated with, sponsored by, or endorsed by Jimmy Bogard, Lucky Penny Software, or the AutoMapper project. The name "AutoMapper" appears here only to say where the code comes from.
> The official AutoMapper is at [automapper.io](https://automapper.io).

## Where the code comes from

| | |
|---|---|
| Original project | AutoMapper, by Jimmy Bogard, Lucian Bargaoanu and the AutoMapper contributors |
| Source repository | [AutoMapper/AutoMapper.Archive](https://github.com/AutoMapper/AutoMapper.Archive) (archived by its owners) |
| Forked from | commit [`92816585`](https://github.com/AutoMapper/AutoMapper.Archive/commit/928165859deaf9a59c4433771b3c58a9536284dd) of 2025-06-20, which is release `v14.0.0` plus 3 commits |
| Licence of the original code | MIT, Copyright (c) 2010 Jimmy Bogard |
| Licence of this fork | MIT, unchanged ([LICENSE.txt](LICENSE.txt)) |
| What this fork changed | `git log 92816585..HEAD`, summarised in [NOTICE.md](NOTICE.md) |

All the design, the API and nearly all of the code are the work of the original authors. This fork adds maintenance on top.

## Why this fork exists

The official AutoMapper continues at [LuckyPennySoftware/AutoMapper](https://github.com/LuckyPennySoftware/AutoMapper) under the Reciprocal Public License 1.5 or a commercial licence. The MIT-licensed code was archived at the commit above. The MIT licence lets anyone keep that code alive, and that is all this fork does: it keeps the MIT line compiling, tested and compatible as .NET moves on.

If you want new features from the original authors, or commercial support, use the official AutoMapper.

## How this fork is developed

- Changes are produced with AI coding tools and automation such as Dependabot.
- A person reviews every change before it is merged and decides what gets released.
- No code is taken from AutoMapper releases published after the fork point, or from any source whose licence does not allow it.
- Every change to the library is measured against the last release before it is merged, and again before it is published: it must not be slower and must not allocate more memory.

The full rules are in [AI_POLICY.md](AI_POLICY.md).

## Status

- The first stable release, `14.1.0`, is on nuget.org: [`ForkForward.Mapper`](https://www.nuget.org/packages/ForkForward.Mapper). What changed in each release is in [CHANGELOG.md](CHANGELOG.md); the plan is in [ROADMAP.md](ROADMAP.md).

  ```
  dotnet add package ForkForward.Mapper
  ```

- The assembly is `ForkForward.Mapper.dll` and the namespaces start with `ForkForward.Mapper`; the types, their members and the behaviour are those of AutoMapper 14.0.0. Moving a project is a package reference and a `using` directive: [Moving from AutoMapper 14](docs/source/Moving-from-AutoMapper-14.md). Type names that carry the old name, such as `AutoMapperMappingException` and `AddAutoMapper`, are unchanged.
- Native AOT and trimming are not supported, and from the next release the build warns when a trimmed or Native AOT application creates a mapper configuration. What happens if you try is in [Native AOT and trimming](docs/source/Native-AOT-and-trimming.md).
- Security fixes are written here independently, from the public advisory and this code. Fixed so far: [CVE-2026-32933](docs/security/CVE-2026-32933.md), a source graph nested deeper than the thread's stack allows crashed the process; it now ends in an `AutoMapperMappingException`. Vulnerability scanners know this package as `ForkForward.Mapper`, not `AutoMapper`, so check that page rather than the official advisory for what is fixed here.

## Using it

Configure the mapper once at startup:

```csharp
using ForkForward.Mapper;

var configuration = new MapperConfiguration(cfg =>
{
    cfg.CreateMap<Foo, FooDto>();
    cfg.CreateMap<Bar, BarDto>();
});
// only during development, validate your mappings; remove it before release
#if DEBUG
configuration.AssertConfigurationIsValid();
#endif
var mapper = configuration.CreateMapper();
```

Then map:

```csharp
var fooDto = mapper.Map<FooDto>(foo);
var barDto = mapper.Map<BarDto>(bar);
```

The documentation inherited from AutoMapper is published at [forkforward.dev/dotnet-mapper](https://forkforward.dev/dotnet-mapper/). Its source is in [docs/source](docs/source), starting with [Getting started](docs/source/Getting-started.md). It still describes this code, because the API has not changed.

## Building

The project builds and is tested on Windows and on Linux. You need the .NET 10 SDK. The library targets `net8.0`; the tests target `net10.0`.

```powershell
./Build.ps1
```

This builds the solution, runs all tests and packs `ForkForward.Mapper`. It runs under PowerShell on both systems.

All build output goes to `artifacts/` instead of `bin` and `obj` folders next to each project: the package to `artifacts/package/release`, test results to `artifacts/TestResults`.

The integration tests need a SQL Server. On Windows they use LocalDB. Anywhere else, point them at a server with `FORKFORWARD_TEST_SQLSERVER`; see [CONTRIBUTING.md](CONTRIBUTING.md#building).

## Dependency injection

Support for `Microsoft.Extensions.DependencyInjection` is built in: call `services.AddAutoMapper(...)`. There is no separate extension package.

If your project references `AutoMapper.Extensions.Microsoft.DependencyInjection`, remove that reference together with the reference to `AutoMapper`, add `ForkForward.Mapper`, and change the `using`. Your `AddAutoMapper` calls stay as they are.

## Reporting problems and contributing

Open an issue in this repository for bugs in this fork. Do not report them to the original AutoMapper project; its maintainers are not responsible for this code. See [CONTRIBUTING.md](CONTRIBUTING.md).

## About the logo

The red node is the origin: it uses the red of the AutoMapper logo. The two arrows are the code moving forward from there. The mark is new artwork; the original AutoMapper logo is not used.

## Licence and credits

ForkForward.Mapper is released under the [MIT licence](LICENSE.txt), the same licence the original code was published under.

AutoMapper is Copyright (c) 2010 Jimmy Bogard and other contributors. Thank you to them for building it and for publishing it as open source. Modifications made in this fork are Copyright (c) 2026 ForkForward contributors and are released under the same MIT terms. Details are in [NOTICE.md](NOTICE.md).
