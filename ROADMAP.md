# Roadmap

Where ForkForward.Mapper is going, and the decisions that are still open. Open decisions are marked **Decision needed**; nothing depending on them is done until a maintainer has recorded the answer here.

## Phase 0: identity and housekeeping

- [x] State clearly that this is a fork of AutoMapper, who wrote the original, and under what licence ([README.md](README.md), [NOTICE.md](NOTICE.md))
- [x] Own name and logo; remove claims that applied only to the original project
- [x] Rules for AI-assisted development ([AI_POLICY.md](AI_POLICY.md))
- [x] Issue forms, pull request template, code owners, label set
- [x] Tests compile and pass with the .NET 10 SDK
- [x] CI back on Windows with SQL Server LocalDB, with its SDKs installed explicitly
- [ ] CI green on GitHub (the workflow has not run there yet)
- [ ] Repository settings: description, topics `forkforward`, `dotnet`, `object-mapping`, issues enabled, labels applied

## Phase 1: keep it building

- [ ] Work through the Dependabot updates
- [ ] Make the API comparison a hard gate: fail the build on any difference from AutoMapper 14.0.0 that is not already in `ApiCompatBaseline.txt`
- [ ] Decide which .NET versions the library and the tests target, and move the tests to a supported runtime
- [ ] Review the inherited documentation for references to infrastructure that the fork does not have (the MyGet feed, Gitter)

## Phase 2: first release of `ForkForward.Mapper`

The package cannot be published until these are decided.

1. **Decision needed: namespace and assembly name.**
   Today both are still `AutoMapper`. Keeping them means existing code and third-party libraries built against AutoMapper 14 keep working, and switching is a one-line change of the package reference. Renaming them makes the fork impossible to confuse with the original, and forces every user to edit their code. Current state: kept.
2. **Decision needed: strong-name key.**
   The assembly is signed with the `AutoMapper.snk` key from the original repository, which gives it the same assembly identity as the original. A new key gives the fork its own identity and breaks binary compatibility with libraries compiled against the original. This follows from decision 1.
3. **Decision needed: version numbers.**
   Continue from 14.x, which signals compatibility with AutoMapper 14, or start again at 1.0 and document "based on AutoMapper 14.0.0". Today MinVer computes `14.0.1-preview` from the inherited `v14.0.0` tag.
4. Reserve the `ForkForward` prefix on nuget.org and set up publishing from the release workflow. Publishing is switched off until then.
5. Write a NuGet-specific readme and a short guide: "moving from AutoMapper 14 to ForkForward.Mapper".

## Phase 3: beyond the first release

- [ ] Publish the documentation
- [ ] Mark [dotnet-mapper-extensions](https://github.com/ForkForward/dotnet-mapper-extensions) as historical: dependency injection support has been part of the main library since before the fork point (`AddAutoMapper` lives in `src/AutoMapper/ServiceCollectionExtensions.cs`)
- [ ] Organisation profile with a catalogue of ForkForward projects by technology
