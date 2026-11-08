# 08 — Complete todo

Status: ready-for-agent
Blocked by: 02

## Scope

- `Features/Todos/CompleteTodo.cs`: `POST /todos/{id:int}/complete` sets `done` and stamps `completedAt` from `TimeProvider`.
- Idempotent: repeating the call returns the item with the original `completedAt`.
- No reopen path.

## Acceptance

- Open item → `200`, `done: true`, `completedAt` equals the frozen clock.
- Second call after advancing the clock preserves the first timestamp.
- Missing/deleted id → `404` problem; non-numeric id → `404`.

## Tests

- Completing an open item sets both fields.
- Repeating after time advances preserves the original timestamp.
- Unknown id and non-numeric id → `404`.
