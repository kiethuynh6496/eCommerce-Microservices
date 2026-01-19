namespace Product.Application.Interfaces
{
    /// <summary>
    /// Interface cho distributed caching với Redis
    /// </summary>
    public interface ICacheService
    {
        /// <summary>
        /// Lấy data từ cache
        /// </summary>
        Task<T?> GetAsync<T>(string key) where T : class;

        /// <summary>
        /// Set data vào cache với TTL
        /// </summary>
        Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class;

        /// <summary>
        /// Remove data khỏi cache
        /// </summary>
        Task RemoveAsync(string key);

        /// <summary>
        /// Remove nhiều keys theo pattern
        /// </summary>
        Task RemoveByPrefixAsync(string prefix);

        /// <summary>
        /// Check key có tồn tại trong cache không
        /// </summary>
        Task<bool> ExistsAsync(string key);
    }
}