# Native AOT and trimming

ForkForward.Mapper does not support Native AOT, and it does not support trimming of the library or of the assemblies that hold the mapped types. This page says why, how the build tells you, and what was measured when it was tried anyway.

!!! info "Written for this fork"

    This page is not inherited from AutoMapper. The measurements were made on 2026-11-13 with the code of this repository, .NET 11.0.0, on Windows x64. Other runtimes and platforms were not measured.

An application that is neither trimmed nor compiled ahead of time is not affected by anything on this page.

## Why

The library works at run time with things that trimming and ahead-of-time compilation need to know at build time:

- A mapping plan is an expression tree that is compiled when the configuration is created. Without dynamic code it cannot be compiled, so it is interpreted.
- The members of the mapped types, methods of the base class library and methods of the library itself are found through reflection, many of them by name. Trimming removes members that nothing refers to in code.
- Generic types and methods are closed at run time. Under Native AOT the code for such an instantiation may not exist.
- Interface proxies and the types projections build for `let` clauses are generated with Reflection.Emit, which needs dynamic code.

## What the build tells you

From the release that follows 14.1.1, the constructors of `MapperConfiguration` and every `AddAutoMapper` overload carry the `RequiresUnreferencedCode` and `RequiresDynamicCode` attributes. A project that is published trimmed or with Native AOT, or that switches the trim or AOT analyzers on, gets the warnings `IL2026` and `IL3050` at the line that creates the configuration, with a link to this page. Before that, such an application built without a word about it and failed when it started.

Projects that do not use those analyzers see nothing. A default Blazor WebAssembly application, which trims only the framework, was checked: it builds and publishes without these warnings.

## What was measured

A probe application with 25 mapping scenarios: flat and nested objects, conversions between primitive types and enums, flattening, collections, dictionaries, records, resolvers and converters, structs, inheritance, open generics, interface proxies, `ProjectTo`, configuration validation, and `AddAutoMapper` with assembly scanning.

| How the application was built | Scenarios that worked |
|---|---|
| Ordinary build, plans compiled | 25 of 25 |
| Ordinary build with dynamic code switched off, so plans are interpreted; nothing trimmed | 24 of 25. Interface proxies need Reflection.Emit |
| Native AOT, nothing else | 0 of 25. Creating the configuration throws a `TypeInitializationException`: methods of the library that it looks up by name were removed |
| Native AOT, the library kept whole | 1 of 25. Constructors and setters of the mapped types were removed. Two scenarios returned an object whose members had not been mapped, without any exception |
| Native AOT, the library and the assembly with the mapped types kept whole | 16 of 25 |

In the last row these did not work: collections as members and at the top level (`List<T>`, `HashSet<T>`, `Dictionary<TKey, TValue>`), string to enum, `ProjectTo`, and interface proxies. All but the proxies failed because a member of the base class library that the mapper looks up by name had been removed.

A map of two members took about 450 ns and allocated 680 bytes under Native AOT, against about 100 ns and 32 bytes with a compiled plan on the same machine: the plan is interpreted.

## If you try it anyway

This is not supported, and the table above is a measurement, not a promise. What made the last row possible is keeping both assemblies whole:

```xml
<ItemGroup>
  <TrimmerRootAssembly Include="ForkForward.Mapper" />
  <TrimmerRootAssembly Include="The.Assembly.With.Your.Mapped.Types" />
</ItemGroup>
```

Test every map in the published application, not in a debug build. A member that was trimmed away is not always reported: the mapper can treat it as a member that does not exist and leave the destination value unset.

Do not map input of untrusted depth: see the next section.

## Deeply nested graphs under Native AOT

The protection against [CVE-2026-32933](https://github.com/ForkForward/dotnet-mapper/blob/master/docs/security/CVE-2026-32933.md) does not hold under Native AOT. A source graph nested deeper than the stack allows ends the process there, as it did before the fix. A chain of 400 objects mapped on a 1 MB stack; a chain of 1,000 ended the process with a stack overflow. The cause has not been found yet.

`MaxDepth` on the type map helps only when it is small enough for the smallest stack the application maps on: with `MaxDepth(50)` a chain of 100,000 objects was cut off safely on a 256 KB stack, with `MaxDepth(300)` the process ended.

## Runtimes without dynamic code, nothing trimmed

Where the runtime cannot generate code but nothing is trimmed, the library mostly works: its unit tests are run this way on every change (`dotnet test src/UnitTests -p:DynamicCodeSupport=false`), and 1,166 of 1,192 pass. The 26 that are skipped in that run are the known differences:

- **Interface proxies and `let` clauses in projections** (20 tests) throw a `PlatformNotSupportedException`, wrapped in a `TypeInitializationException`: they generate a type with Reflection.Emit.
- **An error from a member that maps through the context is not wrapped** (4 tests). A nested map, a collection of mapped elements, or a resolver or converter that is given the context: when one of these fails, the exception arrives as it was thrown, without the `AutoMapperMappingException` that names the member. Errors from plain members are wrapped as usual. The catch for these members has an exception filter, and the expression interpreter fails while it evaluates that filter.
- **`ForPath` through fields of nested value types does not set the values** (1 test). Not investigated yet.
- **A level of a nested graph needs more stack** (1 test). On a 1 MB stack a chain of 400 objects no longer fits, where a compiled plan maps about 1,000. On CoreCLR the result is still the `AutoMapperMappingException` that says the graph is nested too deeply, not a crash.

Two more things to know:

- Plans are interpreted, so mapping is several times slower and allocates more.
- `Microsoft.Extensions.DependencyInjection` refuses to create an open generic service for a value type when there is no dynamic code. A type converter such as `MyConverter<T>` resolved from the container for an enum or a struct fails with its `InvalidOperationException`; that check is the container's, not the mapper's.

Before the release that follows 14.1.1, every member mapping error in this mode surfaced as `NotSupportedException: Specified method is not supported` instead of an `AutoMapperMappingException`. That is fixed; the bug was inherited from AutoMapper 14.

## Will it be supported?

Not decided. Making trimmed and Native AOT applications work would mean annotating the public API so that the mapped types survive trimming, keeping the library's own reflection targets, and finding the stack overflow described above; plans would still be interpreted. It is listed as an open decision in the [roadmap](https://github.com/ForkForward/dotnet-mapper/blob/master/ROADMAP.md). If you need it, say so in an [issue](https://github.com/ForkForward/dotnet-mapper/issues).
