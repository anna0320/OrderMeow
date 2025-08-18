using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using OrderMeow.Core.Interfaces;

namespace OrderMeow.Infrastructure.Services;

public class CacheService:ICacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly IDistributedCache  _cache;

    public CacheService(IDistributedCache cache, IMemoryCache memoryCache)
    {
        _memoryCache = memoryCache;
        _cache = cache;
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        if (_memoryCache.TryGetValue(key, out T? memoryValue))
        {
            return memoryValue;
        }
        var json = await _cache.GetStringAsync(key);
        if (json == null)
        {
            return default;
        }
        var value = JsonSerializer.Deserialize<T>(json);
        _memoryCache.Set(key, value, new MemoryCacheEntryOptions().SetSize(1).SetAbsoluteExpiration(TimeSpan.FromMinutes(1)));
        return value;
    }

    public async Task SetAsync<T>(string key, T value,  TimeSpan? slidingExpiration = null)
    {
        var options = new DistributedCacheEntryOptions();
        if (slidingExpiration.HasValue)
        {
            options.SetAbsoluteExpiration(slidingExpiration.Value);
        }
        var json = JsonSerializer.Serialize(value);
        _memoryCache.Set(key, value, new MemoryCacheEntryOptions().SetSize(1).SetSlidingExpiration(slidingExpiration ?? TimeSpan.FromMinutes(5)));
        await _cache.SetStringAsync(key, json, options);
    }

    public async Task RemoveAsync(string key)
    {
        _memoryCache.Remove(key);
        await _cache.RemoveAsync(key);
    }

    public string GetCacheKey(Guid userId) => $"OrderMeow_{userId}";
    public async Task<T?> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan memoryExpiration, TimeSpan? slidingExpiration)
    {
        var cacheValue = await GetAsync<T>(key);
        if (cacheValue is not null)
        {
            return cacheValue;
        }
        
        var result = await factory()!;
        await SetAsync(key, result,slidingExpiration ?? memoryExpiration);
        _memoryCache.Set(key, result, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = memoryExpiration,
            PostEvictionCallbacks =
            {
                new PostEvictionCallbackRegistration
                {
                    EvictionCallback = (k, value, reason, state) =>
                    {
                        if (reason == EvictionReason.Expired)
                        {
                            _ = Task.Run(() => GetOrCreateAsync(key, factory, memoryExpiration, slidingExpiration));
                        }
                    }
                }
            }
        }.SetSize(1));
        return result;
    }
}