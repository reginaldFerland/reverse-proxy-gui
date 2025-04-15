using Microsoft.EntityFrameworkCore;
using ReverseProxy.Data;
using Yarp.ReverseProxy.Configuration;
using System.IO;
using Microsoft.Extensions.Logging;

namespace WebApp.Configuration
{
    public static class ApplicationBuilderExtensions
    {
        /// <summary>
        /// Configures the application pipeline and middleware
        /// </summary>
        public static async Task<WebApplication> ConfigureApplicationAsync(this WebApplication app)
        {
            // Ensure the database is created and migrations are applied
            await EnsureDatabaseCreatedAsync(app);

            // Configure the HTTP request pipeline
            ConfigureMiddleware(app);

            // Configure routing
            ConfigureRouting(app);

            // Configure the reverse proxy
            await ConfigureReverseProxyAsync(app);

            return app;
        }

        private static async Task EnsureDatabaseCreatedAsync(WebApplication app)
        {
            try
            {
                // Get the connection string to extract the database path
                var connectionString = app.Configuration.GetConnectionString("DefaultConnection");
                if (connectionString != null)
                {
                    // Extract the database file path from the connection string
                    var dataSourcePrefix = "Data Source=";
                    var dbPathStart = connectionString.IndexOf(dataSourcePrefix);
                    if (dbPathStart >= 0)
                    {
                        var dbPath = connectionString.Substring(dbPathStart + dataSourcePrefix.Length);

                        // Get the directory path
                        var directoryPath = Path.GetDirectoryName(dbPath);
                        if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
                        {
                            // Create the directory if it doesn't exist
                            Directory.CreateDirectory(directoryPath);
                        }
                    }
                }

                using var scope = app.Services.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // Apply any pending migrations
                await dbContext.Database.MigrateAsync();

                // Seed initial data if no mappings exist
                if (!await dbContext.Mappings.AnyAsync())
                {
                    await SeedInitialDataAsync(dbContext);
                }
            }
            catch (Exception ex)
            {
                var logger = app.Services.GetRequiredService<ILogger<Program>>();
                logger.LogError(ex, "An error occurred while initializing the database.");
                throw; // Rethrow to stop startup if database initialization fails
            }
        }

        private static async Task SeedInitialDataAsync(ApplicationDbContext dbContext)
        {
            // Add a default mapping if the database is empty
            dbContext.Mappings.Add(new ReverseProxy.Models.Mapping
            {
                Name = "Default Route",
                RoutePattern = "/api/{**catch-all}",
                Destination1 = "https://api1.example.com",
                Destination2 = "https://api2.example.com",
                ActiveDestination = 1,
                IsEnabled = true
            });

            await dbContext.SaveChangesAsync();
        }

        private static void ConfigureMiddleware(WebApplication app)
        {
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseStaticFiles();
            app.UseRouting();
        }

        private static void ConfigureRouting(WebApplication app)
        {
            // Map controllers for the UI on port 8000
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            // Add simple status endpoint
            app.MapGet("/status", () => "Reverse Proxy GUI is running! UI on port 8000, Proxy on port 8080");

            // Add endpoint to reload the configuration
            app.MapGet("/reload", async (HttpContext context) =>
            {
                await UpdateYarpConfigAsync(app);
                return "Configuration reloaded successfully!";
            });

            // Map reverse proxy
            app.MapReverseProxy();
        }

        private static async Task ConfigureReverseProxyAsync(WebApplication app)
        {
            // Load mappings from database and configure YARP
            await UpdateYarpConfigAsync(app);
        }

        /// <summary>
        /// Updates YARP configuration from database
        /// </summary>
        public static async Task UpdateYarpConfigAsync(WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var configProvider = app.Services.GetRequiredService<InMemoryConfigProvider>();

            // Only include enabled mappings
            var mappings = await dbContext.Mappings
                .Where(m => m.IsEnabled)
                .ToListAsync();

            var routes = new List<RouteConfig>();
            var clusters = new List<ClusterConfig>();

            foreach (var mapping in mappings)
            {
                // Determine active destination
                var destinationUrl = mapping.ActiveDestination == 1
                    ? mapping.Destination1
                    : mapping.Destination2;

                var routeId = $"route-{mapping.Id}";
                var clusterId = $"cluster-{mapping.Id}";

                // Create route
                routes.Add(new RouteConfig
                {
                    RouteId = routeId,
                    ClusterId = clusterId,
                    Match = new RouteMatch
                    {
                        Path = mapping.RoutePattern
                    }
                });

                // Create cluster
                clusters.Add(new ClusterConfig
                {
                    ClusterId = clusterId,
                    Destinations = new Dictionary<string, DestinationConfig>
                    {
                        { "destination-1", new DestinationConfig { Address = destinationUrl } }
                    }
                });
            }

            // Update the proxy with new configuration
            await configProvider.UpdateAsync(routes, clusters);
        }
    }
}