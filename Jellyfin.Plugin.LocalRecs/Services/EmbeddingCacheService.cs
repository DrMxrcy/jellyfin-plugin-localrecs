using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Jellyfin.Plugin.LocalRecs.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.LocalRecs.Services
{
    /// <summary>
    /// Manages on-disk caching of TF-IDF embeddings to avoid recomputation when the library hasn't changed.
    /// </summary>
    public class EmbeddingCacheService
    {
        private const int CacheVersion = 1;
        private readonly ILogger<EmbeddingCacheService> _logger;
        private readonly string _cacheDirectory;

        /// <summary>
        /// Initializes a new instance of the <see cref="EmbeddingCacheService"/> class.
        /// </summary>
        /// <param name="logger">Logger instance.</param>
        /// <param name="cacheDirectory">Directory where the cache file is stored.</param>
        public EmbeddingCacheService(ILogger<EmbeddingCacheService> logger, string cacheDirectory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cacheDirectory = cacheDirectory ?? throw new ArgumentNullException(nameof(cacheDirectory));
        }

        private string CacheFilePath => Path.Combine(_cacheDirectory, "embeddings-cache.json");

        /// <summary>
        /// Computes a SHA-256 fingerprint of the library by hashing sorted (ItemId, DateModified) pairs.
        /// A changed fingerprint means embeddings must be recomputed.
        /// </summary>
        public string ComputeFingerprint(IReadOnlyList<MediaItemMetadata> items)
        {
            var input = string.Join(",", items
                .OrderBy(i => i.Id)
                .Select(i => $"{i.Id}:{i.DateModified.Ticks}"));

            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        /// <summary>
        /// Attempts to load cached embeddings. Returns null if the cache is absent, stale, or corrupt.
        /// </summary>
        public IReadOnlyDictionary<Guid, ItemEmbedding>? TryLoadCache(string fingerprint)
        {
            if (!File.Exists(CacheFilePath))
                return null;

            try
            {
                var json = File.ReadAllText(CacheFilePath);
                var cache = JsonSerializer.Deserialize<EmbeddingCacheFile>(json);

                if (cache == null || cache.Version != CacheVersion || cache.Fingerprint != fingerprint)
                    return null;

                var result = new Dictionary<Guid, ItemEmbedding>(cache.Embeddings.Count);
                foreach (var (key, vector) in cache.Embeddings)
                {
                    if (Guid.TryParse(key, out var id))
                        result[id] = new ItemEmbedding(id, vector);
                }

                _logger.LogInformation("Using cached embeddings ({N} items)", result.Count);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load embedding cache; will recompute");
                return null;
            }
        }

        /// <summary>
        /// Saves embeddings to the cache file. Errors are logged as warnings and swallowed (non-fatal).
        /// </summary>
        public void SaveCache(string fingerprint, IReadOnlyDictionary<Guid, ItemEmbedding> embeddings, int itemCount)
        {
            try
            {
                Directory.CreateDirectory(_cacheDirectory);

                var cache = new EmbeddingCacheFile
                {
                    Version = CacheVersion,
                    Fingerprint = fingerprint,
                    ItemCount = itemCount,
                    ComputedAt = DateTime.UtcNow,
                    Embeddings = embeddings.ToDictionary(
                        kvp => kvp.Key.ToString(),
                        kvp => kvp.Value.Vector)
                };

                var json = JsonSerializer.Serialize(cache, new JsonSerializerOptions { WriteIndented = false });
                File.WriteAllText(CacheFilePath, json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save embedding cache (non-fatal)");
            }
        }

        private sealed class EmbeddingCacheFile
        {
            [JsonPropertyName("version")]
            public int Version { get; set; }

            [JsonPropertyName("fingerprint")]
            public string Fingerprint { get; set; } = string.Empty;

            [JsonPropertyName("itemCount")]
            public int ItemCount { get; set; }

            [JsonPropertyName("computedAt")]
            public DateTime ComputedAt { get; set; }

            [JsonPropertyName("embeddings")]
            public Dictionary<string, float[]> Embeddings { get; set; } = new();
        }
    }
}
