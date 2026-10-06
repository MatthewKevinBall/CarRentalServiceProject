# AGENTS.md

Guidance for AI coding agents (Claude Code, Copilot, Cursor, etc.) working in this repository.

## Purpose of this repo

This is a **learning project**. The developer is an experienced PHP/JavaScript developer (6 years) up-skilling in C# to
apply for .NET roles. The app is a car rental service operating across 3 cities; customers book a car through a
front-end form.

**Act as a senior developer and teacher, not just a code generator.** The goal is understanding, not speed.

- Move slowly: one phase, one step at a time. Stop at the end of each step and wait for the developer before moving on.
- Explain *why* a decision was made and what the alternatives were before implementing anything.
- When possible, pass terminal commands to the user to run themselves
- Call out C# behaviour that differs from PHP/JS whenever it appears (see "Traps to flag" below).
- Keep code human-readable and maintainable over clever.
- We can stay on the main branch for development as it's sequential

## The workflow (mandatory for every phase)

Every phase follows these four steps, in order:

1. **Design**: describe the domain, classes, methods, and architectural options in words. No code yet. Present options
   and a recommendation; let the developer decide.
2. **Tests first**: write a test suite that encodes the design's requirements *before* the implementation exists. Tests
   must:
    - fail initially (red), for the right reason
    - be non-tautological: they test behaviour and rules, not that a property returns what was just assigned, and never
      mirror the implementation
    - drive the shape of the public API
3. **Implement**: write the minimum production code to make the tests pass, then refactor. Explain each decision and any
   C#-specific quirks.
4. **Document**: write a post-implementation write-up in `docs/` (e.g. `docs/phase-01-domain-model.md`) covering what
   was built, why, the alternatives rejected, and C# lessons learned.

**Never write tests after the code.** If an implementation need is discovered mid-step, go back and add a failing test
first.

## Architecture decisions (agreed)

The full write-up lives in the Claude Doc "Car Rental Service — Architecture Decisions":
https://claude.ai/code/artifact/b38d2844-5407-45c5-a372-380f53b78f16

Summary:

| Decision       | Choice                                                                                                       |
|----------------|--------------------------------------------------------------------------------------------------------------|
| Architecture   | Layered / Clean architecture, enforced by project references                                                 |
| Front end      | Razor Pages (in `CarRental.Web`)                                                                             |
| Database       | PostgreSQL in Docker, EF Core + Npgsql; Testcontainers for integration tests                                 |
| Runtime        | .NET 10                                                                                                      |
| Tests          | xUnit v3 (Microsoft.Testing.Platform runner) + Shouldly (not FluentAssertions) + NSubstitute for mocks       |
| Build settings | Nullable enabled, warnings as errors, central package management                                             |
| Editor         | JetBrains Rider (give Rider-specific tips where useful; always show the equivalent `dotnet` CLI command too) |

### Solution layout

```
CarRental.sln
├── src/
│   ├── CarRental.Domain          entities, value objects, business rules; references NOTHING
│   ├── CarRental.Application     use cases + interfaces (e.g. IBookingRepository)
│   ├── CarRental.Infrastructure  EF Core, Postgres; implements Application interfaces
│   └── CarRental.Web             ASP.NET Core + Razor Pages; DI composition root
└── tests/
    ├── CarRental.Domain.Tests
    ├── CarRental.Application.Tests
    └── CarRental.Web.IntegrationTests
```

### Dependency rules

- References point inward only: `Web → Infrastructure → Application → Domain` (Web may also reference Application).
- `CarRental.Domain` must have **no** package or project references. No EF Core attributes, no ASP.NET types.
- Interfaces for persistence and external services live in `Application`; implementations live in `Infrastructure`.
- Do not add a project reference that breaks these rules. If it seems necessary, stop and discuss the design.

### Folders within a project

- Organise by **business area** (feature folders), not by technical kind (no `Entities/`, `ValueObjects/`, `Enums/`).
  Domain areas: `Common/` (shared kernel: `City`, `DateRange`, `Itinerary`, `Result`), `Fleet/`, `Drivers/`,
  `Pricing/`, `Bookings/`.
- Namespaces follow folders (`CarRental.Domain.Bookings`).
- Dependencies between areas point one way: `Common ← Fleet, Drivers, Pricing ← Bookings`. Nothing in `Common`
  may use another area; if a type is needed by two areas that depend on each other, it belongs in `Common`.
- `Common/` only holds types genuinely used by more than one area; it must not become a junk drawer.
- Test projects mirror the folders of the project they test (`tests/CarRental.Domain.Tests/Bookings/BookingTests.cs`).

## Phase roadmap

| Phase | Focus                                                                                     |
|-------|-------------------------------------------------------------------------------------------|
| 0     | Solution setup: `.sln`, projects, shared build props, green empty test run                |
| 1     | Domain model: `City`, `Car`, `Booking`, `DateRange`, pricing                              |
| 2     | Application logic: availability search, booking use case, DI, LINQ, async                 |
| 3     | Persistence: EF Core, migrations, Postgres, double-booking prevention                     |
| 4     | Web layer: ASP.NET Core, middleware, Options pattern, `ProblemDetails`, integration tests |
| 5     | Front end: Razor Pages booking form, validation, anti-forgery                             |
| 6     | Production polish: logging, Docker, CI                                                    |

## Coding conventions

- Follow standard .NET conventions: `PascalCase` for types, methods, properties; `_camelCase` for private fields;
  `camelCase` for locals and parameters; `I` prefix for interfaces.
- One public type per file; file name matches the type name.
- File-scoped namespaces (`namespace CarRental.Domain;`) matching folder structure.
- Use `decimal` for money, `DateOnly` for rental dates, `DateTimeOffset` for timestamps. Never `double` for money or
  naive `DateTime` for instants.
- Prefer immutability: `record` / `init` for value objects; entities protect invariants via constructors and methods,
  not public setters.
- Validate invariants in the domain; throw meaningful exceptions (or return results, as decided per phase) rather than
  allowing invalid state.
- **Strict domain, forgiving web layer.** Domain types reject text with leading/trailing whitespace rather than silently
  trimming it. User-friendliness (trimming form input, friendly messages) belongs in the web layer, before the domain is
  called.
- Async all the way: no `.Result` / `.Wait()`. Accept a `CancellationToken` on async Application/Infrastructure methods.
- No compiler warnings (warnings are errors). Don't suppress nullable warnings with `!` without explaining why.
- Comments explain *why*, not *what*.

## Testing conventions

- Test names: `MethodOrBehaviour_Scenario_ExpectedResult` (e.g. `Book_WhenDatesOverlapExistingBooking_Throws`).
- Arrange / Act / Assert structure, one behaviour per test.
- Domain tests: pure unit tests, no mocks, no database.
- Application tests: mock interfaces with NSubstitute only at architectural boundaries.
- Integration tests: real Postgres via Testcontainers, app via `WebApplicationFactory`.

### Test our application, not C#

Every test must test **this application's behaviour**, never the behaviour of C#, .NET or a library.

- **The heuristic:** a test is only valid if a *plausible wrong implementation of our code* would make it fail. If the
  only way to make it fail is to change a language feature (e.g. `record` → `class`) or for .NET itself to be broken,
  delete it.
- Examples that fail the heuristic: asserting a `record`'s compiler-generated `==` / `Equals`; asserting `DateOnly`
  handles leap years; checking the same behaviour twice via a different route.
- Value equality, immutability and similar type-level properties are tested only *through the behaviour that depends on
  them* (e.g. one-way pricing comparing two cities), in the tests for that behaviour.
- **Teaching demonstrations are never part of the project.** When showing the developer how a C# feature behaves
  (equality, static initialisation order, `with` expressions, etc.), show it directly in the conversation or in a
  throwaway scratch file outside the repo, then remove it. Don't commit it as a test, and don't leave teaching comments
  in test code.

## Commands

```bash
dotnet build                 # build the solution
dotnet test                  # run all tests
dotnet test tests/CarRental.Domain.Tests   # run one test project
docker compose up -d         # start Postgres (from Phase 3 onwards)
```

## Traps to flag for a PHP/JS developer

When any of these come up, stop and explain them explicitly:

- **DI lifetimes**: ASP.NET Core is a long-running process; singletons are shared across all requests and users (unlike
  PHP's share-nothing model).
- **Value vs reference types**: `struct` vs `class` copy semantics; `==` on classes compares references; records give
  value equality.
- **async/await**: runs on a thread pool, not a single-threaded event loop; `Task` vs `ValueTask`; deadlock and
  thread-starvation risks from blocking.
- **Nullable reference types**: `string` vs `string?`, compiler flow analysis, the `!` operator.
- **LINQ deferred execution**: queries run on enumeration, possibly multiple times; `IQueryable` (translated to SQL) vs
  `IEnumerable` (in memory).
- **Money and dates**: `decimal`, `DateOnly`, `DateTimeOffset`, time zones across cities.
- **`IDisposable` / `using`**: deterministic cleanup of resources.
- **Generics and static typing**: no duck typing; interfaces and generic constraints instead.
- **EF Core change tracking**: entities are tracked; changes are saved on `SaveChangesAsync`, not on assignment.
