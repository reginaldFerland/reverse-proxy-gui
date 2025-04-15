using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using WebApp.Configuration;

namespace WebApp.Services
{
    public class ReverseProxyService
    {
        private readonly ILogger<ReverseProxyService> _logger;
        private readonly IServiceProvider _serviceProvider;

        public ReverseProxyService(IServiceProvider serviceProvider, ILogger<ReverseProxyService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task<bool> ReloadConfigurationAsync()
        {
            try
            {
                _logger.LogInformation("Triggering configuration reload on integrated Reverse Proxy");

                // Call the UpdateYarpConfigAsync method with the service provider
                await WebApp.Configuration.ApplicationBuilderExtensions.UpdateYarpConfigAsync(_serviceProvider);

                _logger.LogInformation("Successfully reloaded Reverse Proxy configuration");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while triggering Reverse Proxy reload");

                // Try the fallback HTTP approach
                try
                {
                    // This might be useful during development or if the direct approach fails
                    using (var httpClient = new HttpClient())
                    {
                        var response = await httpClient.GetAsync("http://localhost:8080/reload");
                        if (response.IsSuccessStatusCode)
                        {
                            _logger.LogInformation("Successfully reloaded Reverse Proxy configuration via HTTP endpoint");
                            return true;
                        }
                        else
                        {
                            _logger.LogWarning("Failed to reload Reverse Proxy configuration. Status code: {StatusCode}", response.StatusCode);
                        }
                    }
                }
                catch (Exception httpEx)
                {
                    _logger.LogError(httpEx, "Error occurred while triggering Reverse Proxy reload via HTTP endpoint");
                }

                return false;
            }
        }
    }
}