using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.LocalRecs.Models
{
    /// <summary>
    /// Result of generating recommendations for multiple users in a single refresh run.
    /// </summary>
    public class MultiUserRefreshResult
    {
        /// <summary>
        /// Gets or sets the recommendations per user (movies and TV).
        /// </summary>
        public Dictionary<Guid, (List<ScoredRecommendation> Movies, List<ScoredRecommendation> Tv)> Recommendations { get; set; } = new();

        /// <summary>
        /// Gets or sets per-user errors collected during the run. A non-empty list indicates partial success.
        /// </summary>
        public List<(Guid UserId, Exception Error)> Errors { get; set; } = new();

        /// <summary>
        /// Gets or sets a value indicating whether embeddings were served from the on-disk cache.
        /// </summary>
        public bool EmbeddingsCached { get; set; }

        /// <summary>
        /// Gets or sets the total number of library items that were processed.
        /// </summary>
        public int TotalLibraryItems { get; set; }
    }
}
