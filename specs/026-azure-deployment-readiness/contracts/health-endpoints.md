# Production Health Endpoint Contract

Web and API expose the following unauthenticated operational endpoints for Azure Container Apps probes. Responses never include dependency names, exception messages, connection strings, tokens, user data, or configuration values.

## `GET /alive`

Purpose: liveness check for process responsiveness.

- Returns `200 OK` with a small plain-text or JSON body when the process event loop is responsive.
- Does not call PostgreSQL, Azure OpenAI, Entra, Blob storage, Key Vault, or other remote dependencies.
- Returns `503 Service Unavailable` only when an in-process liveness check explicitly fails.
- Container Apps uses this endpoint for the `Liveness` probe.

Example success body:

```json
{"status":"Healthy"}
```

## `GET /health`

Purpose: readiness check for accepting user traffic.

- Returns `200 OK` when startup initialization is complete and required runtime dependencies are available.
- Returns `503 Service Unavailable` while database migration is running or failed, PostgreSQL is unavailable, required configuration is invalid, or another mandatory dependency is unavailable.
- Optional disabled integrations are reported internally as not applicable and do not fail readiness.
- Public response contains only aggregate status.
- Container Apps uses this endpoint for the `Readiness` probe.

Example unavailable body:

```json
{"status":"Unhealthy"}
```

## Startup Probe

Container Apps uses `GET /alive` as the `Startup` probe with a failure window long enough for scale-from-zero startup and serialized migrations. Liveness and readiness evaluation begin after startup succeeds.

## Security and Cache Rules

- Both endpoints are HTTPS-only through external ingress.
- Responses set `Cache-Control: no-store`.
- No detailed health UI is publicly exposed.
- Detailed causes are emitted to structured telemetry with the release ID and component name, subject to telemetry redaction.

## Service Semantics

| Service | `/alive` | `/health` required checks |
|---------|----------|---------------------------|
| Web | Process responsiveness | API reachability, required auth configuration, data-protection key persistence initialized |
| API | Process responsiveness | Migration complete, PostgreSQL connectivity, required Azure OpenAI configuration and connectivity policy |

A dependency failure affects readiness, not liveness, so Container Apps removes the replica from traffic without repeatedly restarting an otherwise healthy process.
