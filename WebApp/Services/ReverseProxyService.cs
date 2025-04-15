using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

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

                // Since the proxy is now integrated, we can directly call the update method
                var app = _serviceProvider.GetRequiredService<WebApplication>();

                // Call the UpdateYarpConfigAsync method using reflection 
                // (it's defined in Program.cs as a local function)
                var programType = app.GetType().Assembly.GetTypes()
                    .FirstOrDefault(t => t.Name == "Program");

                if (programType != null)
                {
                    var updateMethod = programType.GetMethod("UpdateYarpConfigAsync",
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Static);

                    if (updateMethod != null)
                    {
                        await (Task)updateMethod.Invoke(null, new object[] { app });
                        _logger.LogInformation("Successfully reloaded Reverse Proxy configuration");
                        return true;
                    }
                }

                // Fallback to calling the endpoint directly
                // This might be useful during development or if the reflection approach fails
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
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while triggering Reverse Proxy reload");
                return false;
            }
        }
    }
}