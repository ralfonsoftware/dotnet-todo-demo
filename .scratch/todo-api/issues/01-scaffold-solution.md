# 01 — Scaffold solution, app shell and test harness

Status: resolved
Blocked by: —

## Goal

Create the solution, projects, and the application shell every slice plugs into.

## Scope

- `TodoApi.slnx` at the repo root, `src/TodoApi/`, `tests/TodoApi.Tests/`, project reference from tests to app.
- `.gitignore` for .NET build output and IDE state.
- `Program.cs`: camel-case JSON defaults, `JsonStringEnumConverter<Priority>` (camel-case names, integers rejected), `AddProblemDetails`, `RouteHandlerOptions.ThrowOnBadRequest = false`, `TimeProvider.System`, `AddOpenApi` + `MapOpenApi`, `MapScalarApiReference`.
- Empty `MapTodoEndpoints` extension (now in `Features/Todos/TodoEndpoints.cs`), ready for slices.
- Test harness: `TodoApiFactory` (`WebApplicationFactory<Program>`) with a `FakeTimeProvider` fixed to `2026-11-08T11:16:00+00:00` and a fresh `ITodoStore` per factory; shared JSON options for deserialising responses.
- Smoke test: app boots and `GET /openapi/v1.json` returns `200`.

## Acceptance

- `dotnet build` and `dotnet test` pass.
- Scalar UI is served at `/scalar`.
- `public partial class Program` is visible to the test project.

## Notes

- xUnit v3 on Microsoft.Testing.Platform; Shouldly for assertions.
- This ticket intentionally has no todo endpoints; the OpenAPI smoke test keeps the suite green.

## Outcome

`TodoApi.slnx` with `src/TodoApi` and `tests/TodoApi.Tests`; app shell serves `/openapi/v1.json` and `/scalar`; `TodoApiFactory` fixes the clock at `2026-11-08T11:16:00+00:00`; two smoke tests pass.
