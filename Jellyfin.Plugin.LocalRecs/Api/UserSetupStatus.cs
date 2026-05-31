namespace Jellyfin.Plugin.LocalRecs.Api
{
    /// <summary>Setup status for a single user.</summary>
    public class UserSetupStatus
    {
        /// <summary>Gets or sets the user ID as a string.</summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>Gets or sets the username.</summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>Gets or sets a value indicating whether either directory exists.</summary>
        public bool DirectoriesExist { get; set; }

        /// <summary>Gets or sets the movies virtual library path.</summary>
        public string MoviesPath { get; set; } = string.Empty;

        /// <summary>Gets or sets a value indicating whether a Jellyfin library points at the movies path.</summary>
        public bool MoviesLibraryLinked { get; set; }

        /// <summary>Gets or sets the TV virtual library path.</summary>
        public string TvPath { get; set; } = string.Empty;

        /// <summary>Gets or sets a value indicating whether a Jellyfin library points at the TV path.</summary>
        public bool TvLibraryLinked { get; set; }
    }
}
