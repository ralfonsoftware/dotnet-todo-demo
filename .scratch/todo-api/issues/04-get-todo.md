# 04 — Get todo by id

Status: resolved
Blocked by: 02

## Scope

- `Features/Todos/GetTodo.cs`: `GET /todos/{id:int}` returns the item or `404` ProblemDetails.

## Acceptance

- Existing id → `200` with the full wire shape.
- Missing id → `404` with `application/problem+json`.
- Non-numeric id does not match the route and returns `404`.

## Tests

- Existing item returns every field.
- Unknown id returns a `404` problem with `status: 404`.
- Non-numeric id returns `404`.

## Outcome

`GET /todos/{id:int}` returns the item or a `404` ProblemDetails; non-numeric ids fall through the route constraint.
