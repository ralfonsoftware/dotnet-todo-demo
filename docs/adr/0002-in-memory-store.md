# In-memory Todos Store

Status: accepted

Todo Items live only in process memory behind `ITodoStore`, implemented by a thread-safe singleton backed by a `ConcurrentDictionary`; every API process starts empty and loses its data on restart. This is deliberate for a demo: the point is the HTTP contract and slice structure, not durability.

We considered SQLite or EF Core with a real database and rejected it: migrations, connection management, and per-test database cleanup would add moving parts without exercising anything the demo is meant to show. Swapping in durable storage later means implementing `ITodoStore` and changing one DI registration. The observability decorator is typed on `ITodoStore`, so a durable adapter inherits store spans and metrics without reimplementing them. The accepted consequences are no durability, no multi-process scale-out, and ids that restart at 1 on every launch.
