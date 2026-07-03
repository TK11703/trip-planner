# Brand System Contract: Adventurous Explorer

This contract defines the expected visual system outcomes for the Modern UI Refresh.

## Brand direction

- Product personality: modern, trustworthy, optimistic, travel-oriented, and exploratory.
- Visual metaphor: Adventurous explorer planning a journey, not generic enterprise administration.
- Main graphic/icon: a recognizable trip-planning mark usable in a large landing placement, app navigation, mobile header, favicon/app icon contexts, and empty states without crowding key actions.

## Semantic color roles

Each role must have light and dark values.

- `primary`: primary brand emphasis and primary actions.
- `secondary`: secondary actions and supporting brand expression.
- `supporting`: travel-oriented accents for highlights, illustrations, and guided states.
- `surface`: page, card, panel, modal, and calendar surfaces.
- `text`: heading, body, muted, inverse, and link text.
- `border`: dividers, focus rings, selected state borders, and subtle structure.
- `feedback-success`: success and completion states.
- `feedback-warning`: caution and non-blocking risk states.
- `feedback-error`: validation errors, destructive states, and blocked operations.
- `feedback-info`: informational, loading, unavailable, and guidance states.
- `selected`: active navigation, selected calendar items, and current filter/toggle states.

## Theme mode rules

- Light and dark modes are equal first-class expressions of the same brand.
- Signed-in travelers' saved preference takes precedence over device/browser color setting.
- Signed-in travelers with no saved preference follow device/browser color setting by default.
- Unauthenticated visitors follow device/browser color setting by default.
- Theme changes must apply consistently across the shell, navigation, content surfaces, cards, forms, calendars, modals, validation, and access states.

## Interaction state rules

- Primary actions must be visually distinct from secondary actions.
- Hover, focus, active, selected, loading, empty, validation, access-denied, and unavailable states must be recognizable in both modes.
- Important state meaning must include a non-color cue, such as text, iconography, shape, label, or ARIA relationship.
- Focus indicators must be visible against both light and dark surfaces.

## Responsive rules

- Desktop layouts may use richer composition and supporting visuals when they do not obscure primary actions.
- Tablet layouts must preserve scanability and touch target spacing.
- Phone layouts must keep primary content in a usable single-column or otherwise compact flow without horizontal scrolling for primary content.
- Dense calendar and itinerary views must remain discoverable on phone/tablet without trapping controls off-screen.

## Accessibility rules

- Text, controls, feedback, selected, and calendar states must meet readable contrast expectations in both modes.
- Keyboard navigation must preserve logical focus order across navigation, actions, calendar controls, and modal-related flows.
- Screen reader labels, landmarks, and summaries must communicate purpose without relying on visual styling alone.
- Touch targets for primary actions, navigation controls, cards, and calendar interactions must remain comfortably sized and spaced.
