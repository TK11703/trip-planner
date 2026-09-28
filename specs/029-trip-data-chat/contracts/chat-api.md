# Trip Data Chat API Contract

## `POST /api/chat/messages`

Authenticated by the existing API bearer-token policy. The API derives caller ID/email from validated claims; request content cannot select a user, owner, trip, or permission level. The route is implemented as a Minimal API vertical slice.

### Request

```json
{
  "message": "Which trip includes Seattle, and when does the first leg start?",
  "priorUserMessages": [
    "Compare my trips that include the Pacific Northwest."
  ]
}
```

`message` is the current user turn. `priorUserMessages` is optional, bounded context from the current client session and contains user turns only; previous assistant content is never accepted as grounding evidence. Enforce server-side message length and turn-count limits. Ignore/reject any unrecognized identity, trip-access, URL, or write-operation fields. Do not accept a model name/deployment from the caller.

### Successful response

```json
{
  "status": "answered",
  "answer": "Your Seattle trip starts with the flight on June 12.",
  "citations": [
    {
      "tripId": "9eaf8020-8532-4d58-8ba4-98e11d35f081",
      "tripName": "Pacific Northwest",
      "sourceKind": "leg",
      "sourceId": "d6eed804-a26c-4cb6-ad9a-11f54f24b859",
      "sourceLabel": "Flight to Seattle"
    }
  ]
}
```

The answer is plain text, not trusted HTML. Citation fields are resolved by the API from current canonical rows in the authorized retrieval set. `tripId` and `(sourceKind, sourceId)` are stable domain references. The Web client, not the model, constructs the internal trip-detail link.

### Status values and failure behavior

| HTTP/status | Meaning |
|---|---|
| `200 answered` | Supported trip answer; every cited reference is validated against this turn's authorized sources. |
| `200 insufficient_data` | Accessible records were retrieved, but do not support a factual answer; citations may be empty. |
| `200 no_accessible_trip_data` | No trips are currently available to this caller. Do not disclose inaccessible-trip existence. |
| `401` | Existing authentication response; do not run retrieval or model generation. |
| `429` | Request throttled; client may retry without dropping its session transcript. |
| `503` | Foundry/model/retrieval dependency unavailable; return a generic retryable error with no prompt, trip text, or provider detail. |

Malformed requests use the API's normal validation error contract. Never return partial citations if validation fails. If model output cites an unknown retrieval key, discard that citation and fail closed rather than turning model text into a link. The response must not claim a trip fact without at least one validated citation.

## Processing invariants

1. Authenticate and resolve the current caller before any trip query.
2. Build the accessible-trip set using the same owner/member-ID/member-email rules as `GetTripAccess.sql` and filter vector candidates by that set in SQL before ranking.
3. Retrieve canonical current trip/leg/item values by authorized stable IDs; do not use stale duplicated source text as answer evidence.
4. Call the Microsoft Agent Framework agent only after retrieval and source validation. The agent can use retrieved records and prior user questions, but has no direct PostgreSQL connection or mutation tool.
5. Map structured model citation keys to the allowlisted retrieval set. Return no model-generated href, raw user identity, or authorization decision.
6. Re-run authorization for every turn. Trip sharing changes, deletion, or ownership changes take effect on the next request.
7. Keep default logs/traces content-free; record bounded sizes/counts, opaque correlation ID, duration, and outcome category only.

## Read-only guarantee

The endpoint has no trip mutation routes or agent tools. Its only database writes are internal vector-index maintenance, which cannot alter canonical trip/leg/tracked-item/share data. Existing trip-detail endpoints independently enforce access when citations are followed.