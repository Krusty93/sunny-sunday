using MailKit.Net.Smtp;
using Polly;
using Polly.Retry;

namespace SunnySunday.Server.Infrastructure.Resilience;

public static class RecapDeliveryPolicy
{
    private const int MaxRetries = 2; // 3 total attempts = 1 initial + 2 retries

    public static AsyncRetryPolicy Create(ILogger logger) =>
        Policy
            .Handle<Exception>(IsTransient)
            .WaitAndRetryAsync(
                retryCount: MaxRetries,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (exception, delay, retryAttempt, _) =>
                {
                    logger.LogWarning(
                        exception,
                        "SMTP delivery attempt {Attempt} failed. Retrying in {Delay}s",
                        retryAttempt,
                        delay.TotalSeconds);
                });

    internal static bool IsTransient(Exception ex) =>
        ex is TimeoutException
            or IOException
            or SmtpCommandException { StatusCode: >= (SmtpStatusCode)400 and < (SmtpStatusCode)500 }
            or SmtpProtocolException;
}
