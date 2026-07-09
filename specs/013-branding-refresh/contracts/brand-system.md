# Brand System Contract: Branding Refresh

This contract defines observable visual outcomes for the refreshed brand.

## Logo and mark

- The primary logo mark is a wire frame globe or globe-route symbol.
- The mark must work in full navigation, compact navigation, favicon/app icon, and empty-state contexts.
- The mark must not use the old compass/star/explorer visual as the primary brand symbol.
- The home link must expose an accessible name equivalent to "Trip Planner home".

## Visual direction

- Product personality: modern, attractive, calm, helpful, and travel-forward.
- Visual metaphor: planning routes across the world by car, train, plane, and boat.
- The image-led home page should feel like a polished travel planning product, not a public directory or booking marketplace.
- Decorative visuals must not crowd trip information, navigation, or recent trip access.

## Light mode palette

- Light mode should feel bright, clean, scenic, and suitable for a large travel image.
- Primary brand color should be distinct from the previous explorer green/burnt-orange combination.
- Secondary and accent colors should support transport, route, and planning-tip cues without creating a one-note palette.
- Surfaces, cards, forms, selected states, and feedback states must be defined through semantic tokens rather than one-off colors.

## Dark mode palette

- Dark mode should draw from aurora borealis: deep night surfaces, aurora green/cyan accents, restrained blue-violet atmospheric depth, and limited warm highlights.
- The aurora treatment may use gradients, glow, or background atmosphere when it does not reduce readability.
- Dark mode must remain a usable planning workspace, not only a decorative hero treatment.
- Existing dark mode selection and saved preference behavior must remain intact.

## Semantic color roles

Each role must have light and dark values.

- `primary`: primary brand emphasis and main actions.
- `secondary`: secondary actions and supporting brand moments.
- `accent`: highlights, transport cues, and planning tips.
- `page`: application background.
- `surface`: cards, panels, dropdowns, modals, and forms.
- `raisedSurface`: navigation, floating controls, elevated cards, and overlays.
- `text`: heading, body, muted, inverse, and link text.
- `border`: dividers, component outlines, and selected state structure.
- `focus`: visible focus rings and keyboard navigation cues.
- `selected`: active navigation, selected tabs, selected filters, and active toggles.
- `feedback-success`, `feedback-warning`, `feedback-error`, `feedback-info`: status and validation states.

## Accessibility and interaction rules

- Text, links, controls, badges, dropdowns, and status messages must remain readable in both modes.
- Focus indicators must be visible against image, light, and dark surfaces.
- State meaning must not rely on color alone; use labels, icons, shape, position, or text when communicating status.
- Buttons and links over the welcome image must maintain contrast across desktop and mobile crops.
- Responsive layouts must avoid clipped essential text, overlapping controls, and horizontal scrolling for primary content.