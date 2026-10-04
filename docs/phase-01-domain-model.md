# Phase 1 — Domain model

The domain model holds the business rules from [user-stories.md](user-stories.md), in `CarRental.Domain`, with no dependencies on databases, web frameworks or packages. Each concept below was built with the design → tests → implement → document cycle, in dependency order:

| # | Concept | Status |
|---|---|---|
| 1.1 | `DateRange` | ✅ Done |
| 1.2 | `City` | ✅ Done |
| 1.3 | `Car` | — |
| 1.4 | Pricing | — |
| 1.5 | `Booking` | — |

---

## 1.1 `DateRange`

**Files:** `src/CarRental.Domain/DateRange.cs`, `tests/CarRental.Domain.Tests/DateRangeTests.cs`

### What it is

A value object for a period of whole days from `Start` up to, but **not including**, `End`. Rentals and the cleaning/relocation "blocked periods" are both expressed as `DateRange`s.

| Member | Behaviour |
|---|---|
| `DateRange(DateOnly start, DateOnly end)` | Throws `ArgumentException` unless `end` is after `start` |
| `Start`, `End` | Get-only |
| `Days` | `End − Start`: 10th → 13th is 3 days (counted as nights) |
| `Overlaps(DateRange other)` | `true` if the ranges share at least one day; back-to-back ranges don't overlap; the result is the same whichever range you call it on |

### Design decisions

| Decision | Chosen | Alternatives rejected | Why |
|---|---|---|---|
| Date type | `DateOnly` | `DateTime`, `DateTimeOffset` | A rental date is a calendar date, not an instant. `DateTime` has a time part and a `Kind` that can shift "the 10th" to "the 9th" across time zones |
| Day counting | Nights (`End − Start`) | Inclusive calendar days | Matches how rentals are charged (user stories: 10th → 13th = 3 days) |
| Range semantics | Half-open `[Start, End)` | Closed `[Start, End]` | Back-to-back ranges don't overlap, and `Days` falls out naturally |
| Invalid input | Throw `ArgumentException` | Result object, `TryCreate` | User input is validated before it reaches the domain (Phase 5), so an invalid range here is a programming bug, and exceptions are the idiomatic signal for bugs. Result objects may still be used for business-rule failures in `Booking` |
| Type kind | `sealed record` with an explicit constructor and get-only properties | `class`, `record struct`, positional `record` | See the trap table below |
| "Not in the past" rule | **Not** in `DateRange` | Checking against `DateTime.Now` | It needs a clock and a time zone (New Zealand). Kept out so `DateRange` is pure and its tests never depend on when they run. Belongs in the booking use case (Phase 2, `TimeProvider`) |
| 1–30 day limit | **Not** in `DateRange` | Validating in the constructor | It's a rule about rentals. Blocked periods (rental + cleaning/relocation) can legitimately be up to 38 days. `Booking` will enforce it |
| Cleaning/relocation break | **Not** in `DateRange` | An `Overlaps` variant with a buffer | `Booking` knows whether a rental is one-way, so it computes its own blocked period; `DateRange` only answers "do these overlap?" |

#### The blocked-period rule

Designing `DateRange` produced one simple rule for the cleaning and relocation breaks, which `Booking` will use:

```
blocked = [Start, End + 1 + breakDays)        breakDays = 1 (cleaning) or 7 (relocation)
```

**Two bookings for the same car clash exactly when their blocked periods overlap.** That handles both directions: a new rental can't land in an existing booking's break, and its own break can't run into the next booking.

### Choosing the kind of type

| Option | Equality | Why rejected / chosen |
|---|---|---|
| `class` | `==` compares **references**: two ranges with the same dates are *not* equal | Unlike PHP, where `==` compares properties. You'd write equality code by hand |
| `readonly record struct` | By value ✅ | **`default(DateRange)` skips the constructor completely.** It creates `0001-01-01 → 0001-01-01` with no validation, silently (array elements, uninitialised fields) |
| Positional `record DateRange(DateOnly Start, DateOnly End)` | By value ✅ | Generates `init` properties, so **`with` expressions create copies without running the constructor** |
| **`sealed record` + explicit constructor + `{ get; }`** ✅ | By value ✅ | The constructor is the only way in. `range with { End = … }` is a compile error (CS0200), which we confirmed by trying it |

### How the tests were designed

- **No test just reads back a value it set.** The `Days` tests cover the cases naive day maths gets wrong: crossing a month end (30 Oct → 2 Nov) and a leap day (28 Feb → 1 Mar 2028).
- **Every overlap case is checked from both sides** (`a.Overlaps(b)` and `b.Overlaps(a)`), so an implementation that only handles one direction can't pass.
- **The back-to-back cases encode the half-open decision.** If `Overlaps` is ever "fixed" to include the end date, they fail.
- **`Should.Throw<ArgumentException>` requires that exact type.** In the red run, the stub threw `NotImplementedException` and the constructor tests still failed. That proves they check *which* exception, not just that one occurred.

Red → green sequence:

1. Tests written, no `DateRange` exists → **compile error CS0246**. In C#, a missing type is the first "red"; nothing runs until everything the tests reference exists.
2. A stub with `throw new NotImplementedException()` everywhere → compiles, **16 failed**, each as its own test failure.
3. The real implementation → **16 passed**.

### C# lessons learned

- **Records give value equality.** The compiler generates `Equals`, `GetHashCode`, `==`, `!=` and `ToString()`. `sealed` stops a subclass from changing what "equal" means.
- **`{ get; }` vs `{ get; init; }`.** `init` lets object initializers and `with` set a property, bypassing the constructor. Plain `{ get; }` can only be set in the constructor.
- **Date types are immutable.** `date.AddDays(1)` *returns* a new date and leaves the original unchanged. A bare `end.AddDays(1);` line compiles and does nothing, unlike JS's `setDate`, which changes the object in place.
- **`DateOnly` has no `-` operator.** Use `DayNumber` (days since 0001-01-01) to get an exact day count. Month lengths and leap years are handled for you.
- **`DateOnly` months are 1-based.** `new DateOnly(2026, 10, 5)` is October. In JS, `new Date(2026, 10, 5)` is November.
- **Format dates explicitly in messages.** `{end:yyyy-MM-dd}` avoids culture-dependent output (`10/13/2026` on a US server vs `13/10/2026` on an NZ one).
- **`nameof(end)`** produces `"end"`, and it updates automatically when the parameter is renamed.
- **Nullable reference types are a compile-time check only.** `Overlaps(DateRange other)` stops our code passing `null`, but `null` can still arrive at runtime, e.g. via reflection or deserialisation. No runtime guard was added because no test requires one.
- **Test-side features:**
  - `[Fact]` vs `[Theory]` + `[InlineData]`: like PHPUnit data providers / Jest `test.each`.
  - **Attribute arguments must be compile-time constants.** `DateOnly` (and `decimal`) can't go in `[InlineData]`, so the rows pass `int` day numbers to a helper.
  - **Method overloading:** `October(int)` returns a `DateOnly`; `October(int, int)` returns a `DateRange`. The compiler picks one by the arguments.
  - **Target-typed `new(...)`:** the compiler infers the type from the method's return type.
  - **xUnit creates a new test-class instance for every test,** so instance state is never shared between tests. Helpers that use no state are `static`.

---

## 1.2 `City`

**Files:** `src/CarRental.Domain/City.cs`, `tests/CarRental.Domain.Tests/CityTests.cs`

### What it is

One of the cities we operate in: where a car is based (`HomeCity`, `CurrentCity`) and where a rental starts and ends (`PickupCity`, `DropOffCity`). It's a **"smart enum"**: a `sealed record` with a private constructor and a fixed set of static instances.

| Member | Behaviour |
|---|---|
| `City.Auckland`, `City.Wellington`, `City.Christchurch` | The only `City` instances that exist |
| `Code` | Stable identifier for the database and URLs: `AKL`, `WLG`, `CHC` (NZ airport codes) |
| `Name` | Display name |
| `City.All` | All cities (read-only), for the search form's dropdown |
| `City.FromCode(string code)` | Turns a code into a `City`. **Case-insensitive** (`"akl"` → Auckland); throws `ArgumentException` for an unknown code; matches codes only, never names |

### Design decisions

| Decision | Chosen | Alternatives rejected | Why |
|---|---|---|---|
| Representation | Smart-enum `sealed record` | `enum`, database entity, `string` | See the table below |
| Identifier | Short code, separate from the display name | Store the name | The display name can change ("Ōtautahi Christchurch") without rewriting saved bookings |
| Case sensitivity | Case-insensitive `FromCode` | Exact match | A city doesn't change based on case, and URLs / form values are often lower-cased |
| Unknown code | Throw `ArgumentException` | `TryFromCode(string, out City?)` now | Nothing passes untrusted input yet. The `Try…` pattern comes in Phase 4/5 when web input needs it |
| Time zone | **Not** on `City` | `TimeZoneInfo` property per city | All three cities share NZ time. One business time zone (`Pacific/Auckland`) is used for "today" in Phase 2. It moves onto `City` if a city in another zone opens |

### Choosing a representation

| Option | Pros | Cons |
|---|---|---|
| `enum City { Auckland, … }` | Simplest; `switch` works naturally; EF Core stores it with no setup | **A C# enum is a named `int`**: `(City)42` compiles and runs, and `default(City)` silently becomes `Auckland` (value `0`). Can't carry a code or name |
| **Smart-enum record** ✅ | Private constructor → only valid instances exist. Carries data. Value equality | Slightly more code; EF Core needs a value converter in Phase 3 |
| Database entity | Staff could add cities without a deploy | Needs admin screens (out of scope); the domain could no longer refer to `City.Auckland`; makes Phase 1 depend on persistence |
| `string` | No code at all | `"Auckland"`, `"auckland "` and `"Aukland"` would all be different cities ("primitive obsession") |

Plain enums still have a place: the driver's licence type (Learner / Restricted / Full) is a closed set of labels with no data attached, which suits an enum.

### How the tests were designed

- **`All_ContainsExactlyTheThreeStarterCities`** encodes the business rule, and would catch the static-initialisation-order trap (below) at test time.
- **`All_HaveUniqueCodes`** protects future additions: copying the Auckland line for a new city and forgetting to change `"AKL"` fails the build's tests. It upper-cases the codes first, because codes are case-insensitive.
- **`FromCode_WithUnknownCode_Throws` includes `"Auckland"`**, pinning down that `FromCode` matches codes and never names.
- **`FromCode_ReturnsCityEqualToStaticInstance`** states the requirement it supports: the one-way pricing rule compares cities that may have been obtained in different ways (a static instance vs a code from a form or database).

Red → green sequence:

1. Tests written → compile error **CS0246**: `City` not found.
2. Stub (everything throws `NotImplementedException`) → the 10 City tests fail; the 16 `DateRange` tests still pass.
3. Implementation → all pass. The `[MemberData]` theory now shows as 3 rows instead of 1 (see lesson below).

### C# lessons learned

- **C# enums are named integers**, unlike PHP 8.1 enums, which are real types. Any `int` can be cast to an enum, and the default value is `0`.
- **Static field initialisers run in source order.** Declaring `All` above the three cities would capture `null`s. In this project the **compiler catches it** (CS8601: possible null reference assignment), because nullable analysis is on and warnings are errors. We proved it by moving the line. Without `<Nullable>enable</Nullable>`, it compiles and fails at runtime.
- **`static` means shared by every request and user for the whole process lifetime.** That's fine for immutable objects like `City`; dangerous for anything mutable. This comes back with DI lifetimes in Phase 2.
- **`public static readonly` fields** are the accepted exception to "no public fields" for constant-like objects (as with `string.Empty` and `TimeSpan.Zero`).
- **Private constructor on a record** turns it into a smart enum: no outside code can create new instances.
- **`IReadOnlyList<T>` + a collection expression** gives a list callers can read but not modify.
- **`FirstOrDefault(…) ?? throw …`.** `FirstOrDefault` is like JS `.find()` and returns `City?`. The throw expression turns "not found" into an exception and gives the compiler a non-null `City`, with no need for `!`.
- **Compare identifiers with `StringComparison.OrdinalIgnoreCase`,** not `ToLower()`. It doesn't depend on culture and doesn't create new strings. When you do need to change case, use `ToUpperInvariant()`. The culture-sensitive `ToUpper()` has the "Turkish I" problem (`"i"` → `"İ"`).
- **Test-side features:**
  - **`[MemberData]` + `TheoryData<T1, T2>`** supplies theory rows that aren't compile-time constants (like `City` instances). It's the second workaround for the `[InlineData]` constants rule.
  - **Collection initializer** `new() { { "AKL", City.Auckland }, … }`: each inner `{ … }` becomes a call to `Add`.
  - **Collection expression** `[a, b, c]` (C# 12): the compiler builds whatever collection type is expected.
  - **LINQ `Select`** works like JS `.map()` but is **deferred**: it only runs when something reads the results. More on this in Phase 2.
  - **A theory whose data throws runs as a single test.** In the stub run, building the `TheoryData` threw `NotImplementedException`, so xUnit fell back to one test instead of three rows. xUnit v3 runs tests in the same process it discovers them in, so `City` doesn't need to be serialisable for the rows to show separately. (We first guessed serialisation was the cause, and corrected it after checking the test counts.)
