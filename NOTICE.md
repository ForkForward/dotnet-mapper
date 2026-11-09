# NOTICE

## ForkForward.Mapper

ForkForward.Mapper is a fork of AutoMapper. This file records where the code comes from, who owns what, and what the fork has changed. It ships in the source repository and inside the NuGet package.

## Original work

- **Project:** AutoMapper, a convention-based object-object mapper for .NET
- **Authors:** Jimmy Bogard, Lucian Bargaoanu and the AutoMapper contributors (the full list is in the git history of this repository)
- **Copyright:** Copyright (c) 2010 Jimmy Bogard
- **Licence:** MIT. The original licence text is kept unchanged in [LICENSE.txt](LICENSE.txt).
- **Source:** <https://github.com/AutoMapper/AutoMapper.Archive>
- **Fork point:** commit `928165859deaf9a59c4433771b3c58a9536284dd` (2025-06-20), which is tag `v14.0.0` plus 3 commits
- **Official project today:** <https://automapper.io> and <https://github.com/LuckyPennySoftware/AutoMapper>

The complete commit history of the original project up to the fork point is preserved in this repository.

## This fork

- **Project:** ForkForward.Mapper, <https://github.com/ForkForward/dotnet-mapper>
- **Copyright in the modifications:** Copyright (c) 2026 ForkForward contributors
- **Licence of the modifications:** MIT, the same terms as the original work

Changes in this fork are written with AI coding tools and automation and are reviewed by people. See [AI_POLICY.md](AI_POLICY.md).

## No affiliation

ForkForward is an independent project. It is not affiliated with, sponsored by, or endorsed by Jimmy Bogard, Lucky Penny Software, the AutoMapper project or the .NET Foundation.

"AutoMapper" is the name of the original project and belongs to its owners. This fork uses the name only to identify the origin of the code and to describe compatibility. The `AutoMapper` namespace and assembly name are kept in the code for source compatibility. The fork is distributed under its own name, `ForkForward.Mapper`, and with its own logo.

## Code that must not enter this fork

Nothing may be copied into this fork from AutoMapper releases published after the fork point, or from the `LuckyPennySoftware/AutoMapper` repository. That code is under a different licence (Reciprocal Public License 1.5 or a commercial licence).

## What the fork has changed

The authoritative list is the git history: `git log 92816585..HEAD`.

Summary by area:

- **Identity:** package renamed to `ForkForward.Mapper`; new logo; README, contribution guide and documentation headers rewritten to state the origin of the code.
- **Removed claims that do not apply to the fork:** .NET Foundation support, the Contributor License Agreement, the original authors' sponsorship links, the original package feeds.
- **Build:** continuous integration runs on Windows with SQL Server LocalDB again and installs its .NET SDKs explicitly; the tests compile with the .NET 10 SDK.
- **Maintenance:** Dependabot configuration for dependency updates.

No public API of the library has been changed.
