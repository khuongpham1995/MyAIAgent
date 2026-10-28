/**
 * MyAIAgent | Gateway · Presentation
 *
 * Wires visitor JWT validation, CORS, forwarding, and rate-limit middleware for optional public chat.
 */

using System.Net;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using MyAIAgent.Gateway.Domain.Constants;

namespace MyAIAgent.Gateway.Presentation.PublicApi;

public static class PublicChatExtensions
{
    public const string SubjectClaim = "sub";
    private const int MaxSubjectLength = 128;
    private const string PublicPathPrefix = "/public";
    private const string StreamPath = "/public/chat/stream";

    private static readonly string[] AsymmetricAlgorithms =
        ["RS256", "RS384", "RS512", "ES256", "ES384", "ES512"];

    public static IServiceCollection AddPublicChat(
        this IServiceCollection services,
        PublicChatSettings settings,
        bool isDevelopment)
    {
        services.AddSingleton(settings);

        services.AddAuthentication()
            .AddJwtBearer(GatewayConstant.PublicChat.Scheme, options =>
            {
                options.MapInboundClaims = false;
                options.RequireHttpsMetadata = !isDevelopment;
                options.TokenValidationParameters = BuildValidationParameters(settings);

                if (settings.JwksUri is not null)
                    options.ConfigurationManager = new JwksConfigurationManager(
                        settings.JwksUri, settings.Issuer, requireHttps: !isDevelopment);

                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        if (!IsValidSubject(context.Principal?.FindFirstValue(SubjectClaim)))
                            context.Fail("A valid sub claim is required.");

                        return Task.CompletedTask;
                    },
                };
            });

        services.AddAuthorization(options =>
            options.AddPolicy(GatewayConstant.PublicChat.Policy, policy =>
                policy.AddAuthenticationSchemes(GatewayConstant.PublicChat.Scheme)
                    .RequireAuthenticatedUser()
                    .RequireClaim(SubjectClaim)));

        services.AddCors(options =>
            options.AddPolicy(GatewayConstant.PublicChat.CorsPolicy, policy =>
                policy.WithOrigins([.. settings.AllowedOrigins])
                    .WithHeaders("Content-Type", "Authorization")
                    .WithMethods("GET", "POST", "PUT", "DELETE")
                    .SetPreflightMaxAge(TimeSpan.FromHours(1))));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, _) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);

                return ValueTask.CompletedTask;
            };
            options.GlobalLimiter = BuildRateLimiter(settings.RateLimits);
        });

        if (settings.TrustForwardedHeaders)
        {
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                // Only the right-most entry, appended by the platform ingress, is trusted; earlier ones are client-supplied.
                options.ForwardLimit = 1;
                // Deployment guarantees the app is reachable only through that ingress, whose address is not fixed.
                options.KnownProxies.Clear();
                options.KnownIPNetworks.Clear();
            });
        }

        return services;
    }

    public static IApplicationBuilder UsePublicChat(this WebApplication app, PublicChatSettings settings)
    {
        if (settings.TrustForwardedHeaders)
            app.UseForwardedHeaders();

        app.UseCors();
        app.UseAuthentication();

        // UseAuthentication only runs the default (Entra) scheme, so visitor tokens are resolved here
        // before the rate limiter needs the visitor identity.
        app.UseWhen(
            context => context.Request.Path.StartsWithSegments(PublicPathPrefix),
            branch => branch.Use(async (context, next) =>
            {
                var result = await context.AuthenticateAsync(GatewayConstant.PublicChat.Scheme);
                if (result.Succeeded)
                    context.User = result.Principal;

                await next();
            }));

        app.UseRateLimiter();
        return app;
    }

    public static string GetVisitorUserId(ClaimsPrincipal user) =>
        GatewayConstant.PublicChat.VisitorUserIdPrefix + user.FindFirstValue(SubjectClaim);

    private static TokenValidationParameters BuildValidationParameters(PublicChatSettings settings)
    {
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = settings.Issuer,
            ValidateAudience = true,
            ValidAudience = settings.Audience,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromMinutes(2),
            NameClaimType = SubjectClaim,
        };

        if (settings.SigningKey is not null)
        {
            parameters.IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey));
            parameters.ValidAlgorithms = [SecurityAlgorithms.HmacSha256];
        }
        else
        {
            parameters.ValidAlgorithms = AsymmetricAlgorithms;
        }

        return parameters;
    }

    private static bool IsValidSubject(string? subject) =>
        subject is { Length: > 0 and <= MaxSubjectLength };

    private static PartitionedRateLimiter<HttpContext> BuildRateLimiter(PublicChatRateLimits limits)
    {
        var perIp = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            !IsPublicRequest(context)
                ? RateLimitPartition.GetNoLimiter("not-public")
                : RateLimitPartition.GetFixedWindowLimiter(
                    ClientAddress(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limits.IpPermitLimit,
                        Window = TimeSpan.FromMinutes(limits.IpWindowMinutes),
                        QueueLimit = 0,
                    }));

        var perVisitor = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            !IsStreamRequest(context) || context.User.FindFirstValue(SubjectClaim) is not { } subject
                ? RateLimitPartition.GetNoLimiter("not-visitor-stream")
                : RateLimitPartition.GetTokenBucketLimiter(
                    subject,
                    _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = limits.VisitorBurst,
                        TokensPerPeriod = 1,
                        ReplenishmentPeriod = TimeSpan.FromSeconds(limits.VisitorReplenishSeconds),
                        QueueLimit = 0,
                        AutoReplenishment = true,
                    }));

        var concurrentStreams = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            !IsStreamRequest(context)
                ? RateLimitPartition.GetNoLimiter("not-stream")
                : RateLimitPartition.GetConcurrencyLimiter(
                    "streams",
                    _ => new ConcurrencyLimiterOptions
                    {
                        PermitLimit = limits.MaxConcurrentStreams,
                        QueueLimit = 0,
                    }));

        // One visitor (or address, before a token is validated) must not hold every global stream slot.
        var concurrentPerVisitor = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            !IsStreamRequest(context)
                ? RateLimitPartition.GetNoLimiter("not-stream")
                : RateLimitPartition.GetConcurrencyLimiter(
                    context.User.FindFirstValue(SubjectClaim) ?? ClientAddress(context),
                    _ => new ConcurrencyLimiterOptions
                    {
                        PermitLimit = limits.MaxConcurrentStreamsPerVisitor,
                        QueueLimit = 0,
                    }));

        return PartitionedRateLimiter.CreateChained(perIp, perVisitor, concurrentPerVisitor, concurrentStreams);
    }

    private static bool IsPublicRequest(HttpContext context) =>
        context.Request.Path.StartsWithSegments(PublicPathPrefix)
        && !HttpMethods.IsOptions(context.Request.Method);

    private static bool IsStreamRequest(HttpContext context) =>
        context.Request.Path.Equals(StreamPath, StringComparison.OrdinalIgnoreCase)
        && HttpMethods.IsPost(context.Request.Method);

    private static string ClientAddress(HttpContext context) => GetRateLimitKey(context.Connection.RemoteIpAddress);

    // IPv6 clients usually own a whole /64, so keying by full address would give each of them a fresh budget.
    public static string GetRateLimitKey(IPAddress? address)
    {
        if (address is null)
            return IPAddress.None.ToString();

        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6)
            return address.ToString();

        var bytes = address.GetAddressBytes();
        Array.Clear(bytes, 8, 8);
        return $"{new IPAddress(bytes)}/64";
    }
}
