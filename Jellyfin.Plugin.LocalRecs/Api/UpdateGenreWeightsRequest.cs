using System.Collections.Generic;

namespace Jellyfin.Plugin.LocalRecs.Api
{
    /// <summary>Request body for updating genre weights.</summary>
    public class UpdateGenreWeightsRequest
    {
        /// <summary>Gets or sets genre name to multiplier map. Values outside [0.0, 3.0] are clamped.</summary>
        public Dictionary<string, float> Weights { get; set; } = new();
    }
}
