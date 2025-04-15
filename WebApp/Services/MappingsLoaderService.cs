using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace WebApp.Services
{
    public class MappingsLoaderService : BackgroundService
    {
        private readonly ProxyService _proxyService;
        private readonly ILogger<MappingsLoaderService> _logger;

        public MappingsLoaderService(ProxyService proxyService, ILogger<MappingsLoaderService> logger)
        {
            _proxyService = proxyService;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("MappingsLoaderService is starting.");

            try
            {
                // Load mappings at startup
                await _proxyService.LoadMappingsAsync();

                // Periodically refresh mappings
                while (!stoppingToken.IsCancellationRequested)
                {
                    // Wait for 5 minutes before refreshing mappings
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

                    try
                    {
                        _logger.LogInformation("Refreshing mappings from database");
                        await _proxyService.LoadMappingsAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error refreshing mappings");
                    }
                }
            }
            catch (Exception ex) when (stoppingToken.IsCancellationRequested)
            {
                // Ignore exceptions on shutdown
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in MappingsLoaderService");
            }
        }
    }
}