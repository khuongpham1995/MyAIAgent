/**
 * MyAIAgent | Gateway · Domain
 *
 * Centralizes Gateway configuration keys, validation patterns, authentication names, and public error messages.
 */

namespace MyAIAgent.Gateway.Domain.Constants;

public static class GatewayConstant
{
    public static class Routes
    {
        public const string Chat = "/chat";
        public const string AdminDocuments = "/admin/documents";
        public const string AdminTools = "/admin/tools";
    }

    public const string SessionIdPattern = "^[A-Za-z0-9-]{1,64}$";

    public static class Errors
    {
        public const string ChatFailed = "Chat request failed.";
        public const string DocumentUploadFailed = "Document upload failed.";
        public const string ToolListFailed = "Tool listing failed.";
        public const string AgentListFailed = "Agent listing failed.";
        public const string AgentCreationFailed = "Agent creation failed.";
        public const string AgentDeletionFailed = "Agent deletion failed.";
        public const string ConversationListFailed = "Conversation listing failed.";
        public const string ConversationLoadFailed = "Conversation loading failed.";
        public const string ConversationRenameFailed = "Conversation rename failed.";
        public const string ConversationDeletionFailed = "Conversation deletion failed.";
        public const string ConversationNotFound = "Conversation not found.";
    }

    public static class Config
    {
        public const string OrchestratorBaseUrl = "Orchestrator:BaseUrl";
        public const string KernelMemoryBaseUrl = "KernelMemory:BaseUrl";
        public const string McpServerBaseUrl = "McpServer:BaseUrl";
        public const string OrchestratorAccessKey = "Orchestrator:AccessKey";
        public const string ServiceAuthorizationHeader = "Authorization";
    }

    public static class PublicChat
    {
        public const string Scheme = "PublicVisitor";
        public const string Policy = "PublicChat";
        public const string CorsPolicy = "PublicWidget";
        public const string RateLimitPolicy = "PublicChatLimit";
        public const string VisitorUserIdPrefix = "visitor:";

        public const string AllowedOrigins = "PublicChat:AllowedOrigins";
        public const string AgentId = "PublicChat:AgentId";
        public const string JwtIssuer = "PublicChat:Jwt:Issuer";
        public const string JwtAudience = "PublicChat:Jwt:Audience";
        public const string JwtJwksUri = "PublicChat:Jwt:JwksUri";
        public const string JwtSigningKey = "PublicChat:Jwt:SigningKey";
    }
}
