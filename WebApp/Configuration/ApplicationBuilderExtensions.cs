using Microsoft.EntityFrameworkCore;
using ReverseProxy.Data;
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
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            // Add simple status endpoint
            app.MapGet("/status", () => "Reverse Proxy GUI is running! UI on port 8000");
        }
    }
}