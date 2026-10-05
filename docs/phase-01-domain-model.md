# Phase 1 — Domain model

The domain model holds the business rules from [user-stories.md](user-stories.md), in `CarRental.Domain`, with no dependencies on databases, web frameworks or packages. Each concept below was built with the design → tests → implement → document cycle, in dependency order:

| # | Concept | Status |
|---|---|---|
| 1.1 | `DateRange` | ✅ Done |
| 1.2 | `City` | ✅ Done |
| 1.3 | `Car` (with `CarType`, `Registration`) | ✅ Done |
| 1.4 | Pricing (`Money`, `PriceList`, `PriceQuote`) | ✅ Done |
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

- **No test just reads back a value it set.** The `Days` tests cover the case naive day maths gets wrong: crossing a month end (30 Oct → 2 Nov, where `End.Day - Start.Day` gives −28).
- **Every overlap case is checked from both sides** (`a.Overlaps(b)` and `b.Overlaps(a)`), so an implementation that only handles one direction can't pass.
- **The back-to-back cases encode the half-open decision.** If `Overlaps` is ever "fixed" to include the end date, they fail.
- **`Should.Throw<ArgumentException>` requires that exact type.** In the red run, the stub threw `NotImplementedException` and the constructor tests still failed. That proves they check *which* exception, not just that one occurred.

Red → green sequence:

1. Tests written, no `DateRange` exists → **compile error CS0246**. In C#, a missing type is the first "red"; nothing runs until everything the tests reference exists.
2. A stub with `throw new NotImplementedException()` everywhere → compiles, **16 failed**, each as its own test failure.
3. The real implementation → **16 passed**.

#### Test review: removing tests that tested C#

After implementation we reviewed every test with one question: *can a plausible wrong implementation of our code make this test fail?* (Now a rule in `AGENTS.md`.) Three tests failed that check and were removed, taking the suite from 16 to 13:

| Removed test | Why |
|---|---|
| `Equality_WhenSameDates_AreEqual` | Tested the compiler-generated `record` equality. The only way to fail it is to change `record` to `class`. Nothing in the app compares `DateRange`s with `==`, so no behaviour depends on it |
| `Equality_WhenDifferentDates_AreNotEqual` | Same: tested the generated `!=` |
| `Days_WhenRangeCrossesLeapDay_IncludesFebruary29th` | Any implementation that passes the month-end test already uses .NET's calendar maths, so this only tested .NET's handling of leap years |

The equality tests had been written partly to *demonstrate* value equality. Teaching demonstrations now stay in the conversation, never in the test suite.

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
- **City equality is not tested here.** The one-way pricing rule depends on comparing two cities, but that's a pricing behaviour, so it's tested in the pricing tests (1.4). A test `FromCode_ReturnsCityEqualToStaticInstance` existed briefly and was removed in the test review (below).

Red → green sequence:

1. Tests written → compile error **CS0246**: `City` not found.
2. Stub (everything throws `NotImplementedException`) → the 10 City tests fail; the 16 `DateRange` tests still pass.
3. Implementation → all pass. The `[MemberData]` theory now shows as 3 rows instead of 1 (see lesson below).

**Test review.** `FromCode_ReturnsCityEqualToStaticInstance` was removed. It checked that `FromCode("AKL")` equals `City.Auckland`, which `FromCode_WithKnownCode_ReturnsThatCity` already checks. Its only extra claim was that the compiler-generated `==` agrees with `Equals`, which is testing C#.

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

---

## 1.3 `Car` (with `CarType` and `Registration`)

**Files:** `src/CarRental.Domain/Car.cs`, `CarType.cs`, `Registration.cs`; `tests/CarRental.Domain.Tests/CarTests.cs`, `RegistrationTests.cs`

### What was built

**`Car`: the first entity.** A car is defined by its identity, not its details: two white Economy Corollas in Auckland are two different cars, and a car stays the same car when its data changes.

| Member | Behaviour |
|---|---|
| `Car(Registration, make, model, CarType, City homeCity)` | Rejects blank make/model (`ArgumentException`) and undefined car types (`ArgumentOutOfRangeException`) |
| `Id` | A new time-ordered `Guid` per car, created in the constructor |
| `Registration`, `Make`, `Model`, `Type`, `HomeCity` | Fixed at construction |
| `CurrentCity` | Starts as `HomeCity`; the only property that can change |
| `RecordArrivalIn(City)` | Changes `CurrentCity`; never `HomeCity` |

**`CarType`: a plain enum.** `Economy = 1`, `Standard = 2`, `Suv = 3`, `Premium = 4`. Daily rates are deliberately not here; they belong to pricing (1.4).

**`Registration`: a value object for NZ number plates.**

| Rule | Example |
|---|---|
| 1–6 characters, **spaces count** | `AB 123` ✅, `ABC 123` ❌ |
| Letters A–Z and digits 0–9 only | `ABC-12` ❌, `ÄBC12` ❌, `ſAB12` ❌ |
| Single spaces only, and only between characters | `A B C` ✅, `AB  12` ❌, ` AB12` ❌, `AB12 ` ❌, `AB\t12` ❌ |
| Lower case is accepted and stored upper case | `abc123` → `ABC123` |
| Spaces are significant | `AB 12` and `AB12` are different plates |

### Design decisions

| Decision | Chosen | Alternatives rejected | Why |
|---|---|---|---|
| Identity | `Guid.CreateVersion7()`, created in the constructor | Database-generated `int`; number plate | The car has an ID before any database exists. Version 7 GUIDs are time-ordered, so database indexes stay efficient. Plates change (personalised plates move between cars), so they can't be an identity |
| Strongly-typed ID (`CarId`) | Not yet | Wrap the `Guid` in a `CarId` type | Prevents mixing up car and booking IDs, but adds mapping code in EF Core. Revisit if `Booking` makes it a real risk |
| Equality | Plain `sealed class`, reference equality; compare `Id` when needed | `record`; an `Entity` base class that overrides `Equals` | Record equality would make two identical-looking cars "equal", and its hash code would change when `CurrentCity` changes, silently breaking `HashSet`/`Dictionary` lookups. An `Entity` base class is subtle code we don't need yet |
| Car type | Plain `enum CarType`, values from 1 | Smart enum with a `DailyRate` | A car knows *what* it is; pricing knows what that *costs*. Prices change more often than types, and may move to configuration later |
| Current city | Stored; changed only via `RecordArrivalIn` | Calculated from bookings; left out | Matches the user stories' "home city and current city", ready for the future "rent onward" feature. Nothing reads it yet |
| Number plate | `Registration` value object | A validated `string` on `Car` | It has rules (characters, length, spacing) and normalisation (case), which earns a type of its own |
| Plate edge spaces | **Rejected** | Trimmed | Product-owner decision: callers must send a clean plate |
| Bookings | `Car` does **not** know its bookings | `car.Book(...)` checking clashes in memory | Loading a car shouldn't load years of bookings, and two simultaneous bookings can only be stopped reliably by the database (Phase 3). Clash checks go in the application layer (Phase 2) |

### Value object or plain properties? (the questions we used)

Extract a value object (record) when **any** of these is true:

1. Do the values belong together (always set and replaced as a unit)?
2. Is there a rule to enforce?
3. Is there behaviour derived from the values?
4. Does the business have a word for it?
5. Is it used by more than one entity? (A bonus, not a requirement.)

Keep plain properties when it's a single independent value with no rules: `Make` and `Model` are plain strings. A value object still *belongs to* its entity, and in Phase 3 EF Core can store it as columns in the entity's own table, so modelling it separately doesn't mean a separate table.

### How the tests were designed

Every test was checked against the `AGENTS.md` heuristic: **would a plausible wrong implementation of our code fail it?**

| Test | Wrong code it catches |
|---|---|
| Blank make / model rejected (`""`, `"   "`) | Checking for empty but not whitespace-only strings |
| Undefined `CarType` rejected (`0`, `42`) | No check; an enum starting at `0`, so `default` becomes a real type |
| Each car gets its own ID | `Id` never set (both `Guid.Empty`); a hard-coded ID |
| New car's current city is its home city | Forgetting to set `CurrentCity` (it would be `null` despite the nullable checks) |
| `RecordArrivalIn` changes current city / doesn't change home city | A method that does nothing, or changes the wrong property |
| Plate normalised to upper case, spaces kept | Removing spaces, which would quietly make `AB 12` and `AB12` the same plate |
| Plate boundaries (`HELLO1`, `AB 123` ✅ / `ABC1234`, `ABC 123` ❌) | Off-by-one length checks; not counting spaces |
| `AB  12`, ` AB12`, `AB\t12` rejected | Allowing spaces without checking their neighbours; `Trim()`; `char.IsWhiteSpace` (matches tabs and more) |
| `ÄBC12` rejected | `char.IsLetterOrDigit` (accepts any Unicode letter) |
| `AB12\n` rejected | Ending the regex with `$` instead of `\z` |
| `ſAB12` rejected | Upper-casing *before* validating (`ſ` becomes an ASCII `S`) |

Deliberately **not** tested: that `Make` returns what was passed in (tautological), and that two `Registration`s for the same plate are equal (record equality is C#; the normalisation tests cover what we control). `CarType` has no tests of its own: an enum has no behaviour, and its rules are tested through `Car`'s constructor.

Red → green sequence:

1. Tests written → compile error **CS0246**: `Car`, `CarType`, `Registration` not found.
2. While planning the implementation, two edge cases were found (`AB12\n` and a non-ASCII letter that upper-cases to ASCII). Following the workflow rule, **test cases were added before any code**.
3. Stubs → **29 failed**; the 24 existing tests still passed.
4. Implementation → **all 55 passed**.
5. **Each edge-case test was checked by putting its bug back** (`$` instead of `\z`; upper-casing first):
   - `AB12\n` failed as intended.
   - The original non-ASCII case, Turkish `ı`, **did not fail**. `ToUpperInvariant()` leaves `ı` unchanged (`ı` → `I` is a Turkish-culture rule, not an invariant one), so that test could never catch the bug. It was replaced with `ſ` (long s), which *does* become `S`, and the check then failed as intended.

### C# lessons learned

- **Entities vs value objects.** Entities have identity and change over time (`class`); value objects are defined by their values and never change (`record`). A record's hash code depends on its values, so a *mutable* record in a `HashSet` or `Dictionary` gets lost when it changes.
- **`partial`.** One type defined in several places, joined at compile time. Mainly used so **source generators** can add code to your types. `[GeneratedRegex]` on a `partial` method makes the compiler generate the regex matcher during the build; the type containing it must be `partial` too (otherwise CS0751). `partial` can't extend types from other projects; extension methods do that (Phase 2).
- **Regex in .NET:**
  - `$` also matches just before a trailing `\n`. Use `\z` for "absolute end".
  - `[A-Za-z]` is ASCII-only, but `RegexOptions.IgnoreCase` brings back Unicode surprises (e.g. the Kelvin sign `K` matching `k`).
- **`char.IsLetterOrDigit` and `char.IsWhiteSpace` are Unicode-wide.** They accept `Ä`, tabs, non-breaking spaces and more. For rules about specific ASCII characters, match those characters explicitly.
- **Validate raw input, then normalise.** `ToUpperInvariant()` can turn invalid input into valid-looking input (`ſ` → `S`).
- **Invariant culture ≠ Turkish culture.** `ToUpperInvariant()` doesn't do the `ı` → `I` mapping; only culture-specific upper-casing (e.g. Turkish) does.
- **Enum safety.** Start explicit values at 1 and check `Enum.IsDefined(value)` wherever an enum crosses into the domain.
- **Built-in guard clauses.** `ArgumentException.ThrowIfNullOrWhiteSpace(make)` uses `[CallerArgumentExpression]`: the compiler passes the argument's text (`"make"`) as the parameter name, so no `nameof` is needed.
- **`ArgumentOutOfRangeException`** is the convention for an argument of the right kind but outside the allowed values. Shouldly's `Should.Throw<T>` matches the exact type, so tests pin down which exception is thrown.
- **Acronyms in names.** Two letters stay upper case (`IO`); three or more become PascalCase (`Suv`, `Html`).
- **A property can share its type's name** (`public Registration Registration { get; }`), known as the "Color Color" case.
- **Entity methods name business events.** `RecordArrivalIn(city)`, not `SetCurrentCity(city)`, and the setter is `private`.
- **Test-side features:**
  - **Optional parameters** must have compile-time-constant defaults, so `City? homeCity = null` + `homeCity ?? City.Auckland` is the workaround.
  - **Named arguments** (`CreateCar(model: "")`) let each test change only the detail it's about.
  - **Enum values are constants,** so `[InlineData((CarType)42)]` is allowed.
- **Check that a test can fail.** Two of our edge-case tests looked equally convincing; putting the bug back showed one of them could never fail.

---

## 1.4 Pricing (`Money`, `PriceList`, `PriceQuote`)

**Files:** `src/CarRental.Domain/Money.cs`, `PriceList.cs`, `PriceQuote.cs`; `tests/CarRental.Domain.Tests/MoneyTests.cs`, `PriceListTests.cs`

### The rules (from the user stories)

| Rule | Value |
|---|---|
| Rental price | Daily rate × days |
| Daily rate | By car type (agreed: Economy $55, Standard $75, SUV $105, Premium $140) |
| One-way fee | +$100 when the drop-off city ≠ the pickup city |
| Deposit | 10% of the rental price (**not** the one-way fee), any fraction of a cent rounded **up** |
| Total | Rental price + one-way fee + deposit |

Worked example: SUV, Auckland → Wellington, 3 days = $315.00 + $100.00 + $31.50 = **$446.50**.

### What was built

**`Money`: a `readonly record struct`.** An amount of NZD in whole cents, never negative.

| Member | Behaviour |
|---|---|
| `Money(decimal amount)` | Rejects negatives (`ArgumentOutOfRangeException`) and fractions of a cent (`ArgumentException`) |
| `Amount`, `Money.Zero` | The value; $0.00 |
| `+` | Adds two amounts |
| `* int` | Multiplies by a whole number (rate × days) |
| `PercentageRoundedUp(decimal percent)` | e.g. 10% of $164.81 = $16.481 → **$16.49** |

**`PriceList`.** The business's prices (4 daily rates, a one-way fee, a deposit percentage), supplied from outside the domain. It rejects a $0 daily rate and a deposit percentage outside 0–100.

`Quote(CarType chargedType, DateRange period, City pickupCity, City dropOffCity)` returns a `PriceQuote`; it throws for an undefined car type.

**`PriceQuote`: a `sealed record` with an `internal` constructor.** The US2 breakdown: `DailyRate`, `Days`, `RentalPrice`, `OneWayFee`, `Deposit`, and a calculated `Total`. Only `PriceList.Quote` can create one.

### Design decisions

| Decision | Chosen | Alternatives rejected | Why |
|---|---|---|---|
| Number type | `decimal` | `double` | `0.1 + 0.2 != 0.3` in binary floating point (`double`, and every JS number); `decimal` stores base-10 digits exactly |
| Where prices live | A `PriceList` object, numbers supplied from outside | Hard-coded in a `switch` | Price changes shouldn't need a deploy (config arrives in Phase 4). Tests build their own price lists, so they don't break when prices change. The real prices appear in exactly one test: the user-story example |
| Money representation | A `Money` type, **chosen as a teaching exercise** | Plain `decimal` | With one currency and no extra rules, plain `decimal` would also have been reasonable. `Money` earns its keep by guaranteeing whole cents and making rounding explicit |
| `Money` kind | `readonly record struct` | `sealed record` (class) | Small, immutable, and `default(Money)` is $0.00, a valid amount, so the struct's skipped-constructor problem doesn't apply |
| Currency field | None (NZD implied) | `string Currency` | A reference-type field in a struct is `null` in `default(Money)`. Revisit if a second currency arrives |
| Rounding | **Always up** to the cent (ceiling) | Nearest cent ("school" rounding); banker's rounding (`Math.Round`'s default) | Product-owner decision: the business never under-collects |
| Sub-cent amounts | Impossible: `Money` rejects them | Allow, round at display time | Every amount is exact cents, so the only rounding happens in one method whose name says so |
| Result shape | `PriceQuote` breakdown with a calculated `Total` | A single `decimal` total | US2 shows every line; a calculated total can't disagree with them |
| Who creates quotes | `internal` constructor; only `PriceList.Quote` | Public constructor | Nobody outside the domain can make up a quote with an inconsistent deposit |
| Where `Quote` lives | A method on `PriceList` | A separate `PricingCalculator`; a static helper | The data and the behaviour that uses it sit together |
| "Any available" → Standard rate | Deferred to `Booking` (1.5) | Inside pricing | The idea of a *selection* (specific car / type / any) is born in `Booking`. `Quote` takes the type to charge |
| 1–30 day limit | Not in pricing | Validating in `Quote` | It's a rental rule (`Booking`). Pricing will quote any `DateRange` |

### How the tests were designed

Price-list tests use **made-up, distinct rates** (10/20/30/40, fee 7), so a wrong lookup shows immediately and the tests don't depend on real prices. One test uses the real agreed prices to check the user-story example.

| Test | Wrong code it catches |
|---|---|
| Whole cents accepted, including **`16.500m`** | Rejecting zero; checking `decimal.Scale` instead of the value |
| Fractions of a cent / negatives rejected | Missing checks |
| `+`, `* int` | Operator code returning the wrong value |
| `PercentageRoundedUp`: $164.81 → **$16.49** | Rounding to the nearest cent ($16.48); banker's rounding; rounding to whole dollars |
| A zero rate rejected, once per car type | Forgetting to validate one of the four rates |
| Deposit % of −1 / 101 rejected, 0 / 100 accepted | Off-by-one boundaries |
| `DailyRate` is the charged type's rate (all 4 types) | Swapped arms in the `switch` |
| Undefined car type rejected | A fallback that quietly charges the Economy rate |
| Rental price = rate × days | Off-by-one days; rental price equal to the daily rate |
| One-way fee charged / not charged (drop-off city from `City.FromCode("akl")`) | Fee always or never applied. **This is where city equality is tested**, through the behaviour that depends on it |
| Deposit is 10% of the rental only ($9, not $9.70) | Deposit calculated on rental + fee |
| `Quote`'s deposit rounds up | `Quote` doing its own rounding |
| Total = 90 + 7 + 9 = 106 | A line missing from the total |
| User-story example = $446.50 | Anything wrong, with the real prices |

Red → green sequence:

1. Tests written → compile error **CS0246**: `Money`, `PriceList`, `PriceQuote` not found.
2. Stubs → **33 failed**; the 53 existing tests still passed.
3. Implementation → **all 88 passed** (86 Domain + 2 placeholders).
4. **Each tricky test was checked by putting its bug back.** All were caught:

| Bug put back | Caught by |
|---|---|
| Rounding to the nearest cent | The $164.81 rows (`Money` and `Quote`) |
| `amount.Scale > 2` instead of comparing values | The `16.500m` row |
| Deposit on rental + one-way fee | Deposit, total and user-story tests |
| SUV charged at the Premium rate | The SUV row of the rate theory, plus dependent tests |
| `switch` without the `_ =>` arm | The **compiler**: CS8524 |

### C# lessons learned

- **`decimal` for money.** `0.1 + 0.2 == 0.3` is `false` for `double` and `true` for `decimal`. Decimal literals need the `m` suffix: `decimal rate = 55.99;` is a compile error (CS0664).
- **`decimal` keeps trailing zeros.** `16.5m == 16.500m`, but their *scale* (number of stored decimal places) differs, and `ToString()` prints `16.500`. Check precision by comparing values (`decimal.Round(x, 2) != x`), never by `Scale`.
- **Rounding modes.**
  - `Math.Round` / `decimal.Round` default to **banker's rounding** (`ToEven`): 16.485 → 16.48. Always pass a `MidpointRounding` explicitly for money.
  - ⚠️ `MidpointRounding.ToPositiveInfinity`, `ToNegativeInfinity` and `ToZero` are **not** midpoint rules, despite the enum's name. They always round in one direction. `ToPositiveInfinity` is ceiling.
- **Operator overloading.** `public static Money operator +(Money, Money)` defines `+` for our type (not possible for user classes in PHP, or at all in JS). Operators aren't automatically symmetric: `Money * int` doesn't give you `int * Money`. Leaving out `Money * decimal` was deliberate, so all fractional multiplication goes through the method that says how it rounds.
- **`readonly record struct`** gives value semantics, value equality, and immutability. Use an explicit constructor + `{ get; }` rather than a positional record, so `with` can't skip validation.
- **Switch expressions** (`x switch { A => …, _ => … }`) return values, like PHP 8's `match`. On an enum, the compiler **requires** a catch-all (CS8524), because any integer could arrive. But that same catch-all silences any warning when a new enum value is added later, so search for `switch`es whenever you add one.
- **`internal`** means visible within the same project (assembly) only. An `internal` constructor means other layers, and the test project, can't create the type; only domain code can.
- **Built-in numeric guards:** `ArgumentOutOfRangeException.ThrowIfNegative`, `ThrowIfZero` and `ThrowIfGreaterThan` (.NET 8+) work for any numeric type via *generic math*.
- **Calculated properties** (`public Money Total => …`) can't get out of sync with what they're calculated from.
- **Test-side features:**
  - **`decimal` can't go in attributes**, so decimal theory rows use `TheoryData<decimal>`. Yet `decimal` *can* be an optional parameter's default (`decimal economy = 10m`): two different "constant" rules.
  - **`int` → `decimal` converts automatically** (no information lost), so `[InlineData]` rows of `int`s can feed `decimal` parameters. The reverse needs an explicit cast.
