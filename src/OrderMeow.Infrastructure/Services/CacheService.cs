using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using OrderMeow.Core.Interfaces;

namespace OrderMeow.Infrastructure.Services;

public class CacheService:ICacheService
{
    private readonly IDistributedCache  _cache;

    public CacheService(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        var value = await _cache.GetStringAsync(key);
        return value is null ? default : JsonSerializer.Deserialize<T>(value);
    }

    public async Task SetAsync<T>(string key, T value,  TimeSpan? slidingExpiration = null)
    {
        var options = new DistributedCacheEntryOptions();
        if (slidingExpiration.HasValue)
        {
            options.SetAbsoluteExpiration(slidingExpiration.Value);
        }
        var json = JsonSerializer.Serialize(value);
        await _cache.SetStringAsync(key, json, options);
    }

    public async Task RemoveAsync(string key)
    {
        await _cache.RemoveAsync(key);
    }

    public string GetCacheKey(Guid userId) => $"OrderMeow_{userId}";
}