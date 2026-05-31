using System.Collections.Generic;

namespace Jellyfin.Plugin.LocalRecs.Api
{
    /// <summary>Response model for <see cref="SetupStatusController.GetSetupStatus"/>.</summary>
    public class SetupStatusResponse
    {
        /// <summary>Gets or sets per-user setup status.</summary>
        public List<UserSetupStatus> Users { get; set; } = new();

        /// <summary>Gets or sets a value indicating whether the server is running on Windows.</summary>
        public bool IsWindows { get; set; }
    }
}
