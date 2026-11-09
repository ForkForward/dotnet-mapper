# Contributing to ForkForward.Mapper

ForkForward.Mapper is a community fork of AutoMapper. It is maintained with AI coding tools, and reviewed and released by people. Read [AI_POLICY.md](AI_POLICY.md) first; it is short and it is the part that differs from other projects.

## What helps most

- **A bug report with a reproduction.** Source and destination types, the mapping configuration, the call that fails, and what you expected. A failing test is ideal.
- **A compatibility report.** If something that worked with AutoMapper 14.0.0 behaves differently here, that is a bug in this fork.
- **A report that the build or the tests fail** on a current .NET SDK or operating system.

Report bugs in this fork here, in [ForkForward/dotnet-mapper](https://github.com/ForkForward/dotnet-mapper/issues). The original AutoMapper maintainers are not responsible for this code, so please do not send them issues about it.

## Pull requests

- Pull requests go to the `master` branch.
- The change is produced with an AI coding tool, and the pull request says which one and what you checked yourself. The template asks for this.
- Every bug fix and feature comes with a test.
- The public API stays compatible with AutoMapper 14.0.0. If your change modifies `src/AutoMapper/ApiCompatBaseline.txt`, say why in the pull request.
- Discuss larger changes in an issue before you start.

## Where code may come from

Do not copy anything from AutoMapper releases published after this fork was taken, or from the `LuckyPennySoftware/AutoMapper` repository. That code is under a different licence and cannot be used here, in any form: no code, tests, documentation, or ports of them. A fix that also exists there has to be written independently. The full rule is in [AI_POLICY.md](AI_POLICY.md#4-where-code-may-come-from).

By opening a pull request you confirm that you have the right to submit the change and that it can be released under the MIT licence.

## Building

You need the .NET SDK 9 or newer and the .NET 9 runtime. The integration tests need SQL Server LocalDB, which exists only on Windows; `Setup.ps1` installs it.

```powershell
./Build.ps1
```

This builds the solution, runs all tests and packs the library into `artifacts/`.

To run only the tests that need no database:

```powershell
dotnet test src/UnitTests
dotnet test src/AutoMapper.DI.Tests
```

## Commit messages

A short summary line, then what changed and why. Reference the issue number. Commits written by an AI tool carry a `Co-Authored-By` trailer naming the tool.
