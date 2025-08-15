namespace OrderMeow.Core.Interfaces;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key);
    Task SetAsync<T>(string key, T value, TimeSpan? slidingExpiration = null);
    Task RemoveAsync(string key);
    string GetCacheKey(Guid userId);
    Task<T?> GetOrCreateAsync<T>(string key, Func<Task<T>> factory,TimeSpan memoryExpiration , TimeSpan? slidingExpiration);
}