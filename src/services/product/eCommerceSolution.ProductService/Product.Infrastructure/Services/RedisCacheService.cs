using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Product.Application.Interfaces;

namespace Product.Infrastructure.Services
{
    public class RedisCacheService : ICacheService
    {
        private readonly IDistributedCache _cache;
        private readonly ILogger<RedisCacheService> _logger;
        private readonly TimeSpan _defaultExpiration = TimeSpan.FromMinutes(5);

        public RedisCacheService(
            IDistributedCache cache,
            ILogger<RedisCacheService> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public async Task<T?> GetAsync<T>(string key) where T : class
        {
            try
            {
                _logger.LogDebug("Getting cache for key: {Key}", key);

                var cachedData = await _cache.GetStringAsync(key);

                if (string.IsNullOrEmpty(cachedData))
                {
                    _logger.LogDebug("Cache miss for key: {Key}", key);
                    return null;
                }

                _logger.LogDebug("Cache hit for key: {Key}", key);
                return JsonConvert.DeserializeObject<T>(cachedData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cache for key: {Key}", key);
                return null; // Return null on error, don't break the application
            }
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
        {
            try
            {
                var expirationTime = expiration ?? _defaultExpiration;

                _logger.LogDebug("Setting cache for key: {Key} with expiration: {Expiration}s",
                    key, expirationTime.TotalSeconds);

                var serializedData = JsonConvert.SerializeObject(value);

                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = expirationTime
                };

                await _cache.SetStringAsync(key, serializedData, options);

                _logger.LogDebug("Cache set successfully for key: {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting cache for key: {Key}", key);
                // Don't throw - caching should not break the application
            }
        }

        public async Task RemoveAsync(string key)
        {
            try
            {
                _logger.LogDebug("Removing cache for key: {Key}", key);
                await _cache.RemoveAsync(key);
                _logger.LogDebug("Cache removed successfully for key: {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cache for key: {Key}", key);
            }
        }

        public async Task RemoveByPrefixAsync(string prefix)
        {
            try
            {
                _logger.LogInformation("Removing all cache with prefix: {Prefix}", prefix);

                // Note: Redis pattern-based removal requires StackExchange.Redis directly
                // For simplicity, we'll just log this - implement if needed
                _logger.LogWarning("RemoveByPrefixAsync not fully implemented - consider using StackExchange.Redis directly");

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cache by prefix: {Prefix}", prefix);
            }
        }

        public async Task<bool> ExistsAsync(string key)
        {
            try
            {
                var cachedData = await _cache.GetStringAsync(key);
                return !string.IsNullOrEmpty(cachedData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking cache existence for key: {Key}", key);
                return false;
            }
        }
    }
}