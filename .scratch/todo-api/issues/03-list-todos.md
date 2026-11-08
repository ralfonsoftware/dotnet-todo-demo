# 03 — List todos

Status: ready-for-agent
Blocked by: 02

## Scope

- `Features/Todos/ListTodos.cs`: `GET /todos` returns every Todo Item as `TodoResponse`, sorted by id ascending.
- Register the slice from `MapTodoEndpoints`.

## Acceptance

- Empty store → `200` with `[]`.
- Response uses camelCase property names and lowercase priority.

## Tests

- Empty store returns an empty array.
- Seeded items return in id order with the full wire shape.
