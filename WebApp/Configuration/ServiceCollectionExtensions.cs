using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReverseProxy.Data;
using WebApp.Services;
using System;

namespace WebApp.Configuration
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Configures all application services
        /// </summary>
        public static WebApplicationBuilder ConfigureServices(this WebApplicationBuilder builder)
        {
            // Configure ports
            ConfigureKestrel(builder);

            // Configure MVC
            builder.Services.AddControllersWithViews();

            // Add database context
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

            // Configure HTTP client factory for proxy
            builder.Services.AddHttpClient("ProxyClient", client =>
            {
                client.DefaultRequestHeaders.Add("User-Agent", "ReverseProxyGui");
                client.Timeout = TimeSpan.FromSeconds(100);
            });

            // Add proxy service
            builder.Services.AddSingleton<ProxyService>();

            // Add a hosted service to load mappings at startup
            builder.Services.AddHostedService<MappingsLoaderService>();

            return builder;
        }

        private static void ConfigureKestrel(WebApplicationBuilder builder)
        {
            builder.WebHost.ConfigureKestrel(serverOptions =>
            {
                // UI port
                serverOptions.ListenAnyIP(8000);

                // Proxy port
                serverOptions.ListenAnyIP(8080);
            });
        }
    }
}