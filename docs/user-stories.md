# User stories

The agreed behaviour of the system. Every domain rule we implement should trace back to an acceptance criterion here; if a test can't be traced to one, either the test or this document is wrong.

**Actor:** a *customer*, i.e. a member of the public using the website. No login.

## Business rules (summary)

| Rule | Value |
|---|---|
| Cities | Auckland, Wellington, Christchurch (all on New Zealand time) |
| Currency | NZD, shown to 2 decimal places |
| Rental length | 1 to 30 days, counted as nights: pickup 10th → return 13th = 3 days |
| Same-day booking | Allowed: pickup date may be today (New Zealand date) |
| Car types and daily rates | Economy $55, Standard $75, SUV $105, Premium $140 |
| Booking options | A specific car, a car type, or "any available" |
| "Any available" price | Always the Standard rate, whatever type of car is assigned |
| Car assignment | A real car is assigned at booking time, for every booking option |
| Deposit | 10% of the rental price (excluding the one-way fee), added on top. Any fraction of a cent is rounded **up**: $16.481 → $16.49 |
| One-way rental | Drop-off in a different city costs an extra $100 |
| Car location | Each car has a *home city* and a *current city* |
| Cleaning break | 1 full day after a return-to-home-city rental: returned on the 13th → next pickup the 15th at the earliest |
| Relocation break | 7 days after a one-way rental, while the car is returned to its home city. Replaces the cleaning break (cleaning happens during relocation): returned in Wellington on the 13th → bookable in Auckland from the 21st |
| Registration plate | 1–6 characters including spaces: letters A–Z (lower case is accepted and stored upper case), digits 0–9, and single spaces inside the plate. Leading/trailing spaces and double spaces are rejected. Spaces are significant: `AB 12` and `AB12` are different plates |
| Discounts | None |
| Driver | Must hold a full driver's licence that is valid until the return date |

## US1: Search for available cars

> As a customer, I want to choose a pickup city, a drop-off city and my dates, so that I can see what I can rent.

- **Given** a pickup city and dates, **when** I search, **then** I see only cars whose home city is the pickup city and that are free for the whole rental, with no clash with another booking's cleaning or relocation break.
- The rental must be between 1 and 30 days long, so the return date is 1 to 30 days after the pickup date.
- The pickup date can't be before today's date in New Zealand.
- Results can be viewed as specific cars, or as car types with availability (a type is available if at least one car of that type is free).
- "Any available" is offered whenever at least one car in the city is free.

## US2: See the price before I book

> As a customer, I want to see the full cost of each option, so that I can compare before committing.

- Rental price = daily rate × number of days.
- The daily rate comes from the car's type, except for "any available", which is always charged at the Standard rate.
- A one-way rental (drop-off city ≠ pickup city) adds $100.
- A deposit of 10% of the rental price is added on top. It isn't charged on the one-way fee.
- Total = rental price + one-way fee + deposit.
- The price breakdown shows: daily rate, number of days, rental price, one-way fee (if any), deposit, and total.

*Example:* SUV, Auckland → Wellington, 3 days: rental $315.00 + one-way $100.00 + deposit $31.50 = **$446.50**.

## US3: Book a car

> As a customer, I want to book a specific car, a car type, or any available car, so that a car is reserved for me.

- When I submit valid details, a booking is created for a specific car and I receive a booking reference.
- For a car type or "any available" booking, the system assigns a free car at booking time.
- If the car (or every suitable car) was booked by someone else while I was filling in the form, I see a "no longer available" message and **no double booking is created**.
- A car can't be booked if the rental overlaps another booking for that car, *including* that booking's cleaning or relocation break.
- I must provide my name, email, and driver's licence details: licence number, licence type (learner, restricted or full) and expiry date.
- A learner or restricted licence is rejected, as is a licence that expires before the return date. (We check the details given; we can't verify a licence is genuine.)

## US4: See my booking confirmation

> As a customer, I want a summary after booking, so that I know exactly what I've reserved.

- Shows: reference, car (make, model, type), pickup city, drop-off city, dates, number of days, and the price breakdown from US2.

## Out of scope (for now)

Payments (the deposit is calculated and shown, not charged); customer accounts; cancelling or changing a booking; staff/admin screens; sending emails; discounts.

## Future development

- **Smarter one-way handling.** Today a one-way rental always triggers a 7-day relocation back to the home city. Later, a car could be rented onward from its *current city* instead, with relocation planned around real demand.
