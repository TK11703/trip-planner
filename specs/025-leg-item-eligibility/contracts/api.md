# API Contract: Travel Leg Modes and Item Eligibility

All routes and authorization rules remain unchanged. JSON uses the application's existing camel-case serialization.

## Enumerated Values

- `legKind`: `stay` | `travel`
- `transportationMode`: `flight` | `train` | `bus` | `boat` | `car`

## Create Trip Leg

`POST /api/trips/{tripId}/legs`

```json
{
  "title": "Flight to Honolulu",
  "legKind": "travel",
  "transportationMode": "flight",
  "origin": "Washington Dulles International Airport",
  "destination": "Daniel K. Inouye International Airport",
  "startLocal": "2026-08-01T08:35:00",
  "startTimeZoneId": "America/New_York",
  "endLocal": "2026-08-01T14:55:00",
  "endTimeZoneId": "Pacific/Honolulu",
  "travelCost": 642.18,
  "confirmationCode": "ABC123",
  "notes": "Terminal details pending"
}
```

Stay example:

```json
{
  "title": "Honolulu",
  "legKind": "stay",
  "transportationMode": null,
  "origin": null,
  "destination": "Honolulu, HI",
  "startLocal": "2026-08-01T15:30:00",
  "startTimeZoneId": "Pacific/Honolulu",
  "endLocal": "2026-08-05T10:00:00",
  "endTimeZoneId": "Pacific/Honolulu",
  "travelCost": null,
  "confirmationCode": null,
  "notes": null
}
```

Every Travel mode permits `travelCost` and `confirmationCode` to be null. When supplied, cost must be nonnegative with at most two decimal places and confirmation must be nonblank after trimming and no longer than 255 characters.

### Outcomes

- `201 Created`: leg persisted.
- `400 Bad Request`: field-level validation, including invalid kind/mode combinations, invalid supplied booking details, negative/over-precision cost, or an attempted transition to a restricted mode while items exist.
- `404 Not Found`: trip/leg absent or caller cannot modify it, preserving existing non-disclosure behavior.

## Update Trip Leg

`PUT /api/trips/{tripId}/legs/{tripLegId}`

Request shape and field rules are identical to Create. Update is atomic: if the requested resulting state cannot contain current items, no leg field changes.

## Trip Leg Detail

Every existing `TripLegDto` occurrence adds:

```json
{
  "legKind": "travel",
  "transportationMode": "train",
  "travelCost": 89.50,
  "confirmationCode": "RAIL-2048",
  "canContainItems": false
}
```

`canContainItems` is response-only and derived from kind/mode. Clients use it for presentation; the API/database still validate every write.

## Timeline Leg

Every timeline leg adds the same five fields. Existing `estimatedCostTotal` remains the subtotal of child items. `travelCost` is displayed separately, and the existing trip-level estimated total includes both.

## Item Create and Update

Existing routes and request shapes do not change.

When `tripLegId` names Flight, Train, Bus, or Boat, return:

```json
{
  "code": "validation_failed",
  "message": "Items cannot be assigned to a flight, train, bus, or boat leg.",
  "field": "tripLegId"
}
```

Stay and Car continue through existing ownership and instant-containment validation. Null remains valid for unassigned items.

## Email Draft Placement

Placement responses retain their existing shape. Candidate lists contain only Stay and Car legs. A trip with zero eligible legs still participates in no-cover/gap evaluation and supports confirmation without a leg.