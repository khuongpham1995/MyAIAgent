/**
 * MyAIAgent | Gateway · Presentation
 *
 * Configures deployed Entra JWT authentication and the chat/admin authorization policies for Gateway.
 */

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace MyAIAgent.Gateway.Presentation;

public static class GatewaySecurityExtensions
{
    public const string ChatPolicy = "ChatAccess";
    public const string AdminPolicy = "AgentAdmin";

    public static IServiceCollection AddGatewaySecurity(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isDevelopment)
    {
        if (isDevelopment) return services;

        var tenantId = configuration["Authentication:TenantId"]
            ?? throw new InvalidOperationException("Authentication:TenantId is required in production.");
        var audience = configuration["Authentication:Audience"]
            ?? throw new InvalidOperationException("Authentication:Audience is required in production.");

        if (!Guid.TryParse(tenantId, out _) || !Guid.TryParse(audience, out _))
            throw new InvalidOperationException("Gateway authentication configuration is invalid.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.Authority = $"https://login.microsoftonline.com/{tenantId}/v2.0";
                options.Audience = audience;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    RoleClaimType = "roles"
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(ChatPolicy, policy =>
                policy.RequireAuthenticatedUser()
                    .RequireClaim("tid")
                    .RequireClaim("oid")
                    .RequireRole("Chat.Access", "Agent.Admin"));
            options.AddPolicy(AdminPolicy, policy =>
                policy.RequireAuthenticatedUser().RequireRole("Agent.Admin"));
        });

        return services;
    }
}
