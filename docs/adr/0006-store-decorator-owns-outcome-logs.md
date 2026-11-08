# The store decorator owns Todo Item outcome logs

Status: accepted

The observability decorator around the Todos Store (`InstrumentedTodoStore`) emits every Todo Item outcome log — `CreateSucceeded`, `UpdateSucceeded`, `CompleteSucceeded`, `CompleteAlreadyDone`, `DeleteSucceeded`, `NotFound`, `GetSucceeded`, and `ListSucceeded` — through an injected `ILogger<TodoLogCategory>`. Callers log only what the store cannot know: the HTTP slices and the board emit `CreateValidationRejected` and `UpdateValidationRejected`, under the same category, because rejected input never reaches the store.

## Context

The same call-the-store-then-log code existed in the six HTTP slices and again in the board's `BoardMutations` helper. The two surfaces logged under different categories (`TodoApi.Logging.TodoLogCategory` for the API, `TodoApi.Pages.IndexModel` for the board), so one filter in the Aspire Dashboard never showed every Todo Item outcome. Every new caller of `ITodoStore` had to remember to copy the logging, and nothing checked that it did.

The decorator already classifies every outcome for spans and metrics, including the `CompletionResult` that names the already-done case (ADR-0004). Logging from the same place reuses that classification instead of repeating it.

## Decision

- `InstrumentedTodoStore` takes `ILogger<TodoLogCategory>` from DI next to its `IMeterFactory`, and writes exactly one outcome record per operation, inside the operation's span so the record correlates with it.
- `TodoResults.NotFound` only builds the `404` problem; the slices no longer log outcomes.
- `BoardMutations` is deleted. `IndexModel` calls `ITodoStore` directly and logs validation rejections only.
- Event ids, levels, message templates, and properties stay unchanged. They remain part of the observability contract (`TodoEventIds`).

This does not conflict with ADR-0001: the shared behaviour moves into `Storage/`, not between slices.

## Considered options

- **Keep logging in the callers and only unify the category in `BoardMutations`** (the minimal fix). Rejected: it fixes the dashboard filter but keeps two copies of the orchestration, and the next caller would still have to copy it.
- **A logging decorator separate from the telemetry decorator.** Rejected as one more wrapper for the same outcome classification the telemetry decorator already performs.

## Consequences

- Every caller of `ITodoStore`, including future ones, gets consistent outcome logs without writing any logging code.
- Board reads now log `ListSucceeded` at `Debug`, as the API's list does. It is filtered out at the default `Information` level.
- Direct store calls in tests also log; tests that count records filter by event id or count the records an action adds.
- Callers must not log store outcomes themselves, or the outcome is logged twice. `TodoLogOwnershipTests` checks one record per operation for the API and the board.
