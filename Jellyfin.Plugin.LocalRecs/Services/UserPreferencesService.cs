using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using Jellyfin.Plugin.LocalRecs.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.LocalRecs.Services
{
    /// <summary>
    /// Manages per-user recommendation preferences with an in-memory cache backed by per-user JSON files.
    /// </summary>
    public class UserPreferencesService
    {
        private static readonly JsonSerializerOptions ReadOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private static readonly JsonSerializerOptions WriteOptions = new()
        {
            WriteIndented = true
        };

        private readonly ILogger<UserPreferencesService> _logger;
        private readonly string _dataDirectory;
        private readonly ConcurrentDictionary<Guid, UserPreferences> _cache = new();
        private readonly ConcurrentDictionary<Guid, object> _writeLocks = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="UserPreferencesService"/> class.
        /// </summary>
        /// <param name="logger">Logger instance.</param>
        /// <param name="dataDirectory">Root data directory for the plugin.</param>
        public UserPreferencesService(ILogger<UserPreferencesService> logger, string dataDirectory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dataDirectory = dataDirectory ?? throw new ArgumentNullException(nameof(dataDirectory));
        }

        /// <summary>
        /// Returns preferences for the given user. Returns an empty instance if no file exists.
        /// Reads are lock-free (served from cache after first load).
        /// </summary>
        /// <param name="userId">User to retrieve preferences for.</param>
        /// <returns>The user's saved preferences, or an empty default instance.</returns>
        public UserPreferences GetPreferences(Guid userId)
        {
            if (_cache.TryGetValue(userId, out var cached))
            {
                return cached;
            }

            var prefs = LoadFromDisk(userId);
            _cache.TryAdd(userId, prefs);
            return _cache.TryGetValue(userId, out var final) ? final : prefs;
        }

        /// <summary>
        /// Saves preferences for the given user to both the cache and disk.
        /// Write is serialized per user via a per-user lock.
        /// </summary>
        /// <param name="userId">User to save preferences for.</param>
        /// <param name="prefs">Preferences to persist.</param>
        public void SavePreferences(Guid userId, UserPreferences prefs)
        {
            var lockObj = _writeLocks.GetOrAdd(userId, _ => new object());
            lock (lockObj)
            {
                _cache[userId] = prefs;
                SaveToDisk(userId, prefs);
            }
        }

        private UserPreferences LoadFromDisk(Guid userId)
        {
            var path = GetPreferencesPath(userId);
            if (!File.Exists(path))
            {
                return new UserPreferences();
            }

            try
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<UserPreferences>(json, ReadOptions)
                    ?? new UserPreferences();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load preferences for user {UserId}, using defaults", userId);
                return new UserPreferences();
            }
        }

        private void SaveToDisk(Guid userId, UserPreferences prefs)
        {
            try
            {
                var path = GetPreferencesPath(userId);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var tmp = path + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(prefs, WriteOptions));
                File.Move(tmp, path, overwrite: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save preferences for user {UserId}", userId);
            }
        }

        private string GetPreferencesPath(Guid userId)
            => Path.Combine(_dataDirectory, "users", userId.ToString(), "preferences.json");
    }
}
