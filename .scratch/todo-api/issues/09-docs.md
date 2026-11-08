# 09 — Documentation and design verification

Status: ready-for-agent
Blocked by: 03, 04, 05, 06, 07, 08

## Scope

- `README.md`: prerequisites (.NET 10 SDK), how to run and test, endpoint table, curl examples, in-memory caveat, links to spec/glossary/ADRs.
- Re-read `GLOSSARY.md`, both ADRs, and `spec.md`; correct any drift from the shipped API.
- Verify all nine tickets are `resolved` or note follow-ups.

## Acceptance

- README commands work from a clean checkout.
- No design document contradicts the implementation.
- `dotnet build` and `dotnet test` pass.

## Tests

- Manual smoke run: create, list, get, update, complete (twice), delete against `dotnet run`.
