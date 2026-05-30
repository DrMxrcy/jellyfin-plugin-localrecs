using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Jellyfin.Plugin.LocalRecs.Models;
using Jellyfin.Plugin.LocalRecs.VirtualLibrary;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.LocalRecs.Api
{
    /// <summary>
    /// Returns per-user virtual library setup status so the admin UI can show a health checklist.
    /// </summary>
    [ApiController]
    [Route("LocalRecs")]
    [Authorize(Policy = "RequiresElevation")]
    public class SetupStatusController : ControllerBase
    {
        private readonly ILogger<SetupStatusController> _logger;
        private readonly ILibraryManager _libraryManager;
        private readonly IUserManager _userManager;
        private readonly VirtualLibraryManager _virtualLibraryManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="SetupStatusController"/> class.
        /// </summary>
        public SetupStatusController(
            ILogger<SetupStatusController> logger,
            ILibraryManager libraryManager,
            IUserManager userManager,
            VirtualLibraryManager virtualLibraryManager)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _libraryManager = libraryManager ?? throw new ArgumentNullException(nameof(libraryManager));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _virtualLibraryManager = virtualLibraryManager ?? throw new ArgumentNullException(nameof(virtualLibraryManager));
        }

        /// <summary>
        /// Returns per-user directory existence and Jellyfin library linkage status.
        /// </summary>
        [HttpGet("SetupStatus")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<SetupStatusResponse> GetSetupStatus()
        {
            var users = _userManager.GetUsers().ToList();
            var virtualFolders = _libraryManager.GetVirtualFolders();
            var linkedPaths = new HashSet<string>(
                virtualFolders.SelectMany(vf => vf.Locations),
                StringComparer.OrdinalIgnoreCase);

            var userStatuses = users.Select(user =>
            {
                var moviesPath = _virtualLibraryManager.GetUserLibraryPath(user.Id, MediaType.Movie);
                var tvPath = _virtualLibraryManager.GetUserLibraryPath(user.Id, MediaType.Series);

                var moviesExist = Directory.Exists(moviesPath);
                var tvExist = Directory.Exists(tvPath);

                return new UserSetupStatus
                {
                    UserId = user.Id.ToString(),
                    Username = user.Username ?? string.Empty,
                    DirectoriesExist = moviesExist || tvExist,
                    MoviesPath = moviesPath,
                    MoviesLibraryLinked = moviesExist && linkedPaths.Contains(moviesPath),
                    TvPath = tvPath,
                    TvLibraryLinked = tvExist && linkedPaths.Contains(tvPath)
                };
            }).ToList();

            return Ok(new SetupStatusResponse
            {
                Users = userStatuses,
                IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            });
        }
    }

    /// <summary>Response model for <see cref="SetupStatusController.GetSetupStatus"/>.</summary>
    public class SetupStatusResponse
    {
        /// <summary>Gets or sets per-user setup status.</summary>
        public List<UserSetupStatus> Users { get; set; } = new();

        /// <summary>Gets or sets a value indicating whether the server is running on Windows.</summary>
        public bool IsWindows { get; set; }
    }

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
