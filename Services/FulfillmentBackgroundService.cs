using System;
using System.Threading;
using System.Threading.Tasks;
using BizSuite.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BizSuite.Services
{
    public class FulfillmentBackgroundService : BackgroundService
    {
        private readonly ILogger<FulfillmentBackgroundService> _logger;
        private readonly IServiceProvider _serviceProvider;

        // Back-off tracking — prevents log spam on repeated DB failures
        private int _consecutiveFailures = 0;
        private const int MaxConsecutiveFailures = 5;

        public FulfillmentBackgroundService(
            ILogger<FulfillmentBackgroundService> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Fulfillment Background Service started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var repo = scope.ServiceProvider.GetRequiredService<IFulfillmentRepository>();
                    repo.ProcessAutomatedFulfillments();

                    // Reset failure counter on success
                    _consecutiveFailures = 0;
                }
                catch (OperationCanceledException)
                {
                    // Graceful shutdown — don't log as error
                    break;
                }
                catch (Exception ex)
                {
                    _consecutiveFailures++;

                    if (_consecutiveFailures <= MaxConsecutiveFailures)
                    {
                        _logger.LogError(ex,
                            "Fulfillment background job failed (attempt {Attempt}/{Max}).",
                            _consecutiveFailures, MaxConsecutiveFailures);
                    }
                    else if (_consecutiveFailures == MaxConsecutiveFailures + 1)
                    {
                        // Only log once after threshold to avoid log flooding
                        _logger.LogCritical(ex,
                            "Fulfillment background job has exceeded {Max} consecutive failures. " +
                            "Entering reduced polling mode. Check database connectivity.",
                            MaxConsecutiveFailures);
                    }
                }

                // Use exponential back-off delay when failures occur
                var delay = _consecutiveFailures > MaxConsecutiveFailures
                    ? TimeSpan.FromMinutes(30)   // Slow down on repeated DB failures
                    : TimeSpan.FromMinutes(5);   // Normal cadence

                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Graceful shutdown during delay
                    break;
                }
            }

            _logger.LogInformation("Fulfillment Background Service stopped.");
        }
    }
}
