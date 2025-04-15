using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using ReverseProxy.Models;

namespace WebApp.Services;

public class ProxyService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ProxyService> _logger;
    private List<Mapping> _mappings = new();
    private readonly IServiceProvider _serviceProvider;

    public ProxyService(
        IHttpClientFactory httpClientFactory,
        ILogger<ProxyService> logger,
        IServiceProvider serviceProvider)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task LoadMappingsAsync()
    {
        // Create a scope to resolve the DbContext
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ReverseProxy.Data.ApplicationDbContext>();

        _mappings = await dbContext.Mappings.ToListAsync();
        _logger.LogInformation("Loaded {Count} route mappings from database", _mappings.Count);
    }

    public async Task<HttpResponseMessage> ProxyRequestAsync(HttpContext context)
    {
        var originalPath = context.Request.Path.Value ?? string.Empty;
        var mapping = FindMatchingMapping(originalPath);

        if (mapping == null)
        {
            _logger.LogWarning("No mapping found for path: {Path}", originalPath);
            return new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent($"No mapping found for path: {originalPath}", Encoding.UTF8, "text/plain")
            };
        }

        // Determine the destination based on ActiveDestination
        var destinationUrl = mapping.ActiveDestination == 1 ? mapping.Destination1 : mapping.Destination2;

        // Ensure the destination URL doesn't end with a slash
        destinationUrl = destinationUrl.TrimEnd('/');

        // Build the target URI
        var targetUri = $"{destinationUrl}{originalPath}";
        _logger.LogInformation("Proxying request from {OriginalPath} to {TargetUri}", originalPath, targetUri);

        // Forward the request
        var httpClient = _httpClientFactory.CreateClient("ProxyClient");
        var requestMessage = CreateProxyHttpRequest(context, new Uri(targetUri));

        return await httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
    }

    private Mapping? FindMatchingMapping(string path)
    {
        foreach (var mapping in _mappings)
        {
            if (IsWildcardMatch(path, mapping.RoutePattern))
            {
                return mapping;
            }
        }

        return null;
    }

    private bool IsWildcardMatch(string requestPath, string pattern)
    {
        // Convert the pattern to regex
        string regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";

        // Create regex with case-insensitive matching
        var regex = new Regex(regexPattern, RegexOptions.IgnoreCase);

        // Test if the request path matches the pattern
        return regex.IsMatch(requestPath);
    }

    private HttpRequestMessage CreateProxyHttpRequest(HttpContext context, Uri targetUri)
    {
        var requestMessage = new HttpRequestMessage();
        var requestMethod = context.Request.Method;

        // Set the request method
        if (!HttpMethods.IsGet(requestMethod) &&
            !HttpMethods.IsHead(requestMethod) &&
            !HttpMethods.IsDelete(requestMethod) &&
            !HttpMethods.IsTrace(requestMethod))
        {
            var streamContent = new StreamContent(context.Request.Body);
            requestMessage.Content = streamContent;
        }

        // Set the request method
        requestMessage.Method = new HttpMethod(requestMethod);

        // Set the request URI
        requestMessage.RequestUri = targetUri;

        // Copy headers from the incoming request to the outgoing request
        foreach (var header in context.Request.Headers)
        {
            // Skip headers that are controlled by the client or HttpClient
            if (!header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase) &&
                !header.Key.Equals("Connection", StringComparison.OrdinalIgnoreCase) &&
                !header.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase) &&
                !header.Key.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
            {
                requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }
        }

        // Set the Content-Type header if it exists and we have content
        if (requestMessage.Content != null &&
            context.Request.Headers.TryGetValue("Content-Type", out var contentTypeValues))
        {
            requestMessage.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentTypeValues.FirstOrDefault() ?? "application/octet-stream");
        }

        // Set the Host header to the target host
        requestMessage.Headers.Host = targetUri.Authority;

        return requestMessage;
    }
}