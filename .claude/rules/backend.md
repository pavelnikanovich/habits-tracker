---
paths:
  - "backend/**"
---

# Backend rules

These hold the hexagonal shape of constitution IV and the data rules of research R20; the tests guard the first ones, review the rest.

- `HabitsTracker.Identity.Core` references nothing: no `PackageReference`, `FrameworkReference` or `ProjectReference`, no `System.ComponentModel.DataAnnotations` or `System.Text.Json`. Put what needs a framework in the adapters.
- Time comes only through `TimeProvider`, so tests can move it with `FakeTimeProvider`; never `DateTime.Now`, `DateTimeOffset.UtcNow` or `Stopwatch` for domain time.
- An invalid value raises `DomainException` with its `DomainError`; the HTTP adapter turns it into a problem.
- Adapters meet only in the core: the folders `Http`, `Storage`, `Email` and `Translations` of `HabitsTracker.Identity.Adapters` name no type of one another; only `IdentityContextRegistration` names them all.
- Driving adapters (endpoints, the authentication handler, the hourly cleanup) call only use cases: they never resolve a driven port and never change an aggregate they received.
- `FromStorage` is called only by the storage adapter.
- No email, user id, session token or one-time code in a log; IP addresses are the only personal data a log holds ("Entity and type model", research R20).
- Tests first: a failing test, then the code.
- Copy values from `spec.md` and `data-model.md` (limits, lengths, lifetimes, enumerations) verbatim.
