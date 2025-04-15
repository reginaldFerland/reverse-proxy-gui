using Microsoft.EntityFrameworkCore;
using ReverseProxy.Data;
using Yarp.ReverseProxy.Configuration;
using Microsoft.Extensions.Primitives;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ??
                    "Data Source=reverseproxy.db"));

// Add memory-based configuration provider for YARP
builder.Services.AddSingleton<InMemoryConfigProvider>();

// Add YARP reverse proxy services
builder.Services.AddReverseProxy()
    .LoadFromMemory(
        routes: Array.Empty<RouteConfig>(),
        clusters: Array.Empty<ClusterConfig>()
    );

var app = builder.Build();

// Ensure the database is created and migrations are applied
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var dbContext = services.GetRequiredService<ApplicationDbContext>();
    dbContext.Database.EnsureCreated();
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// Load mappings from database and configure YARP
await UpdateYarpConfigAsync(app);

// Add endpoint to reload the configuration
app.MapGet("/reload", async (HttpContext context) =>
{
    await UpdateYarpConfigAsync(app);
    return "Configuration reloaded successfully!";
});

app.MapReverseProxy();

app.MapGet("/", () => "Reverse Proxy is running! Check /reload to reload configuration.");

app.Run();

// Helper method to update YARP configuration from database
async Task UpdateYarpConfigAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var configProvider = app.Services.GetRequiredService<InMemoryConfigProvider>();
    
    var mappings = await dbContext.Mappings.ToListAsync();
    
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

// Custom in-memory configuration provider for YARP
public class InMemoryConfigProvider : IProxyConfigProvider
{
    private volatile InMemoryConfig _config;
    private readonly IProxyConfigProvider _provider;

    public InMemoryConfigProvider(IServiceProvider services)
    {
        _config = new InMemoryConfig(
            Array.Empty<RouteConfig>(),
            Array.Empty<ClusterConfig>());
            
        // Get the actual memory config provider injected by YARP
        _provider = services.GetRequiredService<IProxyConfigProvider>();
    }

    public IProxyConfig GetConfig() => _provider.GetConfig();

    public async Task UpdateAsync(IReadOnlyList<RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters)
    {
        // Use reflection to update YARP's memory provider
        var memoryConfigManager = _provider.GetType().GetProperty("Current")?.GetValue(_provider);
        if (memoryConfigManager != null)
        {
            var updateMethod = memoryConfigManager.GetType().GetMethod("Update");
            if (updateMethod != null)
            {
                updateMethod.Invoke(memoryConfigManager, new object[] { routes, clusters });
            }
        }
        
        await Task.CompletedTask;
    }

    private class InMemoryConfig : IProxyConfig
    {
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public InMemoryConfig(IReadOnlyList<RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters)
        {
            Routes = routes;
            Clusters = clusters;
            ChangeToken = new CancellationChangeToken(_cts.Token);
        }

        public IReadOnlyList<RouteConfig> Routes { get; }

        public IReadOnlyList<ClusterConfig> Clusters { get; }

        public IChangeToken ChangeToken { get; }

        internal void SignalChange()
        {
            _cts.Cancel();
        }
    }
}

public class CancellationChangeToken : IChangeToken
{
    private readonly CancellationToken _token;

    public CancellationChangeToken(CancellationToken token)
    {
        _token = token;
    }

    public bool ActiveChangeCallbacks => true;

    public bool HasChanged => _token.IsCancellationRequested;

    public IDisposable RegisterChangeCallback(Action<object?> callback, object? state)
    {
        return _token.Register(callback, state);
    }
}
