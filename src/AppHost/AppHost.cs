var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.McpServer_Presentation>("mcp-server")
    .WithExternalHttpEndpoints();

builder.Build().Run();
