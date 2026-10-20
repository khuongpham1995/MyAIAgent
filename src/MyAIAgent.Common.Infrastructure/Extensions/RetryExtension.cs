/**
 * MyAIAgent | Common · Infrastructure
 *
 * Applies the shared bounded exponential retry policy to opted-in HTTP clients.
 */

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using MyAIAgent.Common.Infrastructure.Http;

namespace MyAIAgent.Common.Infrastructure.Extensions;

public static class RetryExtension
{
    private const string RetryPipelineName = "http-retry-policy";

    public static void AddHttpRetryPolicy(this IHttpClientBuilder builder, bool excludeBlockedDestinations = false)
    {
        builder.AddResilienceHandler(
            RetryPipelineName,
            pipeline =>
            {
                var options = new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = 3,
                    Delay = TimeSpan.FromSeconds(1),
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true
                };
                if (excludeBlockedDestinations)
                {
                    var shouldHandle = options.ShouldHandle;
                    options.ShouldHandle = arguments => OutboundDestinationBlockedException.Find(arguments.Outcome.Exception) is not null
                        ? ValueTask.FromResult(false)
                        : shouldHandle(arguments);
                }
                pipeline.AddRetry(options);
            });
    }
}
