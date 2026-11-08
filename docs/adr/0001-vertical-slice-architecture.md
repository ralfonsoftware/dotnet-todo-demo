# Vertical slice architecture

Status: accepted

The API is organised by feature under `src/TodoApi/Features/Todos/`: each slice owns its route mapping, handler, request record, and tests, and the slices are stitched together by a single `MapTodoEndpoints` extension. Shared building blocks live outside the slices in `Domain/` (the Todo Item and Priority), `Storage/` (the Todos Store), and `Contracts/` (the wire response).

We chose this over the conventional Controllers → Services → Repositories layering because the demo exists to show slice boundaries: adding an operation should mean adding one file plus one registration line, not touching four layers. The cost is that cross-slice reuse must be deliberate; shared code is promoted to `Domain`/`Storage`/`Contracts` rather than imported slice-to-slice.
