namespace OrderMeow.Core.Interfaces;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key);
    Task SetAsync<T>(string key, T value, TimeSpan? slidingExpiration);
    Task RemoveAsync(string key);
    string GetCacheKey(Guid userId);
}