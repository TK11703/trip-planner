# Feature Specification: Estimated Expenses

**Feature Branch**: `[017-estimated-expenses]`

**Created**: 2026-07-10

**Status**: Draft

**Input**: User description: "I want to be able to track estimated expenses and display those totals in various places on the trip details. The expenses should not be a primary focus of this app, but it is nice to know what the estimated costs was."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Record an Estimated Cost for an Itinerary Item (Priority: P1)

As a person planning a trip, I want to attach an estimated cost to the individual events or activities in my itinerary so I can capture what I expect each part of the trip to cost.

**Why this priority**: Capturing the cost estimate at the item level is the foundation for every total shown elsewhere. Without the ability to enter an estimate, no rollups or summaries can exist, so this is the minimum viable slice that delivers standalone value (a user can record and revisit an estimate for any item).

**Independent Test**: Open an event within a trip, enter an estimated cost, save it, reopen the event, and confirm the estimate is retained and displayed on that item.

**Acceptance Scenarios**:

1. **Given** an event in a trip that has no estimated cost, **When** the user enters an estimated cost amount and saves, **Then** the estimated cost is stored and shown on that event.
2. **Given** an event that already has an estimated cost, **When** the user edits the amount and saves, **Then** the updated amount replaces the previous value.
3. **Given** an event that already has an estimated cost, **When** the user clears the amount and saves, **Then** the event is treated as having no estimated cost and is excluded from totals.
4. **Given** the user enters an invalid amount (for example, non-numeric text or a negative value), **When** they attempt to save, **Then** the system rejects the entry and explains what a valid amount looks like.

---

### User Story 2 - See a Total Estimated Cost for the Whole Trip (Priority: P2)

As a person reviewing a trip, I want to see the combined estimated cost of everything in the trip displayed on the trip details so I can understand the overall projected spend at a glance.

**Why this priority**: A trip-wide total is the single most requested summary and turns individual estimates into a meaningful, high-level figure. It builds directly on Story 1 and delivers the "nice to know what the estimated cost was" outcome without requiring finer-grained breakdowns.

**Independent Test**: Add estimated costs to several events within a trip, open the trip details, and confirm the displayed trip total equals the sum of all entered estimates.

**Acceptance Scenarios**:

1. **Given** a trip with several events that have estimated costs, **When** the user views the trip details, **Then** a trip-level total equal to the sum of all item estimates is displayed.
2. **Given** a trip where no items have estimated costs, **When** the user views the trip details, **Then** the total is shown as zero or a clear "no estimates yet" indication rather than an error or blank value.
3. **Given** a trip total is displayed, **When** the user adds, edits, or removes an item estimate, **Then** the displayed trip total reflects the change the next time the trip details are viewed.
4. **Given** the estimated total is presented, **When** the user views the trip details, **Then** the total is shown in a secondary, non-dominant position consistent with expenses being a supporting detail rather than a primary focus.

---

### User Story 3 - See Estimated Cost Totals Per Day or Leg (Priority: P3)

As a person reviewing the flow of a trip, I want to see the estimated cost subtotal for each day or trip leg so I can understand how projected spending is distributed across the trip.

**Why this priority**: Per-day and per-leg subtotals are a convenience layered on top of the trip total. They help users reason about where money is concentrated, but the core value is already delivered by Stories 1 and 2, so this is the lowest priority.

**Independent Test**: Add estimated costs to events spread across different days or legs, view the timeline or leg breakdown, and confirm each day/leg shows a subtotal equal to the sum of its own items and that the subtotals add up to the trip total.

**Acceptance Scenarios**:

1. **Given** a trip whose events span multiple days or legs and carry estimated costs, **When** the user views the timeline or leg breakdown, **Then** each day or leg displays a subtotal equal to the sum of the estimates for items within it.
2. **Given** per-day or per-leg subtotals are displayed, **When** the user adds them together, **Then** their sum equals the trip-level total.
3. **Given** a day or leg with no estimated costs, **When** the user views its subtotal, **Then** it is shown as zero or a clear "no estimates" indication rather than being omitted in a confusing way.

---

### Edge Cases

- What happens when an item's estimated cost is zero versus having no estimate at all? Zero is a recorded value included in counts; no estimate is excluded from totals.
- How does the system handle a very large estimated total that could affect layout or readability in the summary areas?
- How are estimates handled when an item they were attached to is deleted? The estimate is removed with the item and no longer contributes to any total.
- How does the system present totals when some items have estimates and others do not, so the total is understood as a partial estimate rather than a complete budget?
- What happens if the same amount is entered with excessive decimal places or unusual formatting? The value is normalized to a consistent, sensible precision.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Users MUST be able to record an estimated cost amount on an individual trip event or activity.
- **FR-002**: Users MUST be able to edit the estimated cost on an item that already has one.
- **FR-003**: Users MUST be able to remove an estimated cost from an item so it no longer contributes to any total.
- **FR-004**: System MUST validate estimated cost entries, rejecting non-numeric and negative values and communicating the acceptable format.
- **FR-005**: System MUST persist estimated cost values so they remain associated with their item across sessions.
- **FR-006**: System MUST display a trip-level total that is the sum of all item estimates within the trip.
- **FR-007**: System MUST display estimated cost subtotals at the per-day or per-leg level within the trip details.
- **FR-008**: System MUST exclude items that have no estimated cost from all totals while still including items whose estimate is explicitly zero.
- **FR-009**: System MUST keep displayed totals consistent with the underlying item estimates whenever the trip details are viewed after a change.
- **FR-010**: System MUST present estimated cost information as a secondary, supporting detail that does not dominate the trip details experience.
- **FR-011**: System MUST clearly indicate when a trip, day, or leg has no estimates rather than showing a blank or ambiguous value.
- **FR-012**: System MUST present all estimated amounts using a single, consistent currency and formatting throughout the trip details.

### Key Entities *(include if feature involves data)*

- **Estimated Cost**: A monetary estimate attached to a single itinerary item (event/activity). Key attributes: amount and its association to the item it belongs to. A missing estimate is distinct from an estimate of zero.
- **Trip Estimate Summary**: A derived, non-persistent rollup representing the total estimated cost for a trip and its subtotals per day or leg. It is calculated from the individual item estimates rather than stored independently.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user can add or update an estimated cost on an item and see it reflected in the relevant totals within the same viewing session.
- **SC-002**: The displayed trip-level total exactly equals the sum of all item estimates in the trip in 100% of cases.
- **SC-003**: The sum of all per-day or per-leg subtotals exactly equals the trip-level total in 100% of cases.
- **SC-004**: A user can locate and record an estimated cost for an item in under 30 seconds without referring to help.
- **SC-005**: In usability review, expenses are consistently perceived as a supporting detail rather than a primary feature of the trip details.

## Assumptions

- Estimated costs are attached at the individual event/activity level; there is no separate standalone expense list disconnected from itinerary items.
- All estimates within a trip share a single currency; multi-currency entry and conversion are out of scope for this feature.
- Estimates are simple monetary amounts without categories, tax breakdowns, per-person splits, or actual-versus-estimated reconciliation.
- Totals (trip, day, and leg) are calculated on demand from item estimates rather than stored as separate persisted values.
- The existing trip, trip leg, and event structures are reused; no new top-level navigation area is introduced for expenses.
- Amounts are non-negative and normalized to a consistent decimal precision suitable for the display currency.
