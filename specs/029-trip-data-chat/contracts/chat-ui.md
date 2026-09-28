# Trip Data Chat UI Contract

## Shell entry point

- Render a fixed activator in the lower-right corner of the authenticated application shell, above page content and safe-area insets.
- Activator is a button with an accessible name such as "Open trip chat", a stable `aria-controls` target, and `aria-expanded` state. The closed pane is absent from the accessibility tree.
- Activating it opens a right-side pane on the current page, fixed from top to bottom of the viewport (`100dvh` with fallback). On narrow screens the pane occupies the full viewport width; on wider screens it is a bounded-width side panel. The underlying route remains the current route.
- Use a labeled heading, message list, labeled input, send/stop controls with disabled/loading states, visible status and error announcements, keyboard operation, sensible focus placement/return, and a close control. Modal semantics and background interaction must match the responsive overlay behavior; do not trap focus on desktop if the pane is non-modal there.

## Conversation and session behavior

- Keep transcript and pane-open state in `sessionStorage`, namespaced by the opaque authentication-session epoch from the Web auth cookie. Do not use local storage, URL query values, or a durable server transcript.
- On citation navigation to the existing `/trips/{tripId}` details route, preserve the epoch-scoped transcript and open state; the shell restores them on the destination page. Preserve scroll position where practical without blocking page interaction.
- Clear/discard stored state when authentication is absent, sign-out occurs, the auth session expires, or the epoch changes. A new login starts a clean conversation even in the same tab. Tab closure naturally discards browser session storage.
- Keep prior user turns for API follow-up context. Render prior assistant answers for the active transcript, but do not send them back as trusted evidence on later requests.
- Bound client-side stored turns and message sizes. If storage is unavailable/corrupt, fail gracefully to an empty in-memory conversation without logging transcript values.

## Response and citation rendering

- Render the API answer as text; never inject model output as HTML.
- Display typed citation controls using API-resolved trip/source labels. Build links from stable `tripId` and source IDs; never follow an arbitrary model-provided URL.
- Clicking a citation navigates to the trip details page. Existing page/API authorization remains authoritative; a stale/revoked link must show the existing not-found-or-denied state, not leak detail.
- Provide explicit empty, insufficient-data, no-accessible-trips, loading, throttled, and retryable service-failure states. Keep the existing transcript when a request fails.