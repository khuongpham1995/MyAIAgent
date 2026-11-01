/**
 * MyAIAgent | Gateway · Presentation
 *
 * Maps the OpenAPI document and Scalar reference only in Development.
 */

using Scalar.AspNetCore;

namespace MyAIAgent.Gateway.Presentation;

public static class DocumentationEndpoints
{
    public static IEndpointRouteBuilder MapDocumentationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        if (endpoints.ServiceProvider.GetRequiredService<IHostEnvironment>().IsDevelopment())
        {
            endpoints.MapOpenApi();
            endpoints.MapScalarApiReference(options =>
            {
                options.Title = "Kyle's AI Agent";
                options.AddDocument("kyle-ai-agent", isDefault: true);
                options.WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
            });
        }

        return endpoints;
    }
}
