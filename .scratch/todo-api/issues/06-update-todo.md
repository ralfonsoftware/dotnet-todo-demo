# 06 — Update todo

Status: ready-for-agent
Blocked by: 02

## Scope

- `Features/Todos/UpdateTodo.cs`: `PUT /todos/{id:int}` fully replacing `title`, `description`, and `priority`; same validation and defaults as create.
- `createdAt`, `done`, and `completedAt` are never changed by an update.
- Completed items remain editable.

## Acceptance

- Existing item → `200` with replaced fields.
- Omitted description clears it; omitted priority resets to `medium`.
- Missing id → `404`; invalid body → `400`.

## Tests

- Full replacement returns updated fields and keeps `createdAt`.
- Omitted description clears; omitted priority resets.
- Unknown id → `404`.
- Blank title and `done` in body → `400`.
- Updating a completed item keeps `done: true` and the original `completedAt`.
