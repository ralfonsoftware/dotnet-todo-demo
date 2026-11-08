# Todo API

Status: ready-for-agent
Type: feature

A minimal, in-memory REST API for tracking Todo Items through to completion, built as a demo of vertical-slice architecture on .NET 10.

## Domain language

See [`GLOSSARY.md`](../../GLOSSARY.md) for the canonical terms. In short: a **Todo Item** has an id, title, optional description, creation time, optional completion time, a **Priority**, and a **Done** flag. **Completion** is one-way.

## Wire model

```json
{
  "id": 1,
  "title": "Buy milk",
  "description": "Semi-skimmed",
  "createdAt": "2026-11-08T11:16:00+00:00",
  "completedAt": null,
  "priority": "medium",
  "done": false
}
```

- Property names are camelCase.
- Timestamps are ISO 8601 with a zero offset (`+00:00`), produced by `DateTimeOffset`.
- `priority` is the lowercase string `low`, `medium`, or `high`. Input is case-insensitive; output is always lowercase. Integer values are rejected.
- `done` and `completedAt` are server-managed.

## Endpoints

| Method | Route | Success | Failures |
| --- | --- | --- | --- |
| GET | `/todos` | `200`, array sorted by id ascending, unpaged | — |
| GET | `/todos/{id}` | `200`, item | `404` |
| POST | `/todos` | `201`, `Location: /todos/{id}`, item | `400`, `415` |
| PUT | `/todos/{id}` | `200`, item | `400`, `404` |
| DELETE | `/todos/{id}` | `204` | `404` |
| POST | `/todos/{id}/complete` | `200`, item | `404` |

`{id}` is constrained to `int`; a non-numeric id does not match the route and returns `404`.

## Behaviour rules

- **Create / update body:** `title` (required), `description` (optional), `priority` (optional). Unknown members, including `done` and `completedAt`, are rejected with `400`.
- **Normalisation:** `title` and `description` are trimmed. An omitted or whitespace-only description becomes `null`.
- **Defaults:** creating without `priority` uses `medium`. A full replace (`PUT`) without `priority` also resets to `medium`; an omitted `description` clears it.
- **Validation:** `title` must be non-empty after trimming and at most 200 characters. `description` is at most 2000 characters. Violations return field-keyed `errors`.
- **Creation time:** set once by the server from `TimeProvider`; updates never change it.
- **Completion:** `POST /todos/{id}/complete` sets `done` to `true` and stamps `completedAt` from `TimeProvider`. Repeat calls are idempotent and preserve the original `completedAt`. There is no reopen.
- **Lifecycle:** completed items can still be updated (editable fields only) and deleted; their completion state never changes.
- **Ids:** assigned sequentially from 1, never reused after deletion.
- **Store:** empty at startup, in-process, reset on restart.

## Error contract

All errors use RFC 9457 `ProblemDetails` (`application/problem+json`). Validation failures use `TypedResults.ValidationProblem` with an `errors` dictionary keyed by camelCase field name. A request body that cannot be deserialised (malformed JSON, unknown enum value, unknown member) returns `400`; a non-JSON content type returns `415`.

## Non-goals

Authentication, persistence, paging, filtering, sorting parameters, reopening completed items, CORS, Docker, and multi-process support.

## Test strategy

Black-box HTTP integration tests through `WebApplicationFactory<Program>` on xUnit v3 (Microsoft.Testing.Platform) with Shouldly. One test class per operation, each with a fresh in-memory store and a `FakeTimeProvider` fixed to `2026-11-08T11:16:00+00:00`. Tests cover the contract and happy paths plus failure paths (missing ids, invalid bodies, malformed JSON, wrong content type, repeated completion, deleted items). No separate unit-test layer over the store.

## Delivery

One implementation ticket per slice under `issues/`, each including its own tests. One commit per ticket; each commit leaves `dotnet build` and `dotnet test` green.
