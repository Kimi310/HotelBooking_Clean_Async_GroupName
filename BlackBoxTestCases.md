# Create booking – black-box test cases

**Feature:** a room can be booked for a future period (start date – end date) if it is not already booked on one or more days of the period.

Notation: SD = start date, ED = end date, T = today.
For the decision table, a single fully occupied date range **O** is assumed (first day *s* = T+10, last day *e* = T+20). **B** = before O, **A** = after O.

## 1. Equivalence classes (invalid dates)

Dates are assumed to be picked from a date picker, so only the date relations are considered.

| Class | Condition | Valid/Invalid | Example |
|---|---|---|---|
| EC1 | SD > T and SD ≤ ED | Valid | SD = T+1, ED = T+3 |
| EC2 | SD ≤ T | Invalid | SD = T-3, ED = T+3 |
| EC3 | SD > ED | Invalid | SD = T+5, ED = T+2 |

## 2. Boundary values

| Test | SD | ED | Expected |
|---|---|---|---|
| 1 | T | T+5 | Invalid dates (error) |
| 2 | T-1 | T+5 | Invalid dates (error) |
| 3 | T+1 | T+1 | Booking created |
| 4 | T+2 | T+1 | Invalid dates (error) |

## 3. Decision table (availability)

| Rule | 1 | 2 | 3 | 4-5 | 6-7 | 8-10 |
|---|---|---|---|---|---|---|
| SD in | B | A | B | B | O | O |
| ED in | B | A | A | O | A | O |
| Book room | y | y | n | n | n | n |


A rejected booking (n) returns `false` from `CreateBooking`; invalid dates throw an `ArgumentException`.
