using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace SunnySunday.Cli.Infrastructure;

/// <summary>
/// Registers Polly resilience pipeline for the Sunny HTTP client.
/// </summary>
public static class HttpClientResilienceExtensions
{
    public static IHttpClientBuilder AddSunnyResilience(this IHttpClientBuilder builder)
    {
        builder.AddResilienceHandler("sunny-retry", pipeline =>
        {
            pipeline.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 2, // 1 initial + 2 retries = 3 total attempts
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromSeconds(1),
                ShouldHandle = args => ValueTask.FromResult(ShouldRetry(args.Outcome))
            });
        });

        return builder;
    }

    private static bool ShouldRetry(Outcome<HttpResponseMessage> outcome)
    {
        if (outcome.Exception is HttpRequestException)
            return true;

        if (outcome.Result is null)
            return false;

        var statusCode = (int)outcome.Result.StatusCode;
        return statusCode is 408 or 429 or >= 500;
    }
}
