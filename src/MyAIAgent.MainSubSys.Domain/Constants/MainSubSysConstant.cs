namespace MyAIAgent.MainSubSys.Domain.Constants;

public static class MainSubSysConstant
{
    public static class Routes
    {
        public const string Chat = "/chat";
        public const string AdminDocuments = "/admin/documents";
        public const string AdminTools = "/admin/tools";
    }

    public static class Errors
    {
        public const string ChatFailed = "Chat request failed.";
        public const string DocumentUploadFailed = "Document upload failed.";
        public const string ToolListFailed = "Tool listing failed.";
        public const string AgentListFailed = "Agent listing failed.";
        public const string AgentCreationFailed = "Agent creation failed.";
        public const string AgentDeletionFailed = "Agent deletion failed.";
    }

    public static class Config
    {
        public const string OrchestratorBaseUrl = "Orchestrator:BaseUrl";
        public const string KernelMemoryBaseUrl = "KernelMemory:BaseUrl";
        public const string McpServerBaseUrl = "McpServer:BaseUrl";
    }
}
