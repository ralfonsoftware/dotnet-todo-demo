# Todo API

A small demo REST API for tracking Todo Items through to completion. It is built as a .NET 10 minimal API in a vertical-slice style, with an in-memory store, OpenAPI, and Scalar UI.

## Prerequisites

- .NET 10 SDK

## Run

```bash
dotnet run --project src/TodoApi
```

- API: `http://localhost:5266` (see the console output for the exact URL)
- Interactive docs: `http://localhost:5266/scalar`
- OpenAPI document: `http://localhost:5266/openapi/v1.json`

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
- Implementation tickets: [`.scratch/todo-api/issues/`](.scratch/todo-api/issues/)
