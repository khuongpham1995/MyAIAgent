using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace MyAIAgent.McpServer.Infrastructure.Extensions;

public static class RetryExtension
{
    private const string RetryPipelineName = "http-retry-policy";

    public static void AddHttpRetryPolicy(this IHttpClientBuilder builder)
    {
        builder.AddResilienceHandler(
            RetryPipelineName,
            static pipeline =>
            {
                pipeline.AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = 3,
                    Delay = TimeSpan.FromSeconds(1),
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true
                });
            });
    }
}
