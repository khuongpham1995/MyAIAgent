/**
 * MyAIAgent | Gateway · Presentation
 *
 * Defines stable OpenAPI output shared by Scalar and the offline C# client-generation workflow.
 */

using Microsoft.OpenApi;
using System.Text.Json.Nodes;
using MyAIAgent.Common.Application.Models;

namespace MyAIAgent.Gateway.Presentation;

public static class GatewayOpenApiExtensions
{
    public static IServiceCollection AddGatewayOpenApi(this IServiceCollection services) =>
        services.AddOpenApi("kyle-ai-agent", options =>
        {
            // NSwag's pinned generator consumes OpenAPI 3.0; runtime HTTP contracts are unchanged.
            options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_0;
            options.AddSchemaTransformer((schema, context, _) =>
            {
                // Error status codes are written as integers, regardless of input number-reading options.
                if (context.JsonTypeInfo.Type == typeof(ErrorResponseDto) && schema.Properties is not null)
                    schema.Properties["statusCode"] = new OpenApiSchema
                    {
                        Type = JsonSchemaType.Integer,
                        Format = "int32",
                    };
                return Task.CompletedTask;
            });
            options.AddOperationTransformer((operation, _, _) =>
            {
                // ASP.NET's number-reading union becomes an object in NSwag. Query pagination
                // is represented as integers in the contract while the HTTP binding stays unchanged.
                foreach (var parameter in operation.Parameters ?? [])
                    if (parameter is OpenApiParameter value && value.In == ParameterLocation.Query && value.Name is "page" or "pageSize")
                        value.Schema = new OpenApiSchema
                        {
                            Type = JsonSchemaType.Integer,
                            Format = "int32",
                            Default = JsonValue.Create(value.Name == "page" ? 1 : 20),
                        };
                foreach (var response in operation.Responses?.Values.AsEnumerable() ?? [])
                    if (response.Content?.TryGetValue("text/event-stream", out var stream) == true)
                    {
                        // Consumers receive the open SSE stream, not one buffered JSON object.
                        stream.Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" };
                        response.Description = "SSE stream of JSON ChatStreamEvent messages; consume until done or error.";
                    }
                if (operation.RequestBody?.Content?.TryGetValue("multipart/form-data", out var multipart) == true
                    && multipart.Schema is OpenApiSchema body && body.Properties?.ContainsKey("file") == true)
                {
                    // Inline the binary field so NSwag emits both the upload parameter and its helper type.
                    body.Properties["file"] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" };
                }
                return Task.CompletedTask;
            });
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info.Title = "MyAIAgent Gateway";
                document.Info.Version = "v1";
                document.Servers = [];
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes["EntraBearer"] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "Entra access token required outside Development. Chat/conversations require Chat.Access or Agent.Admin; /admin requires Agent.Admin. Development uses the shared local identity.",
                };
                document.Components.SecuritySchemes["VisitorBearer"] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "Visitor JWT required on /public routes, including Development. Tokens must match the configured visitor issuer and audience; they are not Entra tokens.",
                };
                foreach (var (path, item) in document.Paths)
                    foreach (var operation in item.Operations?.Values.AsEnumerable() ?? [])
                    {
                        if (path.StartsWith("/health/", StringComparison.Ordinal))
                        {
                            operation.Security = [];
                            continue;
                        }
                        var scheme = path.StartsWith("/public/", StringComparison.Ordinal) ? "VisitorBearer" : "EntraBearer";
                        operation.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(scheme, document)] = [] }];
                    }
                return Task.CompletedTask;
            });
        });
}
