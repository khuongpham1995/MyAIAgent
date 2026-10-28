# Public streaming chat API

Connect a browser widget to Gateway's `/public` API using JWTs and POST-based SSE. Your backend issues tokens; Gateway validates them. Start with [local setup](local-development.md). Chat uses the [Orchestrator flow](architecture.md#a-chat-turn), without document retrieval.

## Enabling it

Public chat stays off until JWT configuration begins. Partial/ambiguous settings fail startup.

| Setting | Required | Notes |
| --- | --- | --- |
| `PublicChat:Jwt:Issuer` | yes | Must equal the token `iss` claim |
| `PublicChat:Jwt:Audience` | yes | Must equal the token `aud` claim |
| `PublicChat:Jwt:JwksUri` | one of these two | Asymmetric keys (RS256/384/512, ES256/384/512). HTTPS only. Recommended: only the issuer holds the private key |
| `PublicChat:Jwt:SigningKey` | one of these two | Shared HS256 secret, at least 32 bytes. Keep it in Key Vault or user-secrets |
| `PublicChat:AgentId` | production | Agent every public conversation uses. Clients cannot choose the agent |
| `PublicChat:AllowedOrigins` | yes | Origins allowed to call the API from a browser, for example `https://app.example.com`. Exact HTTPS origins only, no wildcards; there is no built-in default |
| `PublicChat:TrustForwardedHeaders` | no | Enable only behind an exclusive trusted proxy; trusts the right-most `X-Forwarded-For`. Otherwise proxy clients share one IP budget. |
| `PublicChat:RateLimits` | no | `IpPermitLimit` (60), `IpWindowMinutes` (10), `VisitorBurst` (20), `VisitorReplenishSeconds` (30), `MaxConcurrentStreams` (10, per instance), `MaxConcurrentStreamsPerVisitor` (2) |

Locally, set the key with user-secrets (never commit it):

```bash
dotnet user-secrets --project src/Services/Gateway/MyAIAgent.Gateway.Presentation set "PublicChat:Jwt:Issuer" "http://localhost"
dotnet user-secrets --project src/Services/Gateway/MyAIAgent.Gateway.Presentation set "PublicChat:Jwt:Audience" "chat-api"
dotnet user-secrets --project src/Services/Gateway/MyAIAgent.Gateway.Presentation set "PublicChat:Jwt:SigningKey" "<private-random-signing-key-at-least-32-bytes>"
dotnet user-secrets --project src/Services/Gateway/MyAIAgent.Gateway.Presentation set "PublicChat:AllowedOrigins:0" "http://localhost:3000"
```

Replace the signing key with a private cryptographically random value and match your browser's origin. Set `PublicChat:AgentId` to an existing agent; required outside Development.

## Token contract

Your backend signs one JWT per visitor and sends it as `Authorization: Bearer <jwt>`.

| Claim | Requirement |
| --- | --- |
| `iss`, `aud` | Must match the configured values |
| `sub` | Stable visitor id, 1 to 128 characters. Conversations are owned by `visitor:<sub>` |
| `exp` | Required. Short lifetimes such as one hour, renewed by your backend, are recommended |

Invalid claims, signature, expiry, algorithm, or `sub` return `401`. Visitor JWTs and Entra tokens are not interchangeable across the two API surfaces.

## Endpoints

| Method | Path | Purpose |
| --- | --- | --- |
| `POST` | `/public/chat/stream` | Send a message and stream the reply |
| `GET` | `/public/conversations?page=1&pageSize=20` | List the visitor's conversations, newest first (page size at most 50) |
| `GET` | `/public/conversations/{id}` | Read a conversation with its messages |
| `PUT` | `/public/conversations/{id}/title` | Rename. Body: `{"title": "..."}` (at most 100 characters) |
| `DELETE` | `/public/conversations/{id}` | Delete a conversation |

A conversation that does not exist or belongs to another visitor returns `404`.

### `POST /public/chat/stream`

Request body (at most 2000 characters of message):

```json
{ "sessionId": null, "userMessage": "Hello" }
```

Omit `sessionId` for a new conversation; reuse the `session` event ID to continue. Unknown/foreign IDs also start a new conversation. For those IDs, conversation get/rename/delete return `404` instead.

Invalid input returns JSON `400` before streaming. Once streaming starts, status is `200` and failures use `error` events.

Each SSE `event:` has a JSON `data:` payload:

| Event | Payload | Notes |
| --- | --- | --- |
| `session` | `{"type":"session","sessionId":"..."}` | First event, so a new conversation id is known immediately |
| `delta` | `{"type":"delta","text":"..."}` | Zero or more reply chunks |
| `title` | `{"type":"title","title":"..."}` | Only on the first turn of a new conversation |
| `done` | `{"type":"done","sessionId":"..."}` | Successful terminal event; the turn is saved |
| `error` | `{"type":"error","message":"..."}` | Failed/interrupted stream; inspect history before retrying |

- `session` precedes persistence. Provider failure before the turn save leaves no new turn; a new session may not exist yet.
- The turn commits **before** optional title/summary work. A later disconnect or downstream truncation can therefore hide an already-saved turn. Check history before resending; missing `done` does not prove a save failed.
- Title/summary work is best effort: title falls back to the first message; summary retries on a later turn.
- Handle terminal `error` and EOF without `done` as unsuccessful streams, including downstream failures on HTTP `200`.

### Rate limits

Default: two streams per visitor. IPv6 budgets group clients by `/64`. Limits return `429` / `Retry-After` and are per Gateway instance; replicas multiply the effective budget. Defaults are configurable in `PublicChat:RateLimits` above.

## Browser example

Use `fetch`; `EventSource` cannot send POST with an Authorization header. In `onEvent`, handle `error` and record `done`; treat EOF without `done` as interrupted.

```js
async function streamChat(token, userMessage, sessionId, onEvent) {
  const response = await fetch("https://<gateway-host>/public/chat/stream", {
    method: "POST",
    headers: { "Content-Type": "application/json", Authorization: `Bearer ${token}` },
    body: JSON.stringify({ sessionId, userMessage }),
  });
  if (!response.ok) throw new Error(`Chat failed with status ${response.status}`);

  const reader = response.body.pipeThrough(new TextDecoderStream()).getReader();
  let buffer = "";
  for (;;) {
    const { value, done } = await reader.read();
    if (done) break;
    buffer += value;
    let boundary;
    while ((boundary = buffer.indexOf("\n\n")) >= 0) {
      const block = buffer.slice(0, boundary);
      buffer = buffer.slice(boundary + 2);
      const data = block.split("\n").filter((l) => l.startsWith("data:")).map((l) => l.slice(5).trim()).join("");
      if (data) onEvent(JSON.parse(data));
    }
  }
}
```

## Operating notes

- Give public agents low-risk, read-only tools. Visitor-controlled summaries are replayed as user content; recent messages remain verbatim.
- Keep Orchestrator internal: it trusts forwarded `userId` and requires `ServiceAuthorization` outside Development.
- Entra callers use `POST /chat/stream` and `/conversations...` under `ChatAccess`.
- Cosmos `ChatSessions` persists history. Others with direct access to the shared test database can read local test data.
