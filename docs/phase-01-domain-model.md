# Phase 1 — Domain model

The domain model holds the business rules from [user-stories.md](user-stories.md), in `CarRental.Domain`, with no
dependencies on databases, web frameworks or packages. Each concept below was built with the design → tests →
implement → document cycle, in dependency order:

| #    | Concept                                                                                    | Status  |
|------|--------------------------------------------------------------------------------------------|---------|
| 1.1  | `DateRange`                                                                                | ✅ Done |
| 1.2  | `City`                                                                                     | ✅ Done |
| 1.3  | `Car` (with `CarType`, `Registration`)                                                     | ✅ Done |
| 1.4  | Pricing (`Money`, `PriceList`, `PriceQuote`)                                               | ✅ Done |
| 1.5a | Driver (`Driver`, `DriverLicence`, `LicenceType`, `EmailAddress`)                          | ✅ Done |
| 1.5b | `Booking` (with `Itinerary`, `CarSelection`, `Result`, `BookingReference`, `BookingError`) | ✅ Done |

---

## 1.1 `DateRange`

**Files:** `src/CarRental.Domain/Common/DateRange.cs`, `tests/CarRental.Domain.Tests/Common/DateRangeTests.cs`

### What it is

A value object for a period of whole days from `Start` up to, but **not including**, `End`. Rentals and the
cleaning/relocation "blocked periods" are both expressed as `DateRange`s.

| Member                                    | Behaviour                                                                                                                             |
|-------------------------------------------|---------------------------------------------------------------------------------------------------------------------------------------|
| `DateRange(DateOnly start, DateOnly end)` | Throws `ArgumentException` unless `end` is after `start`                                                                              |
| `Start`, `End`                            | Get-only                                                                                                                              |
| `Days`                                    | `End − Start`: 10th → 13th is 3 days (counted as nights)                                                                              |
| `Overlaps(DateRange other)`               | `true` if the ranges share at least one day; back-to-back ranges don't overlap; the result is the same whichever range you call it on |

### Design decisions

| Decision                  | Chosen                                                               | Alternatives rejected                         | Why                                                                                                                                                                                                                                       |
|---------------------------|----------------------------------------------------------------------|-----------------------------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Date type                 | `DateOnly`                                                           | `DateTime`, `DateTimeOffset`                  | A rental date is a calendar date, not an instant. `DateTime` has a time part and a `Kind` that can shift "the 10th" to "the 9th" across time zones                                                                                        |
| Day counting              | Nights (`End − Start`)                                               | Inclusive calendar days                       | Matches how rentals are charged (user stories: 10th → 13th = 3 days)                                                                                                                                                                      |
| Range semantics           | Half-open `[Start, End)`                                             | Closed `[Start, End]`                         | Back-to-back ranges don't overlap, and `Days` falls out naturally                                                                                                                                                                         |
| Invalid input             | Throw `ArgumentException`                                            | Result object, `TryCreate`                    | User input is validated before it reaches the domain (Phase 5), so an invalid range here is a programming bug, and exceptions are the idiomatic signal for bugs. Result objects may still be used for business-rule failures in `Booking` |
| Type kind                 | `sealed record` with an explicit constructor and get-only properties | `class`, `record struct`, positional `record` | See the trap table below                                                                                                                                                                                                                  |
| "Not in the past" rule    | **Not** in `DateRange`                                               | Checking against `DateTime.Now`               | It needs a clock and a time zone (New Zealand). Kept out so `DateRange` is pure and its tests never depend on when they run. Belongs in the booking use case (Phase 2, `TimeProvider`)                                                    |
| 1–30 day limit            | **Not** in `DateRange`                                               | Validating in the constructor                 | It's a rule about rentals. Blocked periods (rental + cleaning/relocation) can legitimately be up to 38 days. `Booking` will enforce it                                                                                                    |
| Cleaning/relocation break | **Not** in `DateRange`                                               | An `Overlaps` variant with a buffer           | `Booking` knows whether a rental is one-way, so it computes its own blocked period; `DateRange` only answers "do these overlap?"                                                                                                          |

#### The blocked-period rule

Designing `DateRange` produced one simple rule for the cleaning and relocation breaks, which `Booking` will use:

```
blocked = [Start, End + 1 + breakDays)        breakDays = 1 (cleaning) or 7 (relocation)
```

**Two bookings for the same car clash exactly when their blocked periods overlap.** That handles both directions: a new
rental can't land in an existing booking's break, and its own break can't run into the next booking.

### Choosing the kind of type

| Option                                                      | Equality                                                                     | Why rejected / chosen                                                                                                                                               |
|-------------------------------------------------------------|------------------------------------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `class`                                                     | `==` compares **references**: two ranges with the same dates are *not* equal | Unlike PHP, where `==` compares properties. You'd write equality code by hand                                                                                       |
| `readonly record struct`                                    | By value ✅                                                                  | **`default(DateRange)` skips the constructor completely.** It creates `0001-01-01 → 0001-01-01` with no validation, silently (array elements, uninitialised fields) |
| Positional `record DateRange(DateOnly Start, DateOnly End)` | By value ✅                                                                  | Generates `init` properties, so **`with` expressions create copies without running the constructor**                                                                |
| **`sealed record` + explicit constructor + `{ get; }`** ✅  | By value ✅                                                                  | The constructor is the only way in. `range with { End = … }` is a compile error (CS0200), which we confirmed by trying it                                           |

### How the tests were designed

- **No test just reads back a value it set.** The `Days` tests cover the case naive day maths gets wrong: crossing a
  month end (30 Oct → 2 Nov, where `End.Day - Start.Day` gives −28).
- **Every overlap case is checked from both sides** (`a.Overlaps(b)` and `b.Overlaps(a)`), so an implementation that
  only handles one direction can't pass.
- **The back-to-back cases encode the half-open decision.** If `Overlaps` is ever "fixed" to include the end date, they
  fail.
- **`Should.Throw<ArgumentException>` requires that exact type.** In the red run, the stub threw
  `NotImplementedException` and the constructor tests still failed. That proves they check *which* exception, not just
  that one occurred.

Red → green sequence:

1. Tests written, no `DateRange` exists → **compile error CS0246**. In C#, a missing type is the first "red"; nothing
   runs until everything the tests reference exists.
2. A stub with `throw new NotImplementedException()` everywhere → compiles, **16 failed**, each as its own test failure.
3. The real implementation → **16 passed**.

#### Test review: removing tests that tested C#

After implementation we reviewed every test with one question: *can a plausible wrong implementation of our code make
this test fail?* (Now a rule in `AGENTS.md`.) Three tests failed that check and were removed, taking the suite from 16
to 13:

| Removed test                                        | Why                                                                                                                                                                                          |
|-----------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `Equality_WhenSameDates_AreEqual`                   | Tested the compiler-generated `record` equality. The only way to fail it is to change `record` to `class`. Nothing in the app compares `DateRange`s with `==`, so no behaviour depends on it |
| `Equality_WhenDifferentDates_AreNotEqual`           | Same: tested the generated `!=`                                                                                                                                                              |
| `Days_WhenRangeCrossesLeapDay_IncludesFebruary29th` | Any implementation that passes the month-end test already uses .NET's calendar maths, so this only tested .NET's handling of leap years                                                      |

The equality tests had been written partly to *demonstrate* value equality. Teaching demonstrations now stay in the
conversation, never in the test suite.

### C# lessons learned

- **Records give value equality.** The compiler generates `Equals`, `GetHashCode`, `==`, `!=` and `ToString()`. `sealed`
  stops a subclass from changing what "equal" means.
- **`{ get; }` vs `{ get; init; }`.** `init` lets object initializers and `with` set a property, bypassing the
  constructor. Plain `{ get; }` can only be set in the constructor.
- **Date types are immutable.** `date.AddDays(1)` *returns* a new date and leaves the original unchanged. A bare
  `end.AddDays(1);` line compiles and does nothing, unlike JS's `setDate`, which changes the object in place.
- **`DateOnly` has no `-` operator.** Use `DayNumber` (days since 0001-01-01) to get an exact day count. Month lengths
  and leap years are handled for you.
- **`DateOnly` months are 1-based.** `new DateOnly(2026, 10, 5)` is October. In JS, `new Date(2026, 10, 5)` is November.
- **Format dates explicitly in messages.** `{end:yyyy-MM-dd}` avoids culture-dependent output (`10/13/2026` on a US
  server vs `13/10/2026` on an NZ one).
- **`nameof(end)`** produces `"end"`, and it updates automatically when the parameter is renamed.
- **Nullable reference types are a compile-time check only.** `Overlaps(DateRange other)` stops our code passing `null`,
  but `null` can still arrive at runtime, e.g. via reflection or deserialisation. No runtime guard was added because no
  test requires one.
- **Test-side features:**
    - `[Fact]` vs `[Theory]` + `[InlineData]`: like PHPUnit data providers / Jest `test.each`.
    - **Attribute arguments must be compile-time constants.** `DateOnly` (and `decimal`) can't go in `[InlineData]`, so
      the rows pass `int` day numbers to a helper.
    - **Method overloading:** `October(int)` returns a `DateOnly`; `October(int, int)` returns a `DateRange`. The
      compiler picks one by the arguments.
    - **Target-typed `new(...)`:** the compiler infers the type from the method's return type.
    - **xUnit creates a new test-class instance for every test,** so instance state is never shared between tests.
      Helpers that use no state are `static`.

---

## 1.2 `City`

**Files:** `src/CarRental.Domain/Common/City.cs`, `tests/CarRental.Domain.Tests/Common/CityTests.cs`

### What it is

One of the cities we operate in: where a car is based (`HomeCity`, `CurrentCity`) and where a rental starts and ends
(`PickupCity`, `DropOffCity`). It's a **"smart enum"**: a `sealed record` with a private constructor and a fixed set of
static instances.

| Member                                                  | Behaviour                                                                                                                                              |
|---------------------------------------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------|
| `City.Auckland`, `City.Wellington`, `City.Christchurch` | The only `City` instances that exist                                                                                                                   |
| `Code`                                                  | Stable identifier for the database and URLs: `AKL`, `WLG`, `CHC` (NZ airport codes)                                                                    |
| `Name`                                                  | Display name                                                                                                                                           |
| `City.All`                                              | All cities (read-only), for the search form's dropdown                                                                                                 |
| `City.FromCode(string code)`                            | Turns a code into a `City`. **Case-insensitive** (`"akl"` → Auckland); throws `ArgumentException` for an unknown code; matches codes only, never names |

### Design decisions

| Decision         | Chosen                                     | Alternatives rejected                | Why                                                                                                                                                              |
|------------------|--------------------------------------------|--------------------------------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Representation   | Smart-enum `sealed record`                 | `enum`, database entity, `string`    | See the table below                                                                                                                                              |
| Identifier       | Short code, separate from the display name | Store the name                       | The display name can change ("Ōtautahi Christchurch") without rewriting saved bookings                                                                           |
| Case sensitivity | Case-insensitive `FromCode`                | Exact match                          | A city doesn't change based on case, and URLs / form values are often lower-cased                                                                                |
| Unknown code     | Throw `ArgumentException`                  | `TryFromCode(string, out City?)` now | Nothing passes untrusted input yet. The `Try…` pattern comes in Phase 4/5 when web input needs it                                                                |
| Time zone        | **Not** on `City`                          | `TimeZoneInfo` property per city     | All three cities share NZ time. One business time zone (`Pacific/Auckland`) is used for "today" in Phase 2. It moves onto `City` if a city in another zone opens |

### Choosing a representation

| Option                      | Pros                                                                           | Cons                                                                                                                                                  |
|-----------------------------|--------------------------------------------------------------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------|
| `enum City { Auckland, … }` | Simplest; `switch` works naturally; EF Core stores it with no setup            | **A C# enum is a named `int`**: `(City)42` compiles and runs, and `default(City)` silently becomes `Auckland` (value `0`). Can't carry a code or name |
| **Smart-enum record** ✅    | Private constructor → only valid instances exist. Carries data. Value equality | Slightly more code; EF Core needs a value converter in Phase 3                                                                                        |
| Database entity             | Staff could add cities without a deploy                                        | Needs admin screens (out of scope); the domain could no longer refer to `City.Auckland`; makes Phase 1 depend on persistence                          |
| `string`                    | No code at all                                                                 | `"Auckland"`, `"auckland "` and `"Aukland"` would all be different cities ("primitive obsession")                                                     |

Plain enums still have a place: the driver's licence type (Learner / Restricted / Full) is a closed set of labels with
no data attached, which suits an enum.

### How the tests were designed

- **`All_ContainsExactlyTheThreeStarterCities`** encodes the business rule, and would catch the
  static-initialisation-order trap (below) at test time.
- **`All_HaveUniqueCodes`** protects future additions: copying the Auckland line for a new city and forgetting to change
  `"AKL"` fails the build's tests. It upper-cases the codes first, because codes are case-insensitive.
- **`FromCode_WithUnknownCode_Throws` includes `"Auckland"`**, pinning down that `FromCode` matches codes and never
  names.
- **City equality is not tested here.** The one-way pricing rule depends on comparing two cities, but that's a pricing
  behaviour, so it's tested in the pricing tests (1.4). A test `FromCode_ReturnsCityEqualToStaticInstance` existed
  briefly and was removed in the test review (below).

Red → green sequence:

1. Tests written → compile error **CS0246**: `City` not found.
2. Stub (everything throws `NotImplementedException`) → the 10 City tests fail; the 16 `DateRange` tests still pass.
3. Implementation → all pass. The `[MemberData]` theory now shows as 3 rows instead of 1 (see lesson below).

**Test review.** `FromCode_ReturnsCityEqualToStaticInstance` was removed. It checked that `FromCode("AKL")` equals
`City.Auckland`, which `FromCode_WithKnownCode_ReturnsThatCity` already checks. Its only extra claim was that the
compiler-generated `==` agrees with `Equals`, which is testing C#.

### C# lessons learned

- **C# enums are named integers**, unlike PHP 8.1 enums, which are real types. Any `int` can be cast to an enum, and the
  default value is `0`.
- **Static field initialisers run in source order.** Declaring `All` above the three cities would capture `null`s. In
  this project the **compiler catches it** (CS8601: possible null reference assignment), because nullable analysis is on
  and warnings are errors. We proved it by moving the line. Without `<Nullable>enable</Nullable>`, it compiles and fails
  at runtime.
- **`static` means shared by every request and user for the whole process lifetime.** That's fine for immutable objects
  like `City`; dangerous for anything mutable. This comes back with DI lifetimes in Phase 2.
- **`public static readonly` fields** are the accepted exception to "no public fields" for constant-like objects (as
  with `string.Empty` and `TimeSpan.Zero`).
- **Private constructor on a record** turns it into a smart enum: no outside code can create new instances.
- **`IReadOnlyList<T>` + a collection expression** gives a list callers can read but not modify.
- **`FirstOrDefault(…) ?? throw …`.** `FirstOrDefault` is like JS `.find()` and returns `City?`. The throw expression
  turns "not found" into an exception and gives the compiler a non-null `City`, with no need for `!`.
- **Compare identifiers with `StringComparison.OrdinalIgnoreCase`,** not `ToLower()`. It doesn't depend on culture and
  doesn't create new strings. When you do need to change case, use `ToUpperInvariant()`. The culture-sensitive
  `ToUpper()` has the "Turkish I" problem (`"i"` → `"İ"`).
- **Test-side features:**
    - **`[MemberData]` + `TheoryData<T1, T2>`** supplies theory rows that aren't compile-time constants (like `City`
      instances). It's the second workaround for the `[InlineData]` constants rule.
    - **Collection initializer** `new() { { "AKL", City.Auckland }, … }`: each inner `{ … }` becomes a call to `Add`.
    - **Collection expression** `[a, b, c]` (C# 12): the compiler builds whatever collection type is expected.
    - **LINQ `Select`** works like JS `.map()` but is **deferred**: it only runs when something reads the results. More
      on this in Phase 2.
    - **A theory whose data throws runs as a single test.** In the stub run, building the `TheoryData` threw
      `NotImplementedException`, so xUnit fell back to one test instead of three rows. xUnit v3 runs tests in the same
      process it discovers them in, so `City` doesn't need to be serialisable for the rows to show separately. (We first
      guessed serialisation was the cause, and corrected it after checking the test counts.)

---

## 1.3 `Car` (with `CarType` and `Registration`)

**Files:** `src/CarRental.Domain/Fleet/Car.cs`, `src/CarRental.Domain/Fleet/CarType.cs`, `src/CarRental.Domain/Fleet/Registration.cs`; `tests/CarRental.Domain.Tests/Fleet/CarTests.cs`,
`tests/CarRental.Domain.Tests/Fleet/RegistrationTests.cs`

### What was built

**`Car`: the first entity.** A car is defined by its identity, not its details: two white Economy Corollas in Auckland
are two different cars, and a car stays the same car when its data changes.

| Member                                                   | Behaviour                                                                                              |
|----------------------------------------------------------|--------------------------------------------------------------------------------------------------------|
| `Car(Registration, make, model, CarType, City homeCity)` | Rejects blank make/model (`ArgumentException`) and undefined car types (`ArgumentOutOfRangeException`) |
| `Id`                                                     | A new time-ordered `Guid` per car, created in the constructor                                          |
| `Registration`, `Make`, `Model`, `Type`, `HomeCity`      | Fixed at construction                                                                                  |
| `CurrentCity`                                            | Starts as `HomeCity`; the only property that can change                                                |
| `RecordArrivalIn(City)`                                  | Changes `CurrentCity`; never `HomeCity`                                                                |

**`CarType`: a plain enum.** `Economy = 1`, `Standard = 2`, `Suv = 3`, `Premium = 4`. Daily rates are deliberately not
here; they belong to pricing (1.4).

**`Registration`: a value object for NZ number plates.**

| Rule                                            | Example                                                      |
|-------------------------------------------------|--------------------------------------------------------------|
| 1–6 characters, **spaces count**                | `AB 123` ✅, `ABC 123` ❌                                    |
| Letters A–Z and digits 0–9 only                 | `ABC-12` ❌, `ÄBC12` ❌, `ſAB12` ❌                          |
| Single spaces only, and only between characters | `A B C` ✅, `AB  12` ❌, ` AB12` ❌, `AB12 ` ❌, `AB\t12` ❌ |
| Lower case is accepted and stored upper case    | `abc123` → `ABC123`                                          |
| Spaces are significant                          | `AB 12` and `AB12` are different plates                      |

### Design decisions

| Decision                    | Chosen                                                             | Alternatives rejected                                    | Why                                                                                                                                                                                                                                 |
|-----------------------------|--------------------------------------------------------------------|----------------------------------------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Identity                    | `Guid.CreateVersion7()`, created in the constructor                | Database-generated `int`; number plate                   | The car has an ID before any database exists. Version 7 GUIDs are time-ordered, so database indexes stay efficient. Plates change (personalised plates move between cars), so they can't be an identity                             |
| Strongly-typed ID (`CarId`) | Not yet                                                            | Wrap the `Guid` in a `CarId` type                        | Prevents mixing up car and booking IDs, but adds mapping code in EF Core. Revisit if `Booking` makes it a real risk                                                                                                                 |
| Equality                    | Plain `sealed class`, reference equality; compare `Id` when needed | `record`; an `Entity` base class that overrides `Equals` | Record equality would make two identical-looking cars "equal", and its hash code would change when `CurrentCity` changes, silently breaking `HashSet`/`Dictionary` lookups. An `Entity` base class is subtle code we don't need yet |
| Car type                    | Plain `enum CarType`, values from 1                                | Smart enum with a `DailyRate`                            | A car knows *what* it is; pricing knows what that *costs*. Prices change more often than types, and may move to configuration later                                                                                                 |
| Current city                | Stored; changed only via `RecordArrivalIn`                         | Calculated from bookings; left out                       | Matches the user stories' "home city and current city", ready for the future "rent onward" feature. Nothing reads it yet                                                                                                            |
| Number plate                | `Registration` value object                                        | A validated `string` on `Car`                            | It has rules (characters, length, spacing) and normalisation (case), which earns a type of its own                                                                                                                                  |
| Plate edge spaces           | **Rejected**                                                       | Trimmed                                                  | Product-owner decision: callers must send a clean plate                                                                                                                                                                             |
| Bookings                    | `Car` does **not** know its bookings                               | `car.Book(...)` checking clashes in memory               | Loading a car shouldn't load years of bookings, and two simultaneous bookings can only be stopped reliably by the database (Phase 3). Clash checks go in the application layer (Phase 2)                                            |

### Value object or plain properties? (the questions we used)

Extract a value object (record) when **any** of these is true:

1. Do the values belong together (always set and replaced as a unit)?
2. Is there a rule to enforce?
3. Is there behaviour derived from the values?
4. Does the business have a word for it?
5. Is it used by more than one entity? (A bonus, not a requirement.)

Keep plain properties when it's a single independent value with no rules: `Make` and `Model` are plain strings. A value
object still *belongs to* its entity, and in Phase 3 EF Core can store it as columns in the entity's own table, so
modelling it separately doesn't mean a separate table.

### How the tests were designed

Every test was checked against the `AGENTS.md` heuristic: **would a plausible wrong implementation of our code fail
it?**

| Test                                                               | Wrong code it catches                                                                                    |
|--------------------------------------------------------------------|----------------------------------------------------------------------------------------------------------|
| Blank make / model rejected (`""`, `"   "`)                        | Checking for empty but not whitespace-only strings                                                       |
| Undefined `CarType` rejected (`0`, `42`)                           | No check; an enum starting at `0`, so `default` becomes a real type                                      |
| Each car gets its own ID                                           | `Id` never set (both `Guid.Empty`); a hard-coded ID                                                      |
| New car's current city is its home city                            | Forgetting to set `CurrentCity` (it would be `null` despite the nullable checks)                         |
| `RecordArrivalIn` changes current city / doesn't change home city  | A method that does nothing, or changes the wrong property                                                |
| Plate normalised to upper case, spaces kept                        | Removing spaces, which would quietly make `AB 12` and `AB12` the same plate                              |
| Plate boundaries (`HELLO1`, `AB 123` ✅ / `ABC1234`, `ABC 123` ❌) | Off-by-one length checks; not counting spaces                                                            |
| `AB  12`, ` AB12`, `AB\t12` rejected                               | Allowing spaces without checking their neighbours; `Trim()`; `char.IsWhiteSpace` (matches tabs and more) |
| `ÄBC12` rejected                                                   | `char.IsLetterOrDigit` (accepts any Unicode letter)                                                      |
| `AB12\n` rejected                                                  | Ending the regex with `$` instead of `\z`                                                                |
| `ſAB12` rejected                                                   | Upper-casing *before* validating (`ſ` becomes an ASCII `S`)                                              |

Deliberately **not** tested: that `Make` returns what was passed in (tautological), and that two `Registration`s for the
same plate are equal (record equality is C#; the normalisation tests cover what we control). `CarType` has no tests of
its own: an enum has no behaviour, and its rules are tested through `Car`'s constructor.

Red → green sequence:

1. Tests written → compile error **CS0246**: `Car`, `CarType`, `Registration` not found.
2. While planning the implementation, two edge cases were found (`AB12\n` and a non-ASCII letter that upper-cases to
   ASCII). Following the workflow rule, **test cases were added before any code**.
3. Stubs → **29 failed**; the 24 existing tests still passed.
4. Implementation → **all 55 passed**.
5. **Each edge-case test was checked by putting its bug back** (`$` instead of `\z`; upper-casing first):
    - `AB12\n` failed as intended.
    - The original non-ASCII case, Turkish `ı`, **did not fail**. `ToUpperInvariant()` leaves `ı` unchanged (`ı` → `I`
      is a Turkish-culture rule, not an invariant one), so that test could never catch the bug. It was replaced with `ſ`
      (long s), which *does* become `S`, and the check then failed as intended.

### C# lessons learned

- **Entities vs value objects.** Entities have identity and change over time (`class`); value objects are defined by
  their values and never change (`record`). A record's hash code depends on its values, so a *mutable* record in a
  `HashSet` or `Dictionary` gets lost when it changes.
- **`partial`.** One type defined in several places, joined at compile time. Mainly used so **source generators** can
  add code to your types. `[GeneratedRegex]` on a `partial` method makes the compiler generate the regex matcher during
  the build; the type containing it must be `partial` too (otherwise CS0751). `partial` can't extend types from other
  projects; extension methods do that (Phase 2).
- **Regex in .NET:**
    - `$` also matches just before a trailing `\n`. Use `\z` for "absolute end".
    - `[A-Za-z]` is ASCII-only, but `RegexOptions.IgnoreCase` brings back Unicode surprises (e.g. the Kelvin sign `K`
      matching `k`).
- **`char.IsLetterOrDigit` and `char.IsWhiteSpace` are Unicode-wide.** They accept `Ä`, tabs, non-breaking spaces and
  more. For rules about specific ASCII characters, match those characters explicitly.
- **Validate raw input, then normalise.** `ToUpperInvariant()` can turn invalid input into valid-looking input (`ſ` →
  `S`).
- **Invariant culture ≠ Turkish culture.** `ToUpperInvariant()` doesn't do the `ı` → `I` mapping; only culture-specific
  upper-casing (e.g. Turkish) does.
- **Enum safety.** Start explicit values at 1 and check `Enum.IsDefined(value)` wherever an enum crosses into the
  domain.
- **Built-in guard clauses.** `ArgumentException.ThrowIfNullOrWhiteSpace(make)` uses `[CallerArgumentExpression]`: the
  compiler passes the argument's text (`"make"`) as the parameter name, so no `nameof` is needed.
- **`ArgumentOutOfRangeException`** is the convention for an argument of the right kind but outside the allowed values.
  Shouldly's `Should.Throw<T>` matches the exact type, so tests pin down which exception is thrown.
- **Acronyms in names.** Two letters stay upper case (`IO`); three or more become PascalCase (`Suv`, `Html`).
- **A property can share its type's name** (`public Registration Registration { get; }`), known as the "Color Color"
  case.
- **Entity methods name business events.** `RecordArrivalIn(city)`, not `SetCurrentCity(city)`, and the setter is
  `private`.
- **Test-side features:**
    - **Optional parameters** must have compile-time-constant defaults, so `City? homeCity = null` +
      `homeCity ?? City.Auckland` is the workaround.
    - **Named arguments** (`CreateCar(model: "")`) let each test change only the detail it's about.
    - **Enum values are constants,** so `[InlineData((CarType)42)]` is allowed.
- **Check that a test can fail.** Two of our edge-case tests looked equally convincing; putting the bug back showed one
  of them could never fail.

---

## 1.4 Pricing (`Money`, `PriceList`, `PriceQuote`)

**Files:** `src/CarRental.Domain/Pricing/Money.cs`, `src/CarRental.Domain/Pricing/PriceList.cs`, `src/CarRental.Domain/Pricing/PriceQuote.cs`;
`tests/CarRental.Domain.Tests/Pricing/MoneyTests.cs`, `tests/CarRental.Domain.Tests/Pricing/PriceListTests.cs`

### The rules (from the user stories)

| Rule         | Value                                                                                    |
|--------------|------------------------------------------------------------------------------------------|
| Rental price | Daily rate × days                                                                        |
| Daily rate   | By car type (agreed: Economy $55, Standard $75, SUV $105, Premium $140)                  |
| One-way fee  | +$100 when the drop-off city ≠ the pickup city                                           |
| Deposit      | 10% of the rental price (**not** the one-way fee), any fraction of a cent rounded **up** |
| Total        | Rental price + one-way fee + deposit                                                     |

Worked example: SUV, Auckland → Wellington, 3 days = $315.00 + $100.00 + $31.50 = **$446.50**.

### What was built

**`Money`: a `readonly record struct`.** An amount of NZD in whole cents, never negative.

| Member                                 | Behaviour                                                                                       |
|----------------------------------------|-------------------------------------------------------------------------------------------------|
| `Money(decimal amount)`                | Rejects negatives (`ArgumentOutOfRangeException`) and fractions of a cent (`ArgumentException`) |
| `Amount`, `Money.Zero`                 | The value; $0.00                                                                                |
| `+`                                    | Adds two amounts                                                                                |
| `* int`                                | Multiplies by a whole number (rate × days)                                                      |
| `PercentageRoundedUp(decimal percent)` | e.g. 10% of $164.81 = $16.481 → **$16.49**                                                      |

**`PriceList`.** The business's prices (4 daily rates, a one-way fee, a deposit percentage), supplied from outside the
domain. It rejects a $0 daily rate and a deposit percentage outside 0–100.

`Quote(CarType chargedType, DateRange period, City pickupCity, City dropOffCity)` returns a `PriceQuote`; it throws for
an undefined car type. *(Later refactored to `Quote(CarType chargedType, Itinerary itinerary)`; see 1.5b.)*

**`PriceQuote`: a `sealed record` with an `internal` constructor.** The US2 breakdown: `DailyRate`, `Days`,
`RentalPrice`, `OneWayFee`, `Deposit`, and a calculated `Total`. Only `PriceList.Quote` can create one.

### Design decisions

| Decision                        | Chosen                                              | Alternatives rejected                                                        | Why                                                                                                                                                                                                              |
|---------------------------------|-----------------------------------------------------|------------------------------------------------------------------------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Number type                     | `decimal`                                           | `double`                                                                     | `0.1 + 0.2 != 0.3` in binary floating point (`double`, and every JS number); `decimal` stores base-10 digits exactly                                                                                             |
| Where prices live               | A `PriceList` object, numbers supplied from outside | Hard-coded in a `switch`                                                     | Price changes shouldn't need a deploy (config arrives in Phase 4). Tests build their own price lists, so they don't break when prices change. The real prices appear in exactly one test: the user-story example |
| Money representation            | A `Money` type, **chosen as a teaching exercise**   | Plain `decimal`                                                              | With one currency and no extra rules, plain `decimal` would also have been reasonable. `Money` earns its keep by guaranteeing whole cents and making rounding explicit                                           |
| `Money` kind                    | `readonly record struct`                            | `sealed record` (class)                                                      | Small, immutable, and `default(Money)` is $0.00, a valid amount, so the struct's skipped-constructor problem doesn't apply                                                                                       |
| Currency field                  | None (NZD implied)                                  | `string Currency`                                                            | A reference-type field in a struct is `null` in `default(Money)`. Revisit if a second currency arrives                                                                                                           |
| Rounding                        | **Always up** to the cent (ceiling)                 | Nearest cent ("school" rounding); banker's rounding (`Math.Round`'s default) | Product-owner decision: the business never under-collects                                                                                                                                                        |
| Sub-cent amounts                | Impossible: `Money` rejects them                    | Allow, round at display time                                                 | Every amount is exact cents, so the only rounding happens in one method whose name says so                                                                                                                       |
| Result shape                    | `PriceQuote` breakdown with a calculated `Total`    | A single `decimal` total                                                     | US2 shows every line; a calculated total can't disagree with them                                                                                                                                                |
| Who creates quotes              | `internal` constructor; only `PriceList.Quote`      | Public constructor                                                           | Nobody outside the domain can make up a quote with an inconsistent deposit                                                                                                                                       |
| Where `Quote` lives             | A method on `PriceList`                             | A separate `PricingCalculator`; a static helper                              | The data and the behaviour that uses it sit together                                                                                                                                                             |
| "Any available" → Standard rate | Deferred to `Booking` (1.5)                         | Inside pricing                                                               | The idea of a *selection* (specific car / type / any) is born in `Booking`. `Quote` takes the type to charge                                                                                                     |
| 1–30 day limit                  | Not in pricing                                      | Validating in `Quote`                                                        | It's a rental rule (`Booking`). Pricing will quote any `DateRange`                                                                                                                                               |

### How the tests were designed

Price-list tests use **made-up, distinct rates** (10/20/30/40, fee 7), so a wrong lookup shows immediately and the tests
don't depend on real prices. One test uses the real agreed prices to check the user-story example.

| Test                                                                          | Wrong code it catches                                                                                            |
|-------------------------------------------------------------------------------|------------------------------------------------------------------------------------------------------------------|
| Whole cents accepted, including **`16.500m`**                                 | Rejecting zero; checking `decimal.Scale` instead of the value                                                    |
| Fractions of a cent / negatives rejected                                      | Missing checks                                                                                                   |
| `+`, `* int`                                                                  | Operator code returning the wrong value                                                                          |
| `PercentageRoundedUp`: $164.81 → **$16.49**                                   | Rounding to the nearest cent ($16.48); banker's rounding; rounding to whole dollars                              |
| A zero rate rejected, once per car type                                       | Forgetting to validate one of the four rates                                                                     |
| Deposit % of −1 / 101 rejected, 0 / 100 accepted                              | Off-by-one boundaries                                                                                            |
| `DailyRate` is the charged type's rate (all 4 types)                          | Swapped arms in the `switch`                                                                                     |
| Undefined car type rejected                                                   | A fallback that quietly charges the Economy rate                                                                 |
| Rental price = rate × days                                                    | Off-by-one days; rental price equal to the daily rate                                                            |
| One-way fee charged / not charged (drop-off city from `City.FromCode("akl")`) | Fee always or never applied. **This is where city equality is tested**, through the behaviour that depends on it |
| Deposit is 10% of the rental only ($9, not $9.70)                             | Deposit calculated on rental + fee                                                                               |
| `Quote`'s deposit rounds up                                                   | `Quote` doing its own rounding                                                                                   |
| Total = 90 + 7 + 9 = 106                                                      | A line missing from the total                                                                                    |
| User-story example = $446.50                                                  | Anything wrong, with the real prices                                                                             |

Red → green sequence:

1. Tests written → compile error **CS0246**: `Money`, `PriceList`, `PriceQuote` not found.
2. Stubs → **33 failed**; the 53 existing tests still passed.
3. Implementation → **all 88 passed** (86 Domain + 2 placeholders).
4. **Each tricky test was checked by putting its bug back.** All were caught:

| Bug put back                                   | Caught by                                            |
|------------------------------------------------|------------------------------------------------------|
| Rounding to the nearest cent                   | The $164.81 rows (`Money` and `Quote`)               |
| `amount.Scale > 2` instead of comparing values | The `16.500m` row                                    |
| Deposit on rental + one-way fee                | Deposit, total and user-story tests                  |
| SUV charged at the Premium rate                | The SUV row of the rate theory, plus dependent tests |
| `switch` without the `_ =>` arm                | The **compiler**: CS8524                             |

### C# lessons learned

- **`decimal` for money.** `0.1 + 0.2 == 0.3` is `false` for `double` and `true` for `decimal`. Decimal literals need
  the `m` suffix: `decimal rate = 55.99;` is a compile error (CS0664).
- **`decimal` keeps trailing zeros.** `16.5m == 16.500m`, but their *scale* (number of stored decimal places) differs,
  and `ToString()` prints `16.500`. Check precision by comparing values (`decimal.Round(x, 2) != x`), never by `Scale`.
- **Rounding modes.**
    - `Math.Round` / `decimal.Round` default to **banker's rounding** (`ToEven`): 16.485 → 16.48. Always pass a
      `MidpointRounding` explicitly for money.
    - ⚠️ `MidpointRounding.ToPositiveInfinity`, `ToNegativeInfinity` and `ToZero` are **not** midpoint rules, despite
      the enum's name. They always round in one direction. `ToPositiveInfinity` is ceiling.
- **Operator overloading.** `public static Money operator +(Money, Money)` defines `+` for our type (not possible for
  user classes in PHP, or at all in JS). Operators aren't automatically symmetric: `Money * int` doesn't give you
  `int * Money`. Leaving out `Money * decimal` was deliberate, so all fractional multiplication goes through the method
  that says how it rounds.
- **`readonly record struct`** gives value semantics, value equality, and immutability. Use an explicit constructor +
  `{ get; }` rather than a positional record, so `with` can't skip validation.
- **Switch expressions** (`x switch { A => …, _ => … }`) return values, like PHP 8's `match`. On an enum, the compiler
  **requires** a catch-all (CS8524), because any integer could arrive. But that same catch-all silences any warning when
  a new enum value is added later, so search for `switch`es whenever you add one.
- **`internal`** means visible within the same project (assembly) only. An `internal` constructor means other layers,
  and the test project, can't create the type; only domain code can.
- **Built-in numeric guards:** `ArgumentOutOfRangeException.ThrowIfNegative`, `ThrowIfZero` and `ThrowIfGreaterThan`
  (.NET 8+) work for any numeric type via *generic math*.
- **Calculated properties** (`public Money Total => …`) can't get out of sync with what they're calculated from.
- **Test-side features:**
    - **`decimal` can't go in attributes**, so decimal theory rows use `TheoryData<decimal>`. Yet `decimal` *can* be an
      optional parameter's default (`decimal economy = 10m`): two different "constant" rules.
    - **`int` → `decimal` converts automatically** (no information lost), so `[InlineData]` rows of `int`s can feed
      `decimal` parameters. The reverse needs an explicit cast.

---

## 1.5a Driver (`Driver`, `DriverLicence`, `LicenceType`, `EmailAddress`)

**Files:** `src/CarRental.Domain/Drivers/Driver.cs`, `src/CarRental.Domain/Drivers/DriverLicence.cs`, `src/CarRental.Domain/Drivers/LicenceType.cs`, `src/CarRental.Domain/Drivers/EmailAddress.cs`;
`tests/CarRental.Domain.Tests/Drivers/DriverTests.cs`, `tests/CarRental.Domain.Tests/Drivers/DriverLicenceTests.cs`, `tests/CarRental.Domain.Tests/Drivers/EmailAddressTests.cs`

`Booking` was split into two cycles: **1.5a Driver** (who is driving) and **1.5b Booking** (the rental itself), to keep
each cycle a manageable size.

### What was built

| Type                                                    | Kind                    | Rules                                                                                                                                                                        |
|---------------------------------------------------------|-------------------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `LicenceType`                                           | `enum`                  | `Learner = 1`, `Restricted = 2`, `Full = 3`. Starts at 1 so `default` isn't a real type                                                                                      |
| `DriverLicence`                                         | `sealed partial record` | `Number`: NZ format, 2 letters + 6 digits (lower case accepted, stored upper case). `Type`: must be defined. `ExpiryDate`: the last valid day. Any licence stage is accepted |
| `DriverLicence.PermitsRentalUntil(DateOnly returnDate)` | method                  | `true` only for a **full** licence whose expiry is **on or after** the return date                                                                                           |
| `EmailAddress`                                          | `sealed record`         | Email-shaped only: text before an `@` and text after it. Stored exactly as entered                                                                                           |
| `Driver`                                                | `sealed record`         | `FullName` (as on the licence; not blank, at most 100 characters), `Email`, `Licence`                                                                                        |

Every text input rejects leading/trailing whitespace (see "Strict domain" below).

### Design decisions

| Decision                                           | Chosen                                                              | Alternatives rejected                                                 | Why                                                                                                                                                                                                                         |
|----------------------------------------------------|---------------------------------------------------------------------|-----------------------------------------------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Learner / restricted licences                      | **Valid data**; rejected only for renting, via `PermitsRentalUntil` | Constructor rejects non-full licences                                 | "Is this a well-formed licence?" and "does it allow renting until the return date?" are different questions. The second needs the rental dates, which a licence doesn't know. `Booking` (1.5b) decides what a `false` means |
| Expiry on the return date                          | Accepted (`>=`)                                                     | Rejected (`>`)                                                        | The user story rejects a licence that expires *before* the return date                                                                                                                                                      |
| `Driver`                                           | Value object (`record`)                                             | Entity (`Customer` with an ID)                                        | No customer accounts: a driver is just "who is driving, for this booking". The same person booking twice is two sets of details. A `Customer` entity would arrive with accounts                                             |
| Name                                               | One `FullName`, as on the licence                                   | `PersonName` with given/family names                                  | A single value with simple rules and no derived behaviour (the five questions say: a validated string). Given/family order varies across cultures, and some people have one name                                            |
| Email checks                                       | Email-shaped only                                                   | Dot in the domain, 254-character limit; `System.Net.Mail.MailAddress` | Product-owner decision: no emails are sent, so a correct address is the customer's responsibility. `MailAddress` parses mail *headers* and accepts display-name forms like `Bob <bob@example.com>`                          |
| Email case                                         | Stored as entered                                                   | Lower-cased                                                           | The part before `@` is technically case-sensitive                                                                                                                                                                           |
| Licence number format                              | NZ: 2 letters + 6 digits                                            | Free text                                                             | Product-owner confirmed                                                                                                                                                                                                     |
| Surrounding whitespace                             | **Rejected** everywhere                                             | Trimmed in the domain                                                 | Project-wide rule (now in `AGENTS.md`): **strict domain, forgiving web layer**. The Razor form (Phase 5) trims input before calling the domain                                                                              |
| Shared "no surrounding whitespace" helper          | Not extracted (2 copies)                                            | Extract now                                                           | Rule of three: extract on the third copy, once the real pattern is visible                                                                                                                                                  |
| `private static readonly` field naming (test code) | `_camelCase`, per our `.editorconfig`                               | `PascalCase`; the runtime's `s_camelCase`                             | Followed the existing rule, which the build enforced (IDE1006). Teams genuinely differ here; changing it is a one-line `.editorconfig` edit                                                                                 |

### How the tests were designed

| Test                                                                  | Wrong code it catches                                                                                                                                                                          |
|-----------------------------------------------------------------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Licence number stored upper case                                      | Not normalising case                                                                                                                                                                           |
| 14 invalid licence numbers                                            | Checking only the length; letters and digits in any order; `$` instead of `\z`; `char.IsLetter` (`Ä`); upper-casing before validating (`ſ`); **`\d` instead of `[0-9]`** (Arabic-Indic digits) |
| Undefined `LicenceType` rejected                                      | No `Enum.IsDefined` check                                                                                                                                                                      |
| Learner / Restricted **accepted** by the constructor                  | Mixing "valid licence" with "allowed to rent"                                                                                                                                                  |
| Full licence expiring after / **on** / the day before the return date | `>` instead of `>=`                                                                                                                                                                            |
| Learner / Restricted don't permit renting                             | Checking the expiry but forgetting the type                                                                                                                                                    |
| Email kept exactly as entered (`Jane.Smith@Example.com`)              | Lower-casing                                                                                                                                                                                   |
| `a@b` accepted                                                        | Checks beyond the agreed rule                                                                                                                                                                  |
| No `@`, nothing before or after it                                    | `Contains('@')` alone                                                                                                                                                                          |
| Edge spaces rejected (email, name)                                    | `Trim()` instead of rejecting                                                                                                                                                                  |
| Blank name rejected                                                   | `IsNullOrEmpty` instead of `IsNullOrWhiteSpace`                                                                                                                                                |
| Name of 100 accepted, 101 rejected                                    | Off-by-one (`>=`)                                                                                                                                                                              |

Red → green sequence:

1. Tests written. The compile check against temporary stand-in types failed on **IDE1006** in the *test* code: a
   `private static readonly` field named `ReturnDate` broke our `_camelCase` rule. Renamed to `_returnDate`.
2. While planning the implementation, `\d` was spotted as a trap, so an Arabic-Indic-digits test case was **added before
   any code**.
3. Stubs → **41 failed**; the 86 existing tests still passed.
4. Implementation → **all 129 passed** (127 Domain + 2 placeholders).
5. **Each tricky test was checked by putting its bug back.** All were caught:

| Bug put back                  | Caught by                            |
|-------------------------------|--------------------------------------|
| `\d` instead of `[0-9]`       | The Arabic-Indic digits case         |
| `>` instead of `>=` on expiry | The "expires on the return date" row |
| Licence-type check forgotten  | The Learner and Restricted rows      |
| Email: `Contains('@')` only   | `@example.com`, `jane@`              |
| Name: `>=` instead of `>`     | The exactly-100-characters test      |

### C# lessons learned

- **Valid data vs permitted action.** Constructors check that data is well formed; business permissions that depend on
  context (like rental dates) are methods, called by whoever has that context.
- **Regex shorthand classes are Unicode-aware in .NET.** `\d` matches any Unicode digit (`١٢٣`, `१२३`, …), and `\w`
  matches accented and non-Latin letters. When a rule means ASCII, spell it out: `[0-9]`, `[A-Za-z]`.
- **`T?` means two different things.**
    - On a value type, `DateOnly?` is `Nullable<DateOnly>`: a real wrapper struct with `HasValue` / `Value`.
    - On a reference type, `City?` is only a hint to the compiler's null analysis.
- **`string ==` compares contents.** `string` is a reference type, but it defines `==` to compare characters (as `Money`
  defines `+`).
- **`Trim()` with no arguments removes all Unicode whitespace**, which makes `value != value.Trim()` a good "has
  surrounding whitespace" check.
- **Explaining variables.** `var hasTextBeforeAndAfterAt = atIndex > 0 && atIndex < value.Length - 1;` turns index
  arithmetic into a readable rule.
- **Separate guard clauses, separate messages.** Blank, edge whitespace and too-long are checked separately, so the
  error says *what* is wrong.
- **A record made of records** gets deep value equality automatically (not tested: that's C#).
- **`new string('a', 101)`** repeats a character, like PHP's `str_repeat`.
- **The `.editorconfig` applies to test code too.** It caught a naming inconsistency before any test ran.

---

## 1.5b `Booking` (with `Itinerary`, `CarSelection`, `Result`, `BookingReference`, `BookingError`)

**Files:** `src/CarRental.Domain/Bookings/Booking.cs`, `src/CarRental.Domain/Common/Itinerary.cs`, `src/CarRental.Domain/Bookings/CarSelection.cs`, `src/CarRental.Domain/Common/Result.cs`, `src/CarRental.Domain/Bookings/BookingReference.cs`,
`src/CarRental.Domain/Bookings/BookingError.cs`; tests in `tests/CarRental.Domain.Tests/Bookings/BookingTests.cs`, `tests/CarRental.Domain.Tests/Common/ItineraryTests.cs`, `tests/CarRental.Domain.Tests/Bookings/CarSelectionTests.cs`, `tests/CarRental.Domain.Tests/Common/ResultTests.cs`,
`tests/CarRental.Domain.Tests/Bookings/BookingReferenceTests.cs`

### What was built

| Type                     | Kind                       | Purpose                                                                                                                                                                           |
|--------------------------|----------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `Itinerary`              | positional `sealed record` | Where and when: `PickupCity`, `DropOffCity`, `Period`. `IsOneWay` is the single home of the one-way rule                                                                          |
| `CarSelection`           | closed family of records   | What the customer chose: `SpecificCar(Guid CarId)`, `OfType(CarType Type)`, `AnyAvailable`. `Matches(car)` and `ChargedTypeFor(car)` ("any available" → always the Standard rate) |
| `BookingReference`       | `sealed partial record`    | Customer-facing reference: 6 characters with no look-alikes (no `0`/`O`, `1`/`I`/`L`); lower case accepted, stored upper case                                                     |
| `BookingError`           | `enum`                     | `RentalLongerThan30Days`, `PickupDateInPast`, `LicenceDoesNotPermitRental`, `CarNotBasedInPickupCity`, `CarDoesNotMatchSelection`                                                 |
| `Result<TValue, TError>` | generic `sealed class`     | A value, or every reason it couldn't be produced                                                                                                                                  |
| `Booking`                | entity (`sealed class`)    | A confirmed rental of one car                                                                                                                                                     |

**`Booking`**

| Member                                                                           | Behaviour                                                                                                                                                                       |
|----------------------------------------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `Booking.Create(reference, car, selection, itinerary, driver, priceList, today)` | Returns `Result<Booking, BookingError>`. Checks every rule and reports **all** failures. On success, prices the itinerary at the selection's charged type and stores that quote |
| `Id`, `Reference`, `CarId`, `Itinerary`, `Driver`, `Price`                       | Fixed at creation; a booking never changes                                                                                                                                      |
| `BlockedPeriod`                                                                  | `[Start, End + 1 + break)`: break = 1 day cleaning, or 7 days relocation if one-way                                                                                             |
| `ClashesWith(other)`                                                             | Same car **and** overlapping blocked periods                                                                                                                                    |

| Rule                                                     | Error                        |
|----------------------------------------------------------|------------------------------|
| More than 30 days                                        | `RentalLongerThan30Days`     |
| Pickup before today                                      | `PickupDateInPast`           |
| `driver.Licence.PermitsRentalUntil(returnDate)` is false | `LicenceDoesNotPermitRental` |
| Car's **home** city ≠ pickup city                        | `CarNotBasedInPickupCity`    |
| `selection.Matches(car)` is false                        | `CarDoesNotMatchSelection`   |

### Design decisions

| Decision                | Chosen                                                                 | Alternatives rejected                                                               | Why                                                                                                                                                                                                                             |
|-------------------------|------------------------------------------------------------------------|-------------------------------------------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Split the cycle         | 1.5a Driver, then 1.5b Booking                                         | One large cycle                                                                     | Each cycle stays small enough to follow                                                                                                                                                                                         |
| Business-rule failures  | `Result<Booking, BookingError>` with all errors collected              | Throw a domain exception                                                            | These are expected outcomes, not bugs. The return type shows that creation can fail, callers must handle it, and the customer hears about every problem at once. Programming bugs (undefined enums, invalid ranges) still throw |
| `Result` implementation | Our own small generic type                                             | ErrorOr, FluentResults                                                              | The domain has no package references                                                                                                                                                                                            |
| Error type              | `enum BookingError`                                                    | A record per error carrying details (e.g. the expiry date)                          | No user story needs details yet. Customer wording belongs in the web layer. Moving to records later is mechanical                                                                                                               |
| Customer's choice       | Closed family of records (`CarSelection`)                              | An enum plus nullable `CarId?` / `CarType?` fields                                  | Each kind carries exactly its own data, so nonsense combinations can't be expressed                                                                                                                                             |
| Selection rules         | `Matches` / `ChargedTypeFor` on `CarSelection`, using pattern matching | A `switch` inside `Booking`; abstract methods overridden per subtype (polymorphism) | Keeps `Booking` focused and the selection rules in one readable place. Polymorphism would make a new kind a compile error until implemented, which is a reasonable alternative                                                  |
| "Today"                 | A `DateOnly today` parameter                                           | `TimeProvider` inside the domain                                                    | The domain stays pure and tests choose any date. Phase 2 works out "today in NZ" and passes it in                                                                                                                               |
| Booking reference       | Value object passed **in** to `Create`                                 | Generated inside the domain                                                         | Generating one needs randomness and a uniqueness check against the database (Phases 2–3)                                                                                                                                        |
| Price                   | Quoted once and **stored**                                             | Recalculated when needed                                                            | Later price changes must not alter an agreed booking                                                                                                                                                                            |
| Availability city       | Car's **home** city                                                    | Current city                                                                        | After a one-way rental, the car is relocated home (user stories)                                                                                                                                                                |
| Clashes                 | Blocked periods overlap, same car                                      | Rental periods overlap                                                              | One rule covers both directions: a new rental landing in another's break, and its own break running into the next rental                                                                                                        |
| Strongly-typed IDs      | Still plain `Guid`                                                     | `CarId`, `BookingId`                                                                | `Create` takes a `Car` object, not an ID, so the mix-up risk is low inside the domain. Revisit in Phase 2                                                                                                                       |
| 9 parameters            | Introduced `Itinerary` → 7                                             | Builder; a `BookingRequest` parameter object now                                    | See below                                                                                                                                                                                                                       |

### The 9-parameter smell and `Itinerary`

The first design of `Create` had 9 parameters. The real smell wasn't the count but a **data clump**: `period`,
`pickupCity` and `dropOffCity` appeared together in both `PriceList.Quote` and `Booking.Create`, and both would have
worked out "is it one-way?" separately.

Giving the clump a name (*Introduce Parameter Object*) produced `Itinerary`: a real business concept with behaviour
(`IsOneWay`). `Create` went to 7 parameters and `Quote` from 4 to 2. The remaining parameters are genuinely different
things, so they were left alone; Phase 2's "create booking" command will group the customer's request naturally.
Builders were rejected because every parameter is required.

**When to fix a smell like this:** fix it at design time if you can see it (the API shapes every test), otherwise
refactor later under the protection of the tests.

`Itinerary` is a **positional** record, unlike `DateRange` and `Money`: it has no rules of its own, so a `with`
expression skipping the constructor can't create an invalid one.

### Refactoring `PriceList.Quote`: expand / migrate / contract

`Quote(chargedType, period, pickupCity, dropOffCity)` became `Quote(chargedType, itinerary)` without the build ever
going red:

1. **Expand:** add the new `Quote(CarType, Itinerary)`; the old one delegates to it. → green
2. **Migrate:** move every caller (the pricing tests) to the new method. → green
3. **Contract:** delete the old method. → green

In a larger codebase step 2 can take a long time; marking the old method `[Obsolete("Use …")]` gives every remaining
caller a compiler warning. The pricing tests changed **shape** (they now build an `Itinerary`) but not **meaning**:
every one still checks the same rule. Putting bugs back afterwards confirmed it: `Quote` ignoring `IsOneWay` and
`IsOneWay` inverted were each caught by that layer's own tests.

### How the tests were designed

Each failure test asserts the **exact** list of errors (`ShouldBe([BookingError.X])`), so one problem can't cause a
knock-on second error.

| Test                                                                                                                       | Wrong code it catches                                                 |
|----------------------------------------------------------------------------------------------------------------------------|-----------------------------------------------------------------------|
| `IsOneWay` true / false (drop-off from `City.FromCode("akl")`)                                                             | Always true/false; wrong comparison. City equality is now tested here |
| `Result`: success has no errors; failure keeps all errors                                                                  | `null` errors; keeping only the first                                 |
| `Result.Value` on failure throws                                                                                           | Quietly returning `default`                                           |
| `Result.Failure([])` throws                                                                                                | A failure that can't say why                                          |
| Reference: no `0`, `O`, `1`, `I`, `L`, also in lower case; `\n`; `ſ`                                                       | Wrong alphabet; `$`; upper-casing first                               |
| Specific car matches only that car; type matches only that type; "any" matches all                                         | Comparing the wrong thing                                             |
| "Any available" charges Standard **even for an Economy car**                                                               | A plausible "fair" `min(...)` rule that isn't the business's          |
| 30 days OK / 31 fails                                                                                                      | `>=` instead of `>`                                                   |
| Pickup today OK / yesterday fails                                                                                          | Rejecting same-day bookings                                           |
| Licence expiring the 12th, rental 10th → 13th, fails                                                                       | Checking the licence against the **pickup** date                      |
| Car based in Auckland but currently in Wellington succeeds                                                                 | Checking `CurrentCity` instead of `HomeCity`                          |
| An SUV booked as "any available" is charged 20, not 30                                                                     | Pricing at `car.Type`, ignoring the selection                         |
| One-way fee present in the stored price                                                                                    | Quoting a made-up same-city trip                                      |
| Several problems → all reported                                                                                            | Stopping at the first failure                                         |
| Blocked period +1 cleaning day / +7 relocation days                                                                        | Forgetting the return day; ignoring one-way                           |
| `ClashesWith` rows: during the rental, on the cleaning day, the day after, returned the day before, returned 2 days before | Missing the break; off-by-one at either edge                          |
| Different car never clashes                                                                                                | Comparing dates but not cars                                          |
| After a one-way rental, the 20th clashes and the 21st doesn't                                                              | Using the cleaning break for one-way rentals                          |

Red → green sequence:

1. `Itinerary`: tests → CS0246 → stub (2 failed) → implementation (green) → `Quote` refactor (green throughout).
2. Booking tests written; a placeholder row left in the `ClashesWith` theory while drafting was replaced before anything
   ran. Compile-checked against temporary stand-ins.
3. While implementing, a claim made during the walkthrough was **tested and found wrong**: a record with a private
   constructor is *not* fully closed, because records must keep a `protected` copy constructor that an outside record
   can chain to. The code handles unknown kinds explicitly (`NotSupportedException`) instead of assuming they can't
   exist.
4. Stubs → **59 failed**; the 129 existing tests still passed.
5. Implementation → **all 190 passed** (188 Domain + 2 placeholders).
6. **Each tricky test was checked by putting its bug back.** The first attempt's script used `for f in $FILES` under
   **zsh**, which doesn't split unquoted variables on spaces, so the backups and restores silently failed and the bugs
   piled up. The files were restored by hand, and the checks re-run under `bash` with a comparison against the backups
   after every step. All 12 bugs were then caught:

| Bug put back                            | Caught by                                    |
|-----------------------------------------|----------------------------------------------|
| 30-day limit `>=`                       | 30-day boundary test                         |
| Same-day pickup rejected                | Pickup-today test                            |
| Licence checked against the pickup date | Licence-expires-during-rental test           |
| `CurrentCity` instead of `HomeCity`     | Based-here-but-currently-elsewhere test      |
| Only the first error reported           | Several-problems test                        |
| Priced at `car.Type`                    | "Any available" pricing test                 |
| Blocked period forgets the return day   | Both blocked-period tests + 3 clash rows     |
| `ClashesWith` ignores the car           | Different-car test                           |
| "Any available" at the car's own rate   | `CarSelection` theory + booking pricing test |
| `Value` returns default on failure      | `Failure_AccessingValue_Throws`              |
| Failure allowed with no errors          | `Failure_WithNoErrors_Throws`                |
| Reference alphabet allows `L`           | `KLQM4X` row                                 |

### C# lessons learned

- **Static factory methods** (`Booking.Create`, `Result.Success` / `Failure`) can return anything, including a result
  object; a constructor can only produce an instance or throw. Pair them with a private constructor so the factory is
  the only way in.
- **Generics.** `Result<TValue, TError>` is checked at compile time, and unlike TypeScript the type arguments **exist at
  runtime** (`List<int>` and `List<string>` are different types). `TValue?` on an unconstrained type parameter means
  "may hold `default`".
- **The `!` operator** is justified only when you know something the compiler can't see, and it deserves a comment
  saying what (here: a success always received a real value).
- **Spread** `[.. errors]` copies a sequence into a collection. Copying a caller's `IEnumerable` once protects against
  re-running a lazy LINQ query (Phase 2 topic).
- **Pattern matching.** `this switch { SpecificCar specific => … }` checks the runtime type and gives a typed variable;
  `SpecificCar or OfType => …` combines patterns. Switch expressions also support constant, relational (`<= 30`),
  logical (`and`/`or`/`not`), property (`{ Type: CarType.Suv }`), tuple and list patterns, and the same patterns work
  with `is`. Unlike PHP's `match`, they go beyond `===` comparisons; unlike JS `switch`, they never fall through.
- **Closed families of records** are C#'s usual stand-in for discriminated unions. Nested types can use the outer type's
  private constructor, which prevents *accidental* subtypes, but a record's required `protected` copy constructor means
  it isn't airtight.
- **Pattern matching vs polymorphism.** A `switch` keeps all rules in one place but only fails at runtime for a new
  kind; abstract methods make a new kind a compile error until implemented.
- **The colon** means "inherits from / implements" (`record SpecificCar(...) : CarSelection`), covering both PHP's
  `extends` and `implements`. One base class at most, then any number of interfaces.
- **Positional records** are fine when there are no rules to protect (`Itinerary`); use an explicit constructor +
  get-only properties when there are (`DateRange`, `Money`).
- **`??=`** assigns only if the variable is currently null (as in PHP 7.4+).
- **Expand / migrate / contract** changes a widely used method without ever breaking the build.
- **Shell scripts:** zsh doesn't split unquoted variables on spaces the way bash does. Check that any automated "safety"
  step actually did what it claims.

---

## Phase 1 summary

The domain model is complete: **188 domain tests**, no references to databases, web frameworks or packages.

After Phase 1 the flat file list was reorganised into business-area folders (`Common/`, `Fleet/`, `Drivers/`, `Pricing/`, `Bookings/`) with matching namespaces, and the tests mirrored them. Dependencies between areas point one way, `Common ← Fleet, Drivers, Pricing ← Bookings`, which is why `Itinerary` lives in `Common`: in `Bookings` it would make Pricing and Bookings depend on each other. The convention is recorded in `AGENTS.md`.

| Concept                                             | Kind                           | Key rule                                                                          |
|-----------------------------------------------------|--------------------------------|-----------------------------------------------------------------------------------|
| `DateRange`                                         | value object (`sealed record`) | Half-open `[Start, End)`; `Days`; `Overlaps`                                      |
| `City`                                              | smart enum                     | Three cities; case-insensitive `FromCode`                                         |
| `Car`                                               | entity                         | Identity by `Guid`; `HomeCity` fixed, `CurrentCity` changes via `RecordArrivalIn` |
| `CarType`, `LicenceType`, `BookingError`            | enums                          | Start at 1; checked with `Enum.IsDefined` where they enter the domain             |
| `Registration`, `DriverLicence`, `BookingReference` | value objects                  | Strict ASCII formats; validate, then normalise                                    |
| `Money`                                             | `readonly record struct`       | Whole cents, never negative; `PercentageRoundedUp`                                |
| `PriceList` / `PriceQuote`                          | class / record                 | Rate × days + one-way fee + deposit (ceiling)                                     |
| `EmailAddress`, `Driver`                            | value objects                  | Email-shaped; strict whitespace                                                   |
| `Itinerary`                                         | positional record              | `IsOneWay`                                                                        |
| `CarSelection`                                      | closed record family           | Matching and the "any available → Standard" rule                                  |
| `Result<TValue, TError>`                            | generic class                  | Business-rule failures without exceptions                                         |
| `Booking`                                           | entity                         | All rental rules; blocked period; `ClashesWith`                                   |

**Carried into Phase 2:** working out "today in NZ" with `TimeProvider`; generating booking references; availability
search and double-booking checks using `ClashesWith`; whether to introduce strongly-typed IDs; the "create booking"
command that groups the customer's request.
