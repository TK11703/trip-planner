# Contract: Logic App Relay Responsibilities

**Feature**: 022-accept-email-content

The Logic App is provisioned and operated **outside this repository**. This contract records the boundary so the API is built against a stable expectation. Nothing here is implemented as application code.

## Division of Responsibility

| Concern | Owner |
|---|---|
| Connecting to and monitoring the mailbox | Logic App |
| Detecting new messages | Logic App |
| Decomposing the message into sender/subject/body/attachments | Logic App |
| Calling the ingestion endpoint | Logic App |
| Retry and dead-lettering | Logic App |
| Spam and malware filtering | Mail platform, ahead of the Logic App |
| Authorizing the caller | API |
| Matching sender to traveler | API |
| Duplicate detection | API |
| Recognition, draft creation, notification | API |
| Traveler review and confirmation | API + Web |

The API performs **no** mailbox connection, discovery, or polling of any kind.

## Expected Relay Flow

1. Trigger: *When a new email arrives* on the monitored mailbox.
2. Project the message into the ingestion payload — see [relay-ingestion-endpoint.md](relay-ingestion-endpoint.md).
   - Prefer the mail platform's internet message identifier for `messageId`.
   - Send the original sender's address as `sender`. For a forwarded message this is the traveler's own address, which is what enables attribution.
   - Base64-encode each attachment into `attachments[].contentBase64`.
3. Acquire a token for the API's app registration using the Logic App's **managed identity**.
4. `POST /api/email-ingestion/messages` with that bearer token.
5. Branch on the response `status`:

| Response | Relay behavior |
|---|---|
| `200` `parsed` / `no_content` / `duplicate` | Complete — mark the message handled |
| `400` / `413` / `422` | Do not retry — move to a dead-letter folder and alert |
| `502` `processing_failed` | Retry with exponential backoff, then dead-letter |
| `401` / `403` | Alert — identity or app role misconfiguration |

## Configuration Required Outside Code

- An Entra app role named `EmailIngestion.Relay` exposed on the API app registration.
- That app role assigned to the Logic App's managed identity.
- The API base URL and target audience supplied to the Logic App.
- Attachment size limits on the relay aligned to the API caps (5 MB per attachment, 10 MB per request).

## Non-Goals

- The API does not expose a subscription-validation handshake. That was an Event Grid concern and is removed.
- The API does not accept the Event Grid event envelope.
- The API does not provide a status-polling endpoint for the relay; every call returns a conclusive outcome.
