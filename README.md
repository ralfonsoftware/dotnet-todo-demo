# Todo API

A small demo REST API for tracking Todo Items through to completion. It is built as a .NET 10 minimal API in a vertical-slice style, with an in-memory store, OpenAPI, and Scalar UI.

## Prerequisites

- .NET 10 SDK
- Docker (optional, for viewing telemetry in the Aspire Dashboard)

## Run

```bash
dotnet run --project src/TodoApi
```

- API: `http://localhost:5266` (see the console output for the exact URL)
- Interactive docs: `http://localhost:5266/scalar`
- OpenAPI document: `http://localhost:5266/openapi/v1.json`

## Observability

The API exports traces, metrics, and logs over OTLP. To view them, run the Aspire Dashboard in one terminal and the API in another.

Terminal 1 — start the dashboard:

```bash
./scripts/run-aspire-dashboard.sh
```

Terminal 2 — start the API and make a request:

```bash
dotnet run --project src/TodoApi
curl http://localhost:5266/todos
```

Open `http://localhost:18888` and select the `todo-api` service. The dashboard shows ASP.NET Core request traces, Todos Store spans nested under them, HttpClient traces, ASP.NET Core/HttpClient/Runtime metrics alongside the custom Todo Item lifecycle metrics below, and application logs.

The launcher uses the pinned image `mcr.microsoft.com/aspire/dashboard:13.6.4` and maps:

| Purpose | Host | Container |
| --- | --- | --- |
| Dashboard UI | `18888` | `18888` |
| OTLP/gRPC | `4317` | `18889` |
| OTLP/HTTP | `4318` | `18890` |

Anonymous local access is enabled. The container is removed when it exits or when you press Ctrl+C. The script fails with a clear message if Docker or its daemon is unavailable.

### Configuration

Standard `OTEL_*` environment variables take precedence, then the `OpenTelemetry` section in configuration, then the defaults.

| Setting | Default | Override |
| --- | --- | --- |
| Export enablement | enabled outside the `Testing` environment | `OpenTelemetry:Enabled`, `OTEL_SDK_DISABLED=true` |
| OTLP exporter attached | enabled | `OpenTelemetry:ExporterEnabled` |
| Endpoint | `http://localhost:4317` | `OpenTelemetry:Endpoint`, `OTEL_EXPORTER_OTLP_ENDPOINT` |
| Protocol | `grpc` | `OpenTelemetry:Protocol`, `OTEL_EXPORTER_OTLP_PROTOCOL` (`grpc` or `http/protobuf`) |
| Service name | `todo-api` | `OpenTelemetry:ServiceName`, `OTEL_SERVICE_NAME` |
| Extra resource attributes | none | `OTEL_RESOURCE_ATTRIBUTES` |

`service.version` comes from the assembly, and `deployment.environment` is the ASP.NET Core environment name. With `http/protobuf`, the default endpoint becomes `http://localhost:4318` when none is configured.

Tests run in the `Testing` environment, which disables both the pipeline and the exporter, so `dotnet test` needs no dashboard and makes no outbound telemetry attempts.

### Todos Store spans

Every Todos Store operation emits one span from the `TodoApi.TodosStore` activity source (`ActivityKind.Internal`), nested under the active HTTP request span:

| Span | `todo.operation` | `todo.outcome` |
| --- | --- | --- |
| `todos.list` | `list` | `found` |
| `todos.get` | `get` | `found`, `not found` |
| `todos.create` | `create` | `created` |
| `todos.update` | `update` | `updated`, `not found` |
| `todos.complete` | `complete` | `completed`, `already done`, `not found` |
| `todos.delete` | `delete` | `deleted`, `not found` |

`todo.id` carries the Todo Item id when one is involved. `todo.priority` carries `low`, `medium`, or `high` when an item or priority is involved (`get`, `create`, `update`, `complete`, and `delete`); `list` has no single item, so it carries neither tag. Spans never carry the Todo Item title or description.

### Todo Item metrics

The `TodoApi.Todos` meter carries custom metrics for Todo Item lifecycle activity. It is registered with `metrics.AddMeter("TodoApi.Todos")`, so the dashboard shows it alongside the built-in ASP.NET Core, HttpClient, and runtime metrics. Every instrument uses a stable name, unit, and tag key; no title, description, or other user text ever appears in a tag.

| Instrument | Type | Unit | Tags |
| --- | --- | --- | --- |
| `todo.items.created` | `Counter<long>` | `{item}` | `todo.priority` (`low`/`medium`/`high`) |
| `todo.items.completed` | `Counter<long>` | `{item}` | none |
| `todo.items.deleted` | `Counter<long>` | `{item}` | none |
| `todo.store.failures` | `Counter<long>` | `{failure}` | `todo.operation`, `todo.outcome` (`not found`) |
| `todo.items.total` | `ObservableGauge<long>` | `{item}` | none |
| `todo.items.done` | `ObservableGauge<long>` | `{item}` | none |
| `todo.store.operation.duration` | `Histogram<double>` | `s` | `todo.operation` |

The counters advance once per successful mutation: `todo.items.created` on Create (tagged with the Priority from the created item), `todo.items.completed` on Completion, and `todo.items.deleted` only when Delete removed an item. A Completion increments `todo.items.completed` only for a real not-Done to Done transition; replaying a Completion on a Done Todo Item is reported as `already done` on the span and does not double count. Failed operations (a not-found Get, Update, Complete, or Delete) increment `todo.store.failures` with `todo.operation` and `todo.outcome`.

The gauges read the raw Todos Store on each collection, so they report the current total number of Todo Items and the current number of Done Todo Items without emitting an extra span or measurement. The duration histogram records every Todos Store operation in seconds (via `Stopwatch.GetElapsedTime`), tagged with `todo.operation`, including operations that end not found.

## Test

```bash
dotnet test TodoApi.slnx
```

Tests are xUnit v3 integration tests on Microsoft.Testing.Platform, using Shouldly. Each test gets a fresh in-memory store and a clock fixed at `2026-11-08T11:16:00+00:00`.

## Endpoints

| Method | Route | Result |
| --- | --- | --- |
| `GET` | `/todos` | All Todo Items, sorted by id |
| `GET` | `/todos/{id}` | One Todo Item, or `404` |
| `POST` | `/todos` | `201` + `Location`, or `400` |
| `PUT` | `/todos/{id}` | `200` with replaced fields, or `400`/`404` |
| `DELETE` | `/todos/{id}` | `204`, or `404` |
| `POST` | `/todos/{id}/complete` | `200` with completion applied, or `404` |

### Create

```bash
curl -i -X POST http://localhost:5266/todos \
  -H 'Content-Type: application/json' \
  -d '{"title":"Buy milk","description":"Semi-skimmed","priority":"high"}'
```

```json
{
  "id": 1,
  "title": "Buy milk",
  "description": "Semi-skimmed",
  "createdAt": "2026-11-08T11:16:00+00:00",
  "completedAt": null,
  "priority": "high",
  "done": false
}
```

### List and complete

```bash
curl http://localhost:5266/todos
curl -X POST http://localhost:5266/todos/1/complete
```

## Behaviour notes

- **In-memory only.** Data is lost when the process stops and ids restart at 1. See [ADR-0002](docs/adr/0002-in-memory-store.md).
- **Completion is one-way.** `done` and `completedAt` are managed by the server; completing twice keeps the first timestamp. Completed items can still be edited and deleted.
- **Update replaces.** `PUT` replaces `title`, `description`, and `priority`; an omitted description is cleared and an omitted priority resets to `medium`.
- **Validation.** `title` is required (max 200 characters); `description` is at most 2000. Unknown JSON members, including `done` and `completedAt`, are rejected.
- **Errors** are RFC 9457 ProblemDetails.
- **Timestamps** are ISO 8601 UTC with a `+00:00` offset.

## Design

- [Spec](.scratch/todo-api/spec.md)
- [Glossary](GLOSSARY.md)
- [ADR-0001: vertical slice architecture](docs/adr/0001-vertical-slice-architecture.md)
- [ADR-0002: in-memory store](docs/adr/0002-in-memory-store.md)
- [ADR-0003: OpenTelemetry and the Aspire Dashboard](docs/adr/0003-opentelemetry-and-aspire-dashboard.md)
- [ADR-0004: Completion is a reported transition](docs/adr/0004-completion-is-a-reported-transition.md)
- Implementation tickets: [`.scratch/todo-api/issues/`](.scratch/todo-api/issues/)
