using Microsoft.EntityFrameworkCore;
using ReverseProxy.Data;
using System.IO;
using Microsoft.Extensions.Logging;
using WebApp.Middleware;
using WebApp.Services;

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

            // Load initial mappings for the proxy
            await LoadProxyMappingsAsync(app);

            return app;
        }

        private static async Task LoadProxyMappingsAsync(WebApplication app)
        {
            try
            {
                var proxyService = app.Services.GetRequiredService<ProxyService>();
                await proxyService.LoadMappingsAsync();
            }
            catch (Exception ex)
            {
                var logger = app.Services.GetRequiredService<ILogger<Program>>();
                logger.LogError(ex, "An error occurred while loading proxy mappings.");
            }
        }

        private static async Task EnsureDatabaseCreatedAsync(WebApplication app)
        {
            try
            {
                using var scope = app.Services.CreateScope();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

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
                        logger.LogInformation("Database directory path: {DirectoryPath}", directoryPath);

                        if (!string.IsNullOrEmpty(directoryPath))
                        {
                            // Create the directory if it doesn't exist
                            if (!Directory.Exists(directoryPath))
                            {
                                logger.LogInformation("Creating database directory: {DirectoryPath}", directoryPath);
                                Directory.CreateDirectory(directoryPath);
                            }

                            // Ensure the directory is writable
                            try
                            {
                                // Test write permissions by creating a temp file
                                var testFile = Path.Combine(directoryPath, ".write_test");
                                File.WriteAllText(testFile, string.Empty);
                                File.Delete(testFile);
                                logger.LogInformation("Directory is writable: {DirectoryPath}", directoryPath);
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(ex, "Directory is not writable: {DirectoryPath}", directoryPath);
                                throw new InvalidOperationException($"Cannot write to database directory: {directoryPath}", ex);
                            }
                        }
                    }
                }

                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                logger.LogInformation("Applying database migrations...");
                // Apply any pending migrations
                await dbContext.Database.MigrateAsync();
                logger.LogInformation("Database migrations applied successfully");

                // Seed initial data if no mappings exist
                if (!await dbContext.Mappings.AnyAsync())
                {
                    logger.LogInformation("Seeding initial data...");
                    await SeedInitialDataAsync(dbContext);
                    logger.LogInformation("Initial data seeded successfully");
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
                ActiveDestination = 1
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
            app.MapWhen(
                context => context.Connection.LocalPort == 8000,
                appBuilder =>
                {
                    appBuilder.UseRouting();
                    appBuilder.UseEndpoints(endpoints =>
                    {
                        endpoints.MapControllerRoute(
                            name: "default",
                            pattern: "{controller=Home}/{action=Index}/{id?}");
                    });
                }
            );

            // Configure proxy for port 8080
            app.MapWhen(
                context => context.Connection.LocalPort == 8080,
                appBuilder =>
                {
                    appBuilder.UseReverseProxy();
                }
            );

            // Add simple status endpoint
            app.MapGet("/status", () => "Reverse Proxy GUI is running! UI on port 8000, Proxy on port 8080");
        }
    }
}