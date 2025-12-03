using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using DotNetSample.Core;

namespace DotNetSample.Infrastructure
{
    public class OrderWorker : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<OrderWorker> _logger;

        public OrderWorker(IServiceProvider services, ILogger<OrderWorker> logger)
        {
            _services = services;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _services.CreateScope();
                    var processor = scope.ServiceProvider.GetRequiredService<IOrderProcessor>();
                    await processor.ProcessPendingOrdersAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing orders");
                }

                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }
}
