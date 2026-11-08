# 02: Trace Todos Store operations

**What to build:** Every Todos Store operation — list, get, create, update, complete, and delete — emits one custom span nested under the HTTP server span. A developer can follow a single request in the dashboard and see which store operation ran and what happened, without leaking Todo Item title or description into telemetry.

**Blocked by:** 01 — Run the Aspire Dashboard and wire OpenTelemetry to it

**Status:** resolved

Instrument once at the Todos Store seam with a decorator around the store, rather than repeating tracing inside every feature slice.

- [x] Each of the six Todos Store operations emits exactly one custom span with a stable span name and an operation tag.
- [x] Spans carry the Todo Item id when one is involved, the Priority when one is involved, and an outcome such as found, not found, created, updated, completed, already done, or deleted.
- [x] Spans never carry Todo Item title or description.
- [x] The store span is a child of the active HTTP request span and inherits the service resource attributes.
- [x] Not-found, deleted, and already-completed outcomes are distinguishable from successful outcomes on the span.
- [x] Integration tests use `ActivityListener` to assert span names, tags, and parenting, and run without the dashboard.
- [x] The dashboard shows a create → complete → delete sequence as a nested trace tree under the corresponding HTTP requests.

## Outcome

`Storage/InstrumentedTodoStore` (introduced as `TracingTodoStore`, extended by ticket 03) decorates the Todos Store and emits one `ActivityKind.Internal` span per operation from the `TodoApi.TodosStore` activity source: `todos.list`, `todos.get`, `todos.create`, `todos.update`, `todos.complete`, and `todos.delete`. Spans carry `todo.operation`, `todo.id`, `todo.priority`, and a stable `todo.outcome` (`found`, `not found`, `created`, `updated`, `completed`, `already done`, `deleted`); no title, description, or span events. `ActivityListener` tests cover all six operations, every outcome, exactly one span per operation, privacy, and HTTP parentage including the create → complete → delete tree. The delete seam was changed to return the removed Todo Item so logging and tracing share one atomic delete without an extra store read. `dotnet test TodoApi.slnx`: 87 passed.
