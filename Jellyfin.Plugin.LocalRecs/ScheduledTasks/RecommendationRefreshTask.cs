using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.LocalRecs.Configuration;
using Jellyfin.Plugin.LocalRecs.Models;
using Jellyfin.Plugin.LocalRecs.Services;
using Jellyfin.Plugin.LocalRecs.VirtualLibrary;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.LocalRecs.ScheduledTasks
{
    /// <summary>
    /// Scheduled task for refreshing recommendations for all users.
    /// Can be triggered manually or scheduled via Jellyfin's task scheduler.
    /// </summary>
    public class RecommendationRefreshTask : IScheduledTask
    {
        private readonly ILogger<RecommendationRefreshTask> _logger;
        private readonly IUserManager _userManager;
        private readonly RecommendationRefreshService _refreshService;
        private readonly VirtualLibraryManager _virtualLibraryManager;
        private readonly ILibraryManager _libraryManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="RecommendationRefreshTask"/> class.
        /// </summary>
        /// <param name="logger">Logger instance.</param>
        /// <param name="userManager">User manager for listing all users.</param>
        /// <param name="refreshService">Service that computes embeddings and recommendations.</param>
        /// <param name="virtualLibraryManager">Manages virtual library symlinks.</param>
        /// <param name="libraryManager">Jellyfin library manager for triggering scans.</param>
        public RecommendationRefreshTask(
            ILogger<RecommendationRefreshTask> logger,
            IUserManager userManager,
            RecommendationRefreshService refreshService,
            VirtualLibraryManager virtualLibraryManager,
            ILibraryManager libraryManager)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _refreshService = refreshService ?? throw new ArgumentNullException(nameof(refreshService));
            _virtualLibraryManager = virtualLibraryManager ?? throw new ArgumentNullException(nameof(virtualLibraryManager));
            _libraryManager = libraryManager ?? throw new ArgumentNullException(nameof(libraryManager));
        }

        /// <inheritdoc />
        public string Name => "Refresh Local Recommendations";

        /// <inheritdoc />
        public string Key => "LocalRecsRefresh";

        /// <inheritdoc />
        public string Description => "Updates personalized recommendations for all users based on watch history and metadata similarity";

        /// <inheritdoc />
        public string Category => "Local Recommendations";

        /// <inheritdoc />
        public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting recommendation refresh task (Build: {BuildVersion})", Plugin.BuildVersion);
            var startTime = DateTime.UtcNow;

            try
            {
                var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();

                // Step 1: Get all users (5% progress)
                progress?.Report(0);
                cancellationToken.ThrowIfCancellationRequested();
                var users = _userManager.GetUsers().ToList();
                _logger.LogInformation("Generating recommendations for {UserCount} users", users.Count);
                progress?.Report(5);

                if (users.Count == 0)
                {
                    _logger.LogWarning("No users found, skipping recommendation refresh");
                    progress?.Report(100);
                    return;
                }

                // Step 2: Generate recommendations for all users (5-80% progress)
                var userIds = users.Select(u => u.Id).ToList();
                var refreshResult = await _refreshService.GenerateRecommendationsForMultipleUsersAsync(
                    userIds, config).ConfigureAwait(false);

                progress?.Report(80);

                // Step 3: Sync virtual library symlinks for each user (80-90% progress)
                cancellationToken.ThrowIfCancellationRequested();
                var successfulUsers = 0;
                var failedSyncUsers = new List<(Guid UserId, string Username, string Error)>();

                foreach (var user in users)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        if (refreshResult.Recommendations.TryGetValue(user.Id, out var recs))
                        {
                            _logger.LogDebug("Syncing virtual library symlinks for user {UserName} ({UserId})", user.Username, user.Id);

                            _virtualLibraryManager.SyncRecommendations(user.Id, recs.Movies, MediaType.Movie);
                            _virtualLibraryManager.SyncRecommendations(user.Id, recs.Tv, MediaType.Series);

                            successfulUsers++;
                            _logger.LogDebug(
                                "Successfully updated virtual library symlinks for {UserName}: {MovieCount} movies, {TvCount} TV shows",
                                user.Username, recs.Movies.Count, recs.Tv.Count);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to sync virtual library symlinks for user {UserName} ({UserId})", user.Username, user.Id);
                        failedSyncUsers.Add((user.Id, user.Username, ex.Message));
                    }
                }

                progress?.Report(90);

                // Step 4: Wait for file system to flush, then trigger library scan (90-95% progress)
                _logger.LogDebug("Waiting for file system flush before triggering library scan");
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);

                _libraryManager.QueueLibraryScan();
                _logger.LogInformation("Library scan queued");

                progress?.Report(95);

                // Step 5: Write diagnostics and report results
                var duration = DateTime.UtcNow - startTime;

                var allErrors = refreshResult.Errors
                    .Select(e =>
                    {
                        var username = users.FirstOrDefault(u => u.Id == e.UserId)?.Username ?? e.UserId.ToString();
                        return (UserId: e.UserId, Username: username, Error: e.Error.Message);
                    })
                    .Concat(failedSyncUsers)
                    .ToList();

                WriteLastRefreshStatus(duration, users.Count, successfulUsers, allErrors,
                    refreshResult.TotalLibraryItems, refreshResult.EmbeddingsCached);

                _logger.LogInformation(
                    "Recommendation refresh completed in {Duration:F2} seconds: {Success}/{Total} users successful",
                    duration.TotalSeconds, successfulUsers, users.Count);

                if (allErrors.Count > 0)
                {
                    _logger.LogWarning("Failed for {Count} users: {Users}",
                        allErrors.Count, string.Join(", ", allErrors.Select(e => e.Username)));
                }

                progress?.Report(100);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Recommendation refresh task was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Recommendation refresh task failed");
                throw;
            }
        }

        /// <inheritdoc />
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return new[]
            {
                new TaskTriggerInfo
                {
                    Type = MediaBrowser.Model.Tasks.TaskTriggerInfoType.DailyTrigger,
                    TimeOfDayTicks = TimeSpan.FromHours(4).Ticks
                }
            };
        }

        private void WriteLastRefreshStatus(
            TimeSpan duration,
            int totalUsers,
            int successfulUsers,
            List<(Guid UserId, string Username, string Error)> errors,
            int totalLibraryItems,
            bool embeddingsCached)
        {
            try
            {
                var dataDir = Plugin.Instance?.DataFolderPath;
                if (dataDir == null)
                {
                    return;
                }

                var failedUsers = errors.Select(e => new
                {
                    userId = e.UserId.ToString(),
                    username = e.Username,
                    error = e.Error
                }).ToList();

                var status = new
                {
                    completedAt = DateTime.UtcNow,
                    durationSeconds = Math.Round(duration.TotalSeconds, 1),
                    totalUsers,
                    successfulUsers,
                    failedUsers,
                    totalLibraryItems,
                    embeddingsCached,
                    errors = failedUsers
                };

                var json = JsonSerializer.Serialize(status, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(Path.Combine(dataDir, "last-refresh.json"), json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to write last-refresh.json (non-fatal)");
            }
        }
    }
}
