using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Configuration;

namespace WebApp.Configuration
{
    /// <summary>
    /// Custom in-memory configuration provider for YARP reverse proxy
    /// </summary>
    public class InMemoryConfigProvider : IProxyConfigProvider
    {
        private readonly IProxyConfigProvider _provider;

        public InMemoryConfigProvider(IServiceProvider services)
        {
            // Get the actual memory config provider injected by YARP
            _provider = services.GetRequiredService<IProxyConfigProvider>();
        }

        public IProxyConfig GetConfig() => _provider.GetConfig();

        /// <summary>
        /// Updates the YARP configuration with new routes and clusters
        /// </summary>
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
    }

    /// <summary>
    /// Provides a change token implementation for cancellation tokens
    /// </summary>
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
}