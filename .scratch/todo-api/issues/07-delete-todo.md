# 07 — Delete todo

Status: resolved
Blocked by: 02

## Scope

- `Features/Todos/DeleteTodo.cs`: `DELETE /todos/{id:int}` → `204`, or `404` ProblemDetails.

## Acceptance

- Deleted item is gone (subsequent get/delete → `404`).
- Ids are never reused.
- Completed items can be deleted.

## Tests

- Existing item → `204` with no body; get and delete then return `404`.
- Unknown id → `404` problem.
- Completed item → `204`.

## Outcome

`DELETE /todos/{id:int}` returns `204` or a `404` ProblemDetails; ids are not reused; completed items are deletable. Covered by 4 tests.
