var builder = DistributedApplication.CreateBuilder(args);

var mcpServer = builder.AddProject<Projects.MyAIAgent_McpServer_Presentation>("mcp-server")
    .WithExternalHttpEndpoints();

var kernelMemory = builder.AddProject<Projects.MyAIAgent_KernelMemory_Presentation>("kernel-memory")
    .WithExternalHttpEndpoints();

var orchestrator = builder.AddProject<Projects.MyAIAgent_Orchestrator_Presentation>("orchestrator")
    .WithExternalHttpEndpoints()
    .WithReference(mcpServer)
    .WithReference(kernelMemory);

builder.AddProject<Projects.MyAIAgent_Gateway_Presentation>("gateway")
    .WithExternalHttpEndpoints()
    .WithReference(orchestrator)
    .WithReference(kernelMemory)
    .WithReference(mcpServer);

builder.Build().Run();
