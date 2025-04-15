using Microsoft.EntityFrameworkCore;
using ReverseProxy.Data;

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

            return builder;
        }

        private static void ConfigureKestrel(WebApplicationBuilder builder)
        {
            builder.WebHost.ConfigureKestrel(serverOptions =>
            {
                serverOptions.ListenAnyIP(8000); // UI port only
            });
        }

        private static void ConfigureDatabase(WebApplicationBuilder builder)
        {
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ??
                                "Data Source=reverseproxy.db"));
        }
    }
}