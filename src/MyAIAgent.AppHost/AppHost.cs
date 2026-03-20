var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.MyAIAgent_McpServer_Presentation>("mcp-server")
    .WithExternalHttpEndpoints();

builder.Build().Run();
