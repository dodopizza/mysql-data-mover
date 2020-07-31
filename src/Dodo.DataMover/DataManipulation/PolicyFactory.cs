using System;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Contrib.WaitAndRetry;

namespace Dodo.DataMover.DataManipulation
{
    /// <summary>
    /// PolicyFactory provides centralized policies for the app.
    /// </summary>
    public class PolicyFactory
    {
        public AsyncPolicy SrcPolicy { get; }

        public AsyncPolicy DstPolicy { get; }

        public PolicyFactory(DataMoverSettings settings, ILogger<PolicyFactory> logger)
        {
            SrcPolicy = Create("src", settings, logger);
            DstPolicy = Create("dst", settings, logger);
        }

        private static AsyncPolicy Create(string description, DataMoverSettings settings, ILogger logger)
        {
            var breaker = Policy
                .Handle<Exception>()
                .CircuitBreakerAsync(
                    exceptionsAllowedBeforeBreaking: 5,
                    durationOfBreak: TimeSpan.FromSeconds(3));

            var retry = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(Backoff.LinearBackoff(
                        TimeSpan.FromSeconds(settings.RetryInitialDelaySeconds), settings.RetryCount),
                    (exception, span, retryCount, context) =>
                    {
                        logger.LogError(
                            exception,
                            "{@EventType} {@Realm} {@TimeSpan} {@retryCount}",
                            "Retry",
                            description,
                            span,
                            retryCount);
                    });

            return Policy.WrapAsync(retry, breaker);
        }
    }
}
