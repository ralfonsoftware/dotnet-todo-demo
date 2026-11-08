# 03: Measure Todo Item lifecycle with metrics

**What to build:** The API publishes custom metrics for Todo Item lifecycle activity — items created by Priority, items completed, items deleted, failed store operations, current total and Done counts, and Todos Store operation duration — so a developer can watch the effect of exercising the API in the dashboard.

**Blocked by:** 02 — Trace Todos Store operations (extends the same store instrumentation seam)

Status: resolved

- [x] Counters record created items by Priority, completed items, and deleted items, incrementing exactly once per successful mutation.
- [x] The completion counter increments only when a previously not-Done Todo Item becomes Done; replaying a Completion does not double count.
- [x] Failed store operations are counted with operation and outcome tags.
- [x] Gauges report the current total number of Todo Items and the current number of Done Todo Items from the Todos Store.
- [x] A store operation duration histogram records every store operation with an operation tag and a stable unit.
- [x] Instruments use a stable meter name and documented names, units, and tag keys.
- [x] Integration tests use `MeterListener` to assert instrument values and tags, and run without the dashboard.
- [x] The dashboard shows the custom metrics alongside the built-in ASP.NET Core and runtime metrics.

## Outcome

The combined store decorator publishes meter `TodoApi.Todos`, registered with `metrics.AddMeter`: `todo.items.created` (Counter, `{item}`, `todo.priority`), `todo.items.completed` (Counter, `{item}`, only a real not-Done → Done transition), `todo.items.deleted` (Counter, `{item}`), `todo.store.failures` (Counter, `{failure}`, `todo.operation` + `todo.outcome`), `todo.items.total` and `todo.items.done` (ObservableGauge, `{item}`, read from the raw store), and `todo.store.operation.duration` (Histogram, `s`, `todo.operation`). Completion transitions are serialized so replay and concurrent completions count once. `MeterListener` unit and HTTP integration tests pass without a dashboard, and `AddMeter` was smoke-tested against the Aspire Dashboard. `dotnet test TodoApi.slnx`: 87 passed.
