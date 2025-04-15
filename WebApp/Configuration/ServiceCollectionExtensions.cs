using Microsoft.EntityFrameworkCore;
using ReverseProxy.Data;
using WebApp.Services;
using Yarp.ReverseProxy.Configuration;

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

            // Configure database
            ConfigureDatabase(builder);

            // Configure YARP reverse proxy
            ConfigureReverseProxy(builder);

            return builder;
        }

        private static void ConfigureKestrel(WebApplicationBuilder builder)
        {
            builder.WebHost.ConfigureKestrel(serverOptions =>
            {
                serverOptions.ListenAnyIP(8000); // UI port
                serverOptions.ListenAnyIP(8080); // Reverse proxy port
            });
        }

        private static void ConfigureDatabase(WebApplicationBuilder builder)
        {
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ??
                                "Data Source=reverseproxy.db"));
        }

        private static void ConfigureReverseProxy(WebApplicationBuilder builder)
        {
            // Add memory-based configuration provider for YARP
            builder.Services.AddSingleton<InMemoryConfigProvider>();

            // Add YARP reverse proxy services
            builder.Services.AddReverseProxy()
                .LoadFromMemory(
                    routes: Array.Empty<RouteConfig>(),
                    clusters: Array.Empty<ClusterConfig>()
                );

            // Add the reverse proxy service
            builder.Services.AddScoped<ReverseProxyService>();
        }
    }
}