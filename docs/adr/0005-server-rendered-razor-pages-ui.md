# Server-rendered Razor Pages UI

Status: accepted

The browser frontend is a Razor Pages board served at `/` from the same `TodoApi` process, and its `PageModel` reads and writes through `ITodoStore` and `TimeProvider` directly instead of calling the HTTP API. This keeps one source of truth for todo state, one set of domain rules, and one run command, while pages stay fully server-rendered with no client framework or asset build step. Mutations are plain form POSTs with antiforgery tokens and a POST/Redirect/GET cycle, so the board works without JavaScript; small progressive enhancements add the theme toggle, delete confirmation, and the `N` shortcut. Filter and edit state live in the query string (`?view=open&edit=3`) rather than in client state, which keeps views bookmarkable and deep-linkable.

We considered a separate SPA plus API and a minimal-API-only frontend, and rejected both: the first adds a second process, CORS, duplicated domain rules, and a toolchain for a single-user demo; the second gives up server-side HTML, form validation, and the no-JavaScript path. The accepted consequences are full-page navigations on mutation, UI logic in the same assembly as the HTTP slices, and the board sharing the API's in-memory lifecycle, so todos reset when the process restarts.
