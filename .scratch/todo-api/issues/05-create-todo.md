# 05 — Create todo

Status: ready-for-agent
Blocked by: 02

## Scope

- `Features/Todos/CreateTodo.cs`: `POST /todos` accepting `{ title, description?, priority? }` with unmapped members rejected.
- Validation shared by create/update: trimmed title required, ≤200; description ≤2000; description whitespace → `null`; priority defaults to `medium`.
- `createdAt` from `TimeProvider`.
- `201` with `Location: /todos/{id}`.

## Acceptance

- Empty store creates id 1; ids increase monotonically.
- `done`/`completedAt` in the body → `400`.
- Malformed JSON → `400`; wrong content type → `415`.

## Tests

- Minimal payload applies defaults and returns `Location`.
- Full payload preserves title, description, priority.
- Missing, blank, and over-long title → `400` with `errors.title`.
- Over-long description → `400`.
- Unknown member, `done`, malformed JSON, and integer priority → `400`.
- Wrong content type → `415`.
