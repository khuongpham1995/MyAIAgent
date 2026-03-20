using MyAIAgent.McpServer.Application.Extensions;
using MyAIAgent.McpServer.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddLogging();
builder.Services
    .AddMcpServer() // Scan assembly for tools, prompts, etc.
    .WithHttpTransport() // Enable SSE and POST endpoints (/mcp/stream, /mcp)
    //.WithStdioServerTransport() // Enable stdio transport
    .WithToolsFromAssembly(); // Scan assembly for tools, prompts, etc.;

var app = builder.Build();
app.MapMcp();
await app.RunAsync();