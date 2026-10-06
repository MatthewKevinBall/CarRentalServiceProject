# Phase 0 — Solution setup

**Goal:** an empty but correctly structured solution that builds with zero warnings and has a green test run, so every
later phase starts from a known-good baseline.

## What we built

```
CarRental.sln
global.json                  pins the .NET SDK + selects the test runner
Directory.Build.props        settings shared by every project
Directory.Packages.props     every NuGet version, declared once
.editorconfig                code style + naming rules, enforced by the build
src/
  CarRental.Domain           classlib, no references at all
  CarRental.Application      classlib  → Domain
  CarRental.Infrastructure   classlib  → Application
  CarRental.Web              Razor Pages app → Application, Infrastructure
tests/
  Directory.Build.props      settings shared by test projects (imports the root one)
  CarRental.Domain.Tests           xUnit v3 → Domain
  CarRental.Application.Tests      xUnit v3 → Application
  CarRental.Web.IntegrationTests   xUnit v3 → Web
```

Commands used (run from the repo root):

```bash
dotnet new globaljson --sdk-version 10.0.401 --roll-forward latestFeature
dotnet new sln --name CarRental --format sln
dotnet new classlib -o src/CarRental.Domain -f net10.0          # ...and Application, Infrastructure
dotnet new webapp   -o src/CarRental.Web -f net10.0
dotnet new xunit3   -o tests/CarRental.Domain.Tests -f net10.0  # ...and the other two test projects
dotnet sln add src/**/*.csproj tests/**/*.csproj
dotnet add src/CarRental.Application reference src/CarRental.Domain   # ...one per arrow above
```

## Key concepts (for a PHP/JS developer)

| .NET                | Closest PHP/JS equivalent        | Difference that matters                                                                                                                                                  |
|---------------------|----------------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Solution (`.sln`)   | monorepo workspace               | Only lists projects; no code, no runtime meaning                                                                                                                         |
| Project (`.csproj`) | `composer.json` / `package.json` | Also the compilation boundary: one project → one `.dll` (assembly)                                                                                                       |
| Project reference   | —                                | **A project can only see types from projects it explicitly references.** This is what makes the layered architecture enforceable by the compiler, not just by convention |
| Namespace           | PHP namespace                    | Logical only; not tied to folders by the compiler (we keep them aligned by convention)                                                                                   |
| NuGet               | Composer / npm                   | Packages live in a global cache, not a per-project `vendor/` / `node_modules/`                                                                                           |
| `global.json`       | `.nvmrc`                         | Pins the SDK version for this repo                                                                                                                                       |

References are **transitive**: `Application.Tests` references only `Application`, but can still use `Domain` types.

## Decisions and alternatives

| Decision                        | Chosen                             | Rejected / deferred                                       | Why                                                                                                                                                                      |
|---------------------------------|------------------------------------|-----------------------------------------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Solution format                 | `.sln`                             | `.slnx` (new XML format, .NET 10 default)                 | `.sln` is what most existing commercial codebases use; worth being fluent in. Requires `--format sln` on .NET 10                                                         |
| Test framework                  | xUnit v3                           | xUnit v2                                                  | v3 is the actively developed version. Test projects are executables (`OutputType=Exe`). The `dotnet new xunit3` template defaults to `net8.0`, so we passed `-f net10.0` |
| Test runner                     | Microsoft.Testing.Platform (MTP)   | VSTest                                                    | Template default and the direction .NET is moving. Requires `"test": { "runner": "Microsoft.Testing.Platform" }` in `global.json` (the template added it)                |
| Shared settings                 | `Directory.Build.props`            | Repeating them in each `.csproj`                          | One place to change the target framework, nullable, etc.                                                                                                                 |
| Package versions                | Central package management         | Versions in each `.csproj`                                | Prevents two projects using different versions of the same package                                                                                                       |
| Code style                      | `.editorconfig`, enforced in build | IDE-only hints                                            | CI should catch what Rider catches                                                                                                                                       |
| Architecture tests              | Skipped                            | Reflection or `NetArchTest` tests of the dependency rules | Project references already enforce most of it; may revisit                                                                                                               |
| Lockfile (`packages.lock.json`) | Deferred to Phase 6                | —                                                         | Off by default in .NET (unlike `composer.lock`); most valuable once CI exists                                                                                            |
| Shouldly / NSubstitute          | Deferred to Phases 1 / 2           | Adding them now                                           | Nothing is added until a test needs it                                                                                                                                   |
| Branching                       | Work on `main`                     | Branch per phase                                          | The project is sequential, single-developer                                                                                                                              |

## How the shared build files work

**`Directory.Build.props`.** MSBuild walks up from each `.csproj` and auto-imports the **first** `Directory.Build.props`
it finds. Ours sets:

- `TargetFramework=net10.0`, `Nullable=enable`, `ImplicitUsings=enable`
- `TreatWarningsAsErrors=true`: a warning fails the build
- `EnforceCodeStyleInBuild=true`: `.editorconfig` rules are checked by `dotnet build`, not just by the IDE

**`tests/Directory.Build.props`.** Because MSBuild stops at the *first* file found, this file would hide the root one.
It chains up explicitly:

```xml
<Import Project="$([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '$(MSBuildThisFileDirectory)../'))" />
```

It then adds what all test projects share: `OutputType=Exe`, the global `using Xunit;`, and the `xunit.v3.mtp-v2`
package.

To check what a project actually ends up with:

```bash
dotnet msbuild tests/CarRental.Domain.Tests -getProperty:Nullable -getProperty:TreatWarningsAsErrors
```

**`Directory.Packages.props`.** `ManagePackageVersionsCentrally=true` plus one `<PackageVersion>` per package. Projects
write `<PackageReference Include="..." />` with no version; adding `Version=` in a project is a build error (NU1008).
`dotnet add package X` writes the version here automatically.

**Global usings.** `ImplicitUsings` and `<Using Include="Xunit" />` generate a `*.GlobalUsings.g.cs` file under `obj/`,
which is why files can use `List<T>` or `[Fact]` with no `using` line.

## Lessons learned

1. **IDE rules aren't build rules.** Our first `.editorconfig` set `severity = warning` on each naming rule, and Rider
   flagged `private int Count`, but `dotnet build` didn't. Naming rules only reach the command-line build when their
   diagnostic ID is configured too:
   ```ini
   dotnet_diagnostic.IDE1006.severity = warning
   ```
   Always verify a rule from the CLI, because that's what CI runs.
2. **Check that a guard can fail.** We proved the setup by building a deliberately bad file:
   ```csharp
   namespace CarRental.Domain { public class Oops { private int Count; } }
   ```
   It produced IDE0161 (block-scoped namespace), IDE1006 (missing `_` prefix) and CS0169 (unused field), all as errors.
   This experiment is how lesson 1 was found. It's the same idea as "red before green" in TDD.
3. **`dotnet test` builds first.** A compile error anywhere fails the test run, so you can't accidentally test stale
   code.
4. **MTP fails a run with zero tests** (exit code 8). The template `UnitTest1` placeholders stay until Phase 1 adds real
   tests.
5. **Template defaults vary.** `classlib` defaulted to `net10.0` but `xunit3` defaulted to `net8.0`, and
   `dotnet new sln` defaults to `.slnx` on .NET 10. Pass framework and format explicitly.

## Result

```
dotnet build   → 0 warnings, 0 errors
dotnet test    → 3 succeeded, 0 failed
```
