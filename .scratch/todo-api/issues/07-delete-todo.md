# 07 — Delete todo

Status: ready-for-agent
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
