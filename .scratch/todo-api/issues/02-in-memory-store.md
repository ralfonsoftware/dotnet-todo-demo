# 02 — In-memory Todos Store

Status: resolved
Blocked by: 01

## Goal

Model Todo Items and provide the thread-safe in-memory Todos Store used by every slice.

## Scope

- `Domain/TodoItem.cs`: immutable record with `Id`, `Title`, `Description`, `CreatedAt`, `CompletedAt`, `Priority`, `Done`, plus one-way `Complete(DateTimeOffset)` that preserves an existing `CompletedAt`.
- `Domain/Priority.cs`: `Low`, `Medium`, `High`.
- `Contracts/TodoResponse.cs`: shared wire record and mapping from `TodoItem`.
- `Storage/ITodoStore.cs`: `GetAll`, `Get`, `Create`, `Update`, `Complete`, `Delete`.
- `Storage/InMemoryTodoStore.cs`: `ConcurrentDictionary` + lock for read-modify-write, `Interlocked` id counter starting at 1, ids never reused after delete, empty at startup.
- Register as a singleton in DI.

## Acceptance

- List reads are ordered by id ascending.
- Concurrent create/update/complete/delete never corrupt an item.
- Completing an already-done item returns it unchanged.
- Updating preserves `CreatedAt`, `Done`, and `CompletedAt`.

## Tests

No dedicated store tests: the store is exercised through every slice's HTTP tests, per the spec's test strategy. Keep `dotnet test` green via existing smoke test.

## Outcome

`TodoItem` carries one-way `Complete`; `ITodoStore`/`InMemoryTodoStore` registered as a singleton; ids from `Interlocked`, read-modify-write and delete guarded by a lock; store starts empty.
