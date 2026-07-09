# Data Model: Trip Index Motivation

## Trip Index Introduction

Represents the descriptive copy shown in the Trips page header.

**Fields**

- `text`: The visible opening sentence or short paragraph.
- `placement`: Header area beneath the `Trips` page title.
- `audience`: Signed-in travelers viewing owned and shared trips.

**Relationships**

- Appears before loading, empty, sparse, and populated trip-list states.
- Complements the existing create-trip action without replacing it.

**Validation Rules**

- Must accurately describe owned and shared trips.
- Must remain useful when the user has zero trips or many trips.
- Must avoid technical language and outdated brand terms covered by brand copy tests.

## Motivational Travel Fact

Represents a short, curated piece of page-supporting copy.

**Fields**

- `title`: Short fact label or lead-in.
- `body`: One concise sentence related to practical trip planning motivation.
- `theme`: Planning idea such as buffers, confirmations, sharing, or daily organization.
- `isInteractive`: False for the initial implementation.

**Relationships**

- Displayed as supporting content on the Trips page when the list is empty or sparse.
- May be rendered inside or adjacent to the existing empty-state component.

**Validation Rules**

- Body should be under 140 characters where practical.
- Must be static and deterministic for test stability.
- Must not require attribution or copyrighted quoted material.
- Must not introduce keyboard focus stops unless it becomes actionable.

## Sparse Trip List State

Represents the page state where motivational content can appear without competing with trip content.

**Fields**

- `totalCount`: Number of trips returned by the trip list response.
- `page`: Current trip list page.
- `pageSize`: Requested trip list page size.
- `shouldShowFacts`: Whether motivational facts should appear.

**Relationships**

- Computed from the existing `TripListResponse` in the web component.
- Does not alter API requests or pagination behavior.

**Validation Rules**

- Must be true when `totalCount` is zero.
- May be true for a small first page of trips if the facts remain secondary.
- Must not hide or reorder trip cards when trips are present.
- Must be false or visually minimized when enough trip content exists that facts would reduce scannability.
