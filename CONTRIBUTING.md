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
- The public API stays compatible with AutoMapper 14.0.0, under the `ForkForward.Mapper` namespace. A change to a public type or member is explained in the pull request.
- Discuss larger changes in an issue before you start.

## Where code may come from

Do not copy anything from AutoMapper releases published after this fork was taken, or from the `LuckyPennySoftware/AutoMapper` repository. That code is under a different licence and cannot be used here, in any form: no code, tests, documentation, or ports of them. A fix that also exists there has to be written independently. The full rule is in [AI_POLICY.md](AI_POLICY.md#4-where-code-may-come-from).

By opening a pull request you confirm that you have the right to submit the change and that it can be released under the MIT licence.

## Building

The project builds and is tested on Windows and on Linux; CI runs both. You need the .NET 10 SDK. The solution file is `ForkForward.Mapper.slnx`. The library targets `net8.0`; the tests, the benchmark and the sample app target `net10.0`.

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
dotnet test src/ForkForward.Mapper.DI.Tests
```

## Performance

The library must not get slower, or allocate more memory, than the last release. `Benchmark.ps1` checks it:

```powershell
./Benchmark.ps1
```

It builds the same benchmarks twice, against the library in this repository and against the last published package, measures both on your machine in alternating rounds, and prints a comparison. It takes about eight minutes. It fails when a mapping benchmark is more than 10 % slower than the baseline, or when any benchmark allocates more memory.

To compare with the code this fork started from:

```powershell
./Benchmark.ps1 -BaselinePackage AutoMapper -BaselineVersion 14.0.0
```

CI runs the same comparison on every pull request that changes the library, its dependencies or the benchmarks, and once more before a release is published. Two things to know about the result:

- **Allocations are exact.** They do not depend on how busy the machine is, so an increase is always a real one.
- **Timings are noisy on shared machines.** The fastest iteration of each side is compared, because other work on the machine only ever adds time. The benchmarks in `ReferenceBenchmarks` do not use the library, so both sides must measure the same there. When they do not, the run is reported as inconclusive instead of as a regression: run the job again.
- **CI measures on two runners and believes a slowdown only when both see it.** The two sides are two separately compiled assemblies of the same code, and on some machines the JIT lays one of them out less favourably; a single runner then reports one benchmark 10 % slower that another runner does not see at all. A slowdown on one runner only is shown as "not confirmed" and does not fail the check. More allocations on either runner do. Locally, `./Benchmark.ps1 -MergeVerdicts a/verdict.json, b/verdict.json` judges two runs the same way.
- **Startup benchmarks are checked for allocations only.** The time to build a configuration varies too much between runs to be a gate; it is shown for information.

New benchmarks go into `src/Benchmark`. The same sources are compiled against the baseline package, so a benchmark can only use API that the last release already has, and they do not name the library's namespace: each project supplies it (`ForkForward.Mapper` for the current side, `BaselineNamespace` in `Benchmark.Baseline.csproj` for the baseline).

After a release, raise `BaselineVersion` in `src/Benchmark.Baseline/Benchmark.Baseline.csproj` to the version just released, and set `BaselineNamespace` to `ForkForward.Mapper` once the baseline is 14.1.0 or later.

## Commit messages

A short summary line, then what changed and why. Reference the issue number. Commits written by an AI tool carry a `Co-Authored-By` trailer naming the tool.
