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

The project builds and is tested on Windows and on Linux; CI runs both. You need the .NET 10 SDK. The solution file is `AutoMapper.slnx`. The library targets `net8.0`; the tests, the benchmark and the sample app target `net10.0`.

```powershell
./Build.ps1
```

This builds the solution, runs all tests and packs the library. All build output goes to `artifacts/`, not to `bin` and `obj` folders next to each project:

| Folder | Contents |
|---|---|
| `artifacts/bin/<project>/<configuration>` | compiled assemblies |
| `artifacts/obj/<project>` | intermediate files |
| `artifacts/TestResults` | test results (`.trx`) |
| `artifacts/package/release` | the NuGet package and its symbols |

On Linux the build also needs `bash`, `curl` and `unzip`, to download the AutoMapper package the API is compared with.

The integration tests need a SQL Server:

- **Windows:** they use SQL Server LocalDB. `Setup.ps1` installs it.
- **Linux, macOS, or any other server:** set `FORKFORWARD_TEST_SQLSERVER` to a connection string without a database name. The tests create and drop their own databases, so use a throwaway server:

  ```bash
  docker run --detach --publish 1433:1433 --env ACCEPT_EULA=Y --env 'MSSQL_SA_PASSWORD=<password>' mcr.microsoft.com/mssql/server:2022-latest
  export FORKFORWARD_TEST_SQLSERVER='Server=localhost,1433;User Id=sa;Password=<password>;TrustServerCertificate=True'
  ```

  `ACCEPT_EULA=Y` accepts the Microsoft SQL Server licence terms for that container.

To run only the tests that need no database:

```powershell
dotnet test src/UnitTests
dotnet test src/AutoMapper.DI.Tests
```

## Commit messages

A short summary line, then what changed and why. Reference the issue number. Commits written by an AI tool carry a `Co-Authored-By` trailer naming the tool.
