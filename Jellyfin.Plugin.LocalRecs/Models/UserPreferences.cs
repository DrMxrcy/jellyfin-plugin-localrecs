using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.LocalRecs.Models
{
    /// <summary>Per-user recommendation preference overrides.</summary>
    public class UserPreferences
    {
        /// <summary>Gets or sets item IDs to never recommend to this user.</summary>
        public HashSet<Guid> ExcludedItemIds { get; set; } = new();

        /// <summary>Gets or sets per-genre score multipliers. Absent genres default to 1.0.</summary>
        public Dictionary<string, float> GenreWeights { get; set; } = new();
    }
}
