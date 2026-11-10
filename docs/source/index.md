# ForkForward.Mapper

A convention-based object-object mapper for .NET.

!!! info "A community fork of AutoMapper"

    ForkForward.Mapper is a community fork of [AutoMapper](https://automapper.io), continued from the last
    MIT-licensed AutoMapper code (14.0.0). This documentation was written by Jimmy Bogard and the AutoMapper
    contributors and is inherited from the original project. It still describes this code: the API has not
    changed, only the namespace, which is `ForkForward.Mapper` since 14.1.0 and has been updated in the samples.
    The pages keep calling the library AutoMapper where they describe its design; see
    [Moving from AutoMapper 14](Moving-from-AutoMapper-14.md) for what differs.

    ForkForward is an independent project and is not affiliated with or endorsed by the original authors.

AutoMapper uses a fluent configuration API to define an object-object mapping strategy. AutoMapper uses a
convention-based matching algorithm to match up source to destination values. AutoMapper is geared towards
model projection scenarios to flatten complex object models to DTOs and other simple objects, whose design is
better suited for serialization, communication, messaging, or simply an anti-corruption layer between the
domain and application layer.

New to AutoMapper? Check out the [Getting started](Getting-started.md) page first.

## Install

```
dotnet add package ForkForward.Mapper
```

## Examples

The source code contains unit tests for all of the features in these pages. Use the
[GitHub search](https://github.com/ForkForward/dotnet-mapper) to find relevant examples.

## Housekeeping

The releases are on [NuGet](https://www.nuget.org/packages/ForkForward.Mapper); what changed in each is in the
[changelog](https://github.com/ForkForward/dotnet-mapper/blob/master/CHANGELOG.md).

Security fixes made in this fork are listed in
[docs/security](https://github.com/ForkForward/dotnet-mapper/tree/master/docs/security). Vulnerability scanners
know this package as `ForkForward.Mapper`, not `AutoMapper`, so check there rather than the official advisory
for what is fixed here.

Questions and bug reports go to the [issues](https://github.com/ForkForward/dotnet-mapper/issues) of this fork.
