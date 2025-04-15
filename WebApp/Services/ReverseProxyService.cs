using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace WebApp.Services
{
    public class ReverseProxyService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ReverseProxyService> _logger;
        private readonly string _reverseProxyUrl;

        public ReverseProxyService(HttpClient httpClient, IConfiguration configuration, ILogger<ReverseProxyService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _reverseProxyUrl = configuration["ReverseProxySettings:BaseUrl"] ?? "http://localhost:5000";
        }

        public async Task<bool> ReloadConfigurationAsync()
        {
            try
            {
                _logger.LogInformation("Triggering configuration reload on Reverse Proxy at {Url}", _reverseProxyUrl);
                var response = await _httpClient.GetAsync($"{_reverseProxyUrl}/reload");

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Successfully reloaded Reverse Proxy configuration");
                    return true;
                }
                else
                {
                    _logger.LogWarning("Failed to reload Reverse Proxy configuration. Status code: {StatusCode}", response.StatusCode);
                    return false;
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