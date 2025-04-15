using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ReverseProxy.Data;
using System.Text.RegularExpressions;
using Yarp.ReverseProxy.Forwarder;
using Microsoft.EntityFrameworkCore;

namespace WebApp.Configuration
{
    /// <summary>
    /// Middleware that dynamically routes requests based on path patterns in the database
    /// </summary>
    public class DynamicRoutingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<DynamicRoutingMiddleware> _logger;
        private readonly IHttpForwarder _forwarder;

        public DynamicRoutingMiddleware(
            RequestDelegate next,
            ILogger<DynamicRoutingMiddleware> logger,
            IHttpForwarder forwarder)
        {
            _next = next;
            _logger = logger;
            _forwarder = forwarder;
        }

        public async Task InvokeAsync(HttpContext context, ApplicationDbContext dbContext)
        {
            // Only process requests to the proxy port (8080)
            if (!IsProxyPort(context.Request))
            {
                await _next(context);
                return;
            }

            _logger.LogDebug("Processing request for {path}", context.Request.Path);

            // Get the current path
            var path = context.Request.Path.Value ?? "/";

            // Get all mappings from DB
            var mappings = await dbContext.Mappings.ToListAsync();

            // Try to find a matching route pattern
            var matchedMapping = FindMatchingMapping(mappings, path);

            if (matchedMapping != null)
            {
                // Determine the destination URL
                var destinationUrl = matchedMapping.ActiveDestination == 1
                    ? matchedMapping.Destination1
                    : matchedMapping.Destination2;

                _logger.LogInformation("Matched route pattern '{pattern}' for path '{path}', forwarding to {destination}",
                    matchedMapping.RoutePattern, path, destinationUrl);

                // Forward the request to the destination
                await ForwardRequest(context, destinationUrl);
                return;
            }

            _logger.LogWarning("No matching route pattern found for path '{path}'", path);

            // No matching pattern found, return 404
            context.Response.StatusCode = 404;
            await context.Response.WriteAsync($"No route matched for path: {path}");
        }

        private bool IsProxyPort(HttpRequest request)
        {
            var host = request.Host.Value?.ToLower();
            return host?.Contains(":8080") == true;
        }

        private static ReverseProxy.Models.Mapping? FindMatchingMapping(
            IEnumerable<ReverseProxy.Models.Mapping> mappings,
            string path)
        {
            foreach (var mapping in mappings)
            {
                var pattern = mapping.RoutePattern;

                // Simple case: exact match
                if (pattern.Equals(path, StringComparison.OrdinalIgnoreCase))
                {
                    return mapping;
                }

                // Convert YARP route pattern to regex pattern
                var regexPattern = ConvertYarpPatternToRegex(pattern);

                // Check if the path matches the regex pattern
                if (Regex.IsMatch(path, regexPattern, RegexOptions.IgnoreCase))
                {
                    return mapping;
                }
            }

            return null;
        }

        private static string ConvertYarpPatternToRegex(string yarpPattern)
        {
            // Convert YARP route pattern to regex
            // Examples:
            // /api/{**catch-all} -> ^/api/(.*)$
            // /products/{id} -> ^/products/([^/]+)$
            // /api/* -> ^/api/(.*)$  (new simplified wildcard syntax)

            // First, handle the asterisk wildcard pattern before escaping
            if (yarpPattern.Contains("*"))
            {
                // Replace trailing wildcard with regex pattern
                // For example: /api/* becomes /api/(.*)
                yarpPattern = Regex.Replace(yarpPattern, @"/\*$", "/{**catch-all}");
                
                // Replace wildcards in middle of pattern
                // For example: /api/*/products becomes /api/{**segment}/products
                yarpPattern = Regex.Replace(yarpPattern, @"/\*/", "/{**segment}/");
            }

            var pattern = Regex.Escape(yarpPattern);

            // Replace {**catch-all} with (.*)
            pattern = Regex.Replace(pattern, @"\\\{\\\*\\\*([^}]+)\\\}", "(.*)");

            // Replace {parameter} with ([^/]+)
            pattern = Regex.Replace(pattern, @"\\\{([^}]+)\\\}", "([^/]+)");

            return $"^{pattern}$";
        }

        private async Task ForwardRequest(HttpContext context, string destinationUrl)
        {
            // Parse the destination URL
            if (!Uri.TryCreate(destinationUrl, UriKind.Absolute, out var uri))
            {
                _logger.LogError("Invalid destination URL: {url}", destinationUrl);
                context.Response.StatusCode = 502;
                await context.Response.WriteAsync($"Invalid destination URL: {destinationUrl}");
                return;
            }

            var destinationPrefix = $"{uri.Scheme}://{uri.Authority}";
            var requestOptions = new ForwarderRequestConfig { ActivityTimeout = TimeSpan.FromSeconds(100) };

            // Create a HttpMessageInvoker with an HttpClientHandler that accepts all certificates
            var httpClient = new HttpMessageInvoker(new HttpClientHandler
            {
                AllowAutoRedirect = false,
                UseProxy = false,
                UseCookies = false,
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            });

            try
            {
                // Forward the request
                var error = await _forwarder.SendAsync(context, destinationPrefix, httpClient, requestOptions,
                    static (context, proxyResponse) =>
                    {
                        // Copy all response headers to the client
                        foreach (var header in proxyResponse.Headers)
                        {
                            context.Response.Headers[header.Key] = header.Value.ToArray();
                        }

                        return ValueTask.CompletedTask;
                    });

                if (error != ForwarderError.None)
                {
                    _logger.LogError("Forwarding failed with error: {error}", error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during forwarding request to {url}", destinationUrl);
                context.Response.StatusCode = 502;
                await context.Response.WriteAsync($"Error forwarding request: {ex.Message}");
            }
        }
    }
}