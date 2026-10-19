# Public streaming chat API

The gateway exposes a streaming chat API under `/public` for browser chat
widgets served from origins you allow. Responses use
Server-Sent Events (SSE) over `POST`. The gateway only validates JWTs; your own
backend issues them.

## Enabling it

Public chat is off until a JWT setting is configured. Partial or ambiguous
configuration stops the gateway at startup.

| Setting | Required | Notes |
| --- | --- | --- |
| `PublicChat:Jwt:Issuer` | yes | Must equal the token `iss` claim |
| `PublicChat:Jwt:Audience` | yes | Must equal the token `aud` claim |
| `PublicChat:Jwt:JwksUri` | one of these two | Asymmetric keys (RS256/384/512, ES256/384/512). HTTPS only. Recommended: only the issuer holds the private key |
| `PublicChat:Jwt:SigningKey` | one of these two | Shared HS256 secret, at least 32 bytes. Keep it in Key Vault or user-secrets |
| `PublicChat:AgentId` | production | Agent every public conversation uses. Clients cannot choose the agent |
| `PublicChat:AllowedOrigins` | yes | Origins allowed to call the API from a browser, for example `https://app.example.com`. Exact HTTPS origins only, no wildcards; there is no built-in default |
| `PublicChat:TrustForwardedHeaders` | no | Set `true` only when the app is reachable solely through a proxy such as the Container Apps ingress. Needed so rate limits see the real client IP; when it is `false` behind a proxy, every visitor shares one IP budget. Only enable it when the gateway cannot be reached except through that proxy, because the right-most `X-Forwarded-For` entry is then trusted |
| `PublicChat:RateLimits` | no | `IpPermitLimit` (60), `IpWindowMinutes` (10), `VisitorBurst` (20), `VisitorReplenishSeconds` (30), `MaxConcurrentStreams` (10, per instance), `MaxConcurrentStreamsPerVisitor` (2) |

Locally, set the key with user-secrets (never commit it):

```bash
dotnet user-secrets --project src/MyAIAgent.Gateway.Presentation set "PublicChat:Jwt:Issuer" "http://localhost"
dotnet user-secrets --project src/MyAIAgent.Gateway.Presentation set "PublicChat:Jwt:Audience" "chat-api"
dotnet user-secrets --project src/MyAIAgent.Gateway.Presentation set "PublicChat:Jwt:SigningKey" "$(openssl rand -base64 48)"
```

## Token contract

Your backend signs one JWT per visitor and sends it as `Authorization: Bearer <jwt>`.

| Claim | Requirement |
| --- | --- |
| `iss`, `aud` | Must match the configured values |
| `sub` | Stable visitor id, 1 to 128 characters. Conversations are owned by `visitor:<sub>` |
| `exp` | Required. Short lifetimes such as one hour, renewed by your backend, are recommended |

Tokens with another issuer or audience, an expired token, a bad signature, an
unexpected algorithm, or a missing or overlong `sub` get `401`. Microsoft Entra
tokens are not accepted here, and visitor tokens are not accepted by the
Entra-protected routes.

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

Leave `sessionId` out for a new conversation and reuse the id from the `session`
event to continue one. An invalid body returns `400` as normal JSON before any
streaming starts. After that the status is `200` and failures arrive as an `error` event.
A `sessionId` that this visitor does not own, or that does not exist, is not an error: the request starts a
new conversation and the `session` event carries the new id. Only the conversation routes below answer `404`.

Events, in order (each is an SSE `event:` with a JSON `data:` payload that repeats the type):

| Event | Payload | Notes |
| --- | --- | --- |
| `session` | `{"type":"session","sessionId":"..."}` | First event, so a new conversation id is known immediately |
| `delta` | `{"type":"delta","text":"..."}` | Zero or more reply chunks |
| `title` | `{"type":"title","title":"..."}` | Only on the first turn of a new conversation |
| `done` | `{"type":"done","sessionId":"..."}` | Last event. The turn was saved before `title` was sent |
| `error` | `{"type":"error","message":"..."}` | Terminal. Nothing is saved for that turn |

The `session` event is sent before the reply exists. If the stream fails with an
`error` event, or the client disconnects before the reply finishes, nothing is saved
and that `sessionId` does not exist: start a new conversation instead of resuming it.
The title and the history summary are best effort. If the model is slow or fails, the
title falls back to the start of your first message and the summary is retried on a later turn.

Failures between the gateway and the Orchestrator arrive as an `error` event on a `200` stream.
A visitor can hold at most two streams at once, and IPv6 clients share one budget per `/64` prefix.
Rate limits answer `429` with a `Retry-After` header. Limits are held per gateway
instance, so with several replicas the effective limit is the configured value
times the replica count.

## Browser example

`EventSource` cannot send `POST` or an `Authorization` header, so read the stream with `fetch`:

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

- Visitors can write to the history, so the stored summary is replayed to the model as user content, not as a system message. Give the public agent only low-risk, read-only tools.
- The Orchestrator trusts the `userId` it is given. It must stay on an internal ingress; consider enabling its `ServiceAuthorization` API key.
- Conversation history keeps the last messages verbatim and folds older turns into a
  stored summary, so long conversations stay within the model's context.
- The same streaming and conversation routes exist for Entra callers at
  `POST /chat/stream` and `/conversations...`, guarded by the `ChatAccess` policy.
- Conversations live in Cosmos DB (`ChatSessions` container). Local development uses the test database, so
  conversations you create while testing are visible to anyone else using the same test database.
