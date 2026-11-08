# Todo board

Status: resolved
Type: feature

A server-rendered personal board at `/` for everyday use of the Todos Store. It runs in the same process as the API and reads and writes `ITodoStore` directly. Written after the fact from [ADR-0005](../../docs/adr/0005-server-rendered-razor-pages-ui.md) and the README board section, to record what was built.

## Domain language

See [`GLOSSARY.md`](../../GLOSSARY.md). The board shows **Todo Items** with their **Priority** and **Done** state, and applies **Completion** the same way the API does.

## Goals

- One place to add, edit, complete, and delete Todo Items without a client framework or an asset build step.
- One source of truth: the board and the API share the Todos Store, the domain rules (`TodoDraft`), and the in-memory lifecycle.
- Works without JavaScript; JavaScript only adds small enhancements.

## Behaviour

- **Compose.** A title (required, at most `TodoDraft.TitleMaxLength` characters), an optional note (at most `TodoDraft.DescriptionMaxLength`), and a Low/Medium/High Priority (default Medium). Validation is `TodoDraft.TryCreate`, shared with the API; errors show next to the fields.
- **Filters.** `?view=open` (default), `done`, or `all` (`BoardFilter`). Unknown values fall back to Open. Open items sort by Priority, then creation time; Done items by completion time, newest first; All shows Open then Done. Each filter shows its count.
- **Progress.** A ring and "N of M done" with a rounded percentage.
- **Edit inline.** `?edit=<id>` opens the editor with the current values; a missing id renders no editor. Invalid input keeps the editor open with one message.
- **Complete and delete.** One button each; delete asks for confirmation when JavaScript is on. Missing items show "That todo no longer exists."
- **Post/Redirect/Get.** Every mutation is a form POST with an antiforgery token, then a redirect back to the same filter, with a toast message.
- **Dates.** "Added" and "Done" dates and the "Today" label use the `TimeProvider` local time zone; `datetime` attributes stay ISO 8601 UTC.
- **Enhancements.** Light/dark theme toggle (stored in `localStorage`), delete confirmation, and the `N` shortcut that focuses the composer. The presentation hooks that couple view, CSS, and JavaScript live in `PresentationHooks`.
- **Assets.** Self-hosted Plus Jakarta Sans font; no third-party requests.
- **Observability.** Store calls get the same spans, metrics, and outcome logs as API calls (ADR-0006); the board itself logs only validation rejections.

## Out of scope

- Client-side rendering, a SPA, or calling the HTTP API from the browser (rejected in ADR-0005).
- Accounts, sharing, or more than one user.
- Persistence beyond the process lifetime (ADR-0002).
- Reopening a Done item (Completion is one-way).
- Security headers and a CSP; the app is a localhost demo (review item 23, deferred).

## Tests

- [`tests/TodoApi.Tests/Pages/TodoPageTests.cs`](../../tests/TodoApi.Tests/Pages/TodoPageTests.cs): real form posts with antiforgery tokens, redirects, rendered HTML, and store state.
- [`tests/TodoApi.Tests/Pages/PresentationHookTests.cs`](../../tests/TodoApi.Tests/Pages/PresentationHookTests.cs): the view, CSS, and JavaScript contract, the filter values, and the self-hosted font.
- [`tests/TodoApi.Tests/Pages/BoardMutationLogTests.cs`](../../tests/TodoApi.Tests/Pages/BoardMutationLogTests.cs) and [`tests/TodoApi.Tests/Logging/TodoLogOwnershipTests.cs`](../../tests/TodoApi.Tests/Logging/TodoLogOwnershipTests.cs): board outcome and validation logs.
- [`tests/TodoApi.Tests/Pages/BoardErrorsTests.cs`](../../tests/TodoApi.Tests/Pages/BoardErrorsTests.cs): the editor's error message.
