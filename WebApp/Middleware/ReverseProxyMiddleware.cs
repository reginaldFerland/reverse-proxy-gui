using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using WebApp.Services;

namespace WebApp.Middleware;

public class ReverseProxyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ProxyService _proxyService;
    private readonly ILogger<ReverseProxyMiddleware> _logger;

    public ReverseProxyMiddleware(RequestDelegate next, ProxyService proxyService, ILogger<ReverseProxyMiddleware> logger)
    {
        _next = next;
        _proxyService = proxyService;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            var response = await _proxyService.ProxyRequestAsync(context);
            await CopyProxyResponseToContext(context, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in proxy middleware");
            context.Response.StatusCode = (int)HttpStatusCode.BadGateway;
            await context.Response.WriteAsync($"Proxy error: {ex.Message}");
        }
    }

    private async Task CopyProxyResponseToContext(HttpContext context, HttpResponseMessage response)
    {
        context.Response.StatusCode = (int)response.StatusCode;

        // Copy headers from the proxy response to our response
        foreach (var header in response.Headers)
        {
            // Skip headers that aren't appropriate for the response
            if (!header.Key.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.Headers[header.Key] = header.Value.ToArray();
            }
        }

        // Copy content headers
        if (response.Content != null)
        {
            foreach (var header in response.Content.Headers)
            {
                context.Response.Headers[header.Key] = header.Value.ToArray();
            }

            // Copy the response body
            await response.Content.CopyToAsync(context.Response.Body);
        }
    }
}

// Extension method for easy registration of the middleware
public static class ReverseProxyMiddlewareExtensions
{
    public static IApplicationBuilder UseReverseProxy(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ReverseProxyMiddleware>();
    }
}