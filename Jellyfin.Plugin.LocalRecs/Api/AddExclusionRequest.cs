using System;

namespace Jellyfin.Plugin.LocalRecs.Api
{
    /// <summary>Request body for adding an item exclusion.</summary>
    public class AddExclusionRequest
    {
        /// <summary>Gets or sets the Jellyfin item ID to exclude.</summary>
        public Guid ItemId { get; set; }
    }
}
