using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Plugin.LocalRecs.Configuration;
using Jellyfin.Plugin.LocalRecs.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.LocalRecs.Services
{
    /// <summary>
    /// Service for refreshing recommendations for users.
    /// Computes embeddings once per refresh run and reuses them for all users.
    /// Embeddings are cached to disk and reused when the library hasn't changed.
    /// </summary>
    public class RecommendationRefreshService
    {
        private readonly ILogger<RecommendationRefreshService> _logger;
        private readonly LibraryAnalysisService _libraryAnalysisService;
        private readonly VocabularyBuilder _vocabularyBuilder;
        private readonly EmbeddingService _embeddingService;
        private readonly UserProfileService _userProfileService;
        private readonly RecommendationEngine _recommendationEngine;
        private readonly EmbeddingCacheService _embeddingCacheService;

        /// <summary>
        /// Initializes a new instance of the <see cref="RecommendationRefreshService"/> class.
        /// </summary>
        public RecommendationRefreshService(
            ILogger<RecommendationRefreshService> logger,
            LibraryAnalysisService libraryAnalysisService,
            VocabularyBuilder vocabularyBuilder,
            EmbeddingService embeddingService,
            UserProfileService userProfileService,
            RecommendationEngine recommendationEngine,
            EmbeddingCacheService embeddingCacheService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _libraryAnalysisService = libraryAnalysisService ?? throw new ArgumentNullException(nameof(libraryAnalysisService));
            _vocabularyBuilder = vocabularyBuilder ?? throw new ArgumentNullException(nameof(vocabularyBuilder));
            _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
            _userProfileService = userProfileService ?? throw new ArgumentNullException(nameof(userProfileService));
            _recommendationEngine = recommendationEngine ?? throw new ArgumentNullException(nameof(recommendationEngine));
            _embeddingCacheService = embeddingCacheService ?? throw new ArgumentNullException(nameof(embeddingCacheService));
        }

        /// <summary>
        /// Computes embeddings for the library, using the on-disk cache when the library hasn't changed.
        /// </summary>
        /// <returns>Embeddings, metadata, whether the cache was used, and the item count.</returns>
        public (IReadOnlyDictionary<Guid, ItemEmbedding> Embeddings, IReadOnlyDictionary<Guid, MediaItemMetadata> Metadata, bool Cached, int ItemCount) ComputeEmbeddings()
        {
            var library = _libraryAnalysisService.GetAllMediaItems();
            var metadata = library.ToDictionary(m => m.Id);

            var fingerprint = _embeddingCacheService.ComputeFingerprint(library);
            var cached = _embeddingCacheService.TryLoadCache(fingerprint);
            if (cached != null)
                return (cached, metadata, true, library.Count);

            _logger.LogInformation("Recomputed embeddings ({Count} items)", library.Count);

            var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();
            var vocabulary = _vocabularyBuilder.BuildVocabulary(
                library,
                config.MaxVocabularyActors,
                config.MaxVocabularyDirectors,
                config.MaxVocabularyTags);
            var embeddings = _embeddingService.ComputeEmbeddings(library, vocabulary);
            var embeddingsDict = embeddings.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            _embeddingCacheService.SaveCache(fingerprint, embeddingsDict, library.Count);

            return (embeddingsDict, metadata, false, library.Count);
        }

        /// <summary>
        /// Generates recommendations for a single user. Throws on failure so the multi-user
        /// loop can collect per-user errors without aborting other users.
        /// </summary>
        public (List<ScoredRecommendation> Movies, List<ScoredRecommendation> Tv) GenerateRecommendationsForUser(
            Guid userId,
            IReadOnlyDictionary<Guid, ItemEmbedding> embeddings,
            IReadOnlyDictionary<Guid, MediaItemMetadata> metadata,
            PluginConfiguration config)
        {
            _logger.LogDebug("Generating recommendations for user {UserId}", userId);

            var profile = _userProfileService.BuildUserProfile(userId, embeddings, config);

            var movieRecs = _recommendationEngine.GenerateRecommendations(
                userId, profile, embeddings, metadata, config, MediaType.Movie, config.MovieRecommendationCount);

            var tvRecs = _recommendationEngine.GenerateRecommendations(
                userId, profile, embeddings, metadata, config, MediaType.Series, config.TvRecommendationCount);

            _logger.LogDebug(
                "Generated recommendations for user {UserId}: {MovieCount} movies, {TvCount} TV",
                userId, movieRecs.Count, tvRecs.Count);

            return (movieRecs, tvRecs);
        }

        /// <summary>
        /// Generates recommendations for multiple users. Computes embeddings once and reuses them.
        /// Per-user failures are collected in the result's <see cref="MultiUserRefreshResult.Errors"/> list
        /// rather than aborting the run.
        /// </summary>
        public Task<MultiUserRefreshResult> GenerateRecommendationsForMultipleUsersAsync(
            IEnumerable<Guid> userIds,
            PluginConfiguration config)
        {
            var result = new MultiUserRefreshResult();
            var userIdList = userIds.ToList();

            if (userIdList.Count == 0)
                return Task.FromResult(result);

            _logger.LogInformation("Generating recommendations for {Count} users", userIdList.Count);

            var (embeddings, metadata, cached, itemCount) = ComputeEmbeddings();
            result.EmbeddingsCached = cached;
            result.TotalLibraryItems = itemCount;

            foreach (var userId in userIdList)
            {
                try
                {
                    var recs = GenerateRecommendationsForUser(userId, embeddings, metadata, config);
                    result.Recommendations[userId] = recs;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to generate recommendations for user {UserId}", userId);
                    result.Errors.Add((userId, ex));
                }
            }

            _logger.LogInformation(
                "Generated recommendations for {Success}/{Total} users",
                result.Recommendations.Count, userIdList.Count);

            return Task.FromResult(result);
        }
    }
}
