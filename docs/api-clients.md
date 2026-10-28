# API consumers and the C# SDK

Consume Gateway over HTTP from your own application. The API's OpenAPI document defines its transport contract; the console is one client, not the owner of that contract.

## Paste JSON and build

1. Obtain the complete OpenAPI JSON from the API you are using. For local Gateway, open `https://localhost:7200/openapi/kyle-ai-agent.json` after starting Aspire. Production does not expose this Development-only endpoint; the repository also includes the full contract below.
2. Replace the contents of `src/Services/Gateway/MyAIAgent.Gateway.Client/OpenApi/openapi.json` and save.
3. Build your consuming project normally:

   ```bash
   dotnet build src/Clients/MyAIAgent.ConsoleClient
   ```

The SDK's pinned **NSwag.MSBuild 14.7.1** target generates the methods and models before compilation. It processes every operation in the supplied document. Saving the JSON takes effect on the next build; no npm command, running API, Azure sign-in, or application configuration is needed for generation.

Generated C# lives under `src/Services/Gateway/MyAIAgent.Gateway.Client/obj/<configuration>/net10.0/`. It is ignored, rebuilt from the input, and never edited manually. A malformed document fails the build. Initial package restore needs NuGet access; the API document is a local build input.

The supplied document uses OpenAPI 3.0 and covers normal, administrative, and optional visitor routes. Public routes appear in a running Gateway's document only when configured; copying a smaller document generates a correspondingly smaller SDK. Choose the document for the API surface your application needs.

## Use the SDK from a .NET application

Reference `src/Services/Gateway/MyAIAgent.Gateway.Client/MyAIAgent.Gateway.Client.csproj`. The SDK has no dependency on ConsoleClient, backend projects, Azure configuration, or provider credentials. The generator package is build-only and does not become a runtime dependency of consumers.

Register the generated client with your application's existing HTTP client factory:

```csharp
using MyAIAgent.Gateway.Client;

services.AddHttpClient<GatewayApiClient>(client =>
{
    client.BaseAddress = new Uri("https://gateway.example.com/");
    client.Timeout = Timeout.InfiniteTimeSpan; // Bound each operation with a cancellation deadline.
});
```

For example, an authorized signed-in caller can use:

```csharp
using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
var result = await api.ListConversationsAsync(1, 20, deadline.Token);
```

The consuming application supplies its own authentication and HTTP policies. Normal routes use Entra access tokens outside Development; administrative routes require the admin role. Visitor routes use visitor JWTs and server-selected agents. The SDK does not issue tokens or bypass permissions. See [visitor authentication](public-chat-api.md) and [security](security.md).

The OpenAPI document names these separate schemes `EntraBearer` and `VisitorBearer`, marks their protected operations, and leaves health endpoints anonymous. The Entra scheme description identifies the Development exception and required app roles. Choose deadlines appropriate to each call; nonstreaming chat can wait for model completion before returning headers.

Backend operation results belong to `Common.Application`; entities remain in `Common.Domain`.
The SDK owns HTTP calls and generated wire representations; consumers do not reference backend implementation types.
There is no additional shared contracts project. When copying the SDK project out of this repository, also
carry over its required target/compiler properties and the NSwag package version from the root build files,
or define those settings in the consuming repository's build configuration.

## Streaming and uploads

`StreamChatAsync` and `PublicStreamChatAsync` return a disposable `FileResponse` with an open `Stream`. Read its SSE events incrementally and dispose the response. The wire events are `session`, `delta`, `title`, `done`, and `error`; EOF without `done` is interrupted. The SDK returns the transport stream so callers can choose their own UI and event handling.

Pass the operation's cancellation token to stream reads too, so the deadline covers both response headers and body consumption.

`UploadDocumentAsync` accepts a generated `FileParameter` and returns the typed upload envelope. Admin operations remain available to authorized consumers even when ConsoleClient does not expose them in its menus.

ConsoleClient adds its own local destination restrictions, bounded reads, safe terminal output, cancellation handling, and incomplete-reply display. Those UI policies stay outside the reusable SDK. Other consumers choose their own timeouts, response limits, logging, and replay policies; retries of chat or uploads require particular care because they can repeat writes or paid work.

## Maintain the API contract

Gateway application-error envelopes use `ResponseModel<ErrorResponseDto>`, where `data.statusCode` matches
the HTTP status and `message`/`errors` carry the existing failure details. This replaces null/default failure
payloads, such as `data: false`, with a typed status object. Success responses and HTTP status codes are unchanged.

Gateway route/schema tests compare the SDK input with an isolated export of the real route mappings, including visitor routes enabled with test-only settings. They do not query Azure or providers. After an intentional API change, update the JSON from the API or produce the offline export:

```bash
dotnet test tests/Services/Gateway/MyAIAgent.Gateway.Tests --filter FullyQualifiedName~GatewayOpenApiTests.ExportDocument
```

Copy `artifacts/openapi/gateway.json` into the SDK's `OpenApi/openapi.json`, then build. This export is a repository maintenance aid; consumer builds never run it. `npm run quality` builds the SDK and runs the schema, consumer, console, and backend tests.

No SDK package is published automatically. Clients in other languages can generate from the same OpenAPI JSON using their preferred tooling.
