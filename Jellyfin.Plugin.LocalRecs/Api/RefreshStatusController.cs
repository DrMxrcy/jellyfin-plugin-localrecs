using System;
using System.IO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.LocalRecs.Api
{
    /// <summary>
    /// Returns the result of the most recent recommendation refresh run.
    /// </summary>
    [ApiController]
    [Route("LocalRecs")]
    [Authorize(Policy = "RequiresElevation")]
    public class RefreshStatusController : ControllerBase
    {
        private readonly ILogger<RefreshStatusController> _logger;
        private readonly string _dataDirectory;

        /// <summary>
        /// Initializes a new instance of the <see cref="RefreshStatusController"/> class.
        /// Constructor used by Jellyfin's DI — resolves data directory from <see cref="Plugin.Instance"/>.
        /// </summary>
        /// <param name="logger">Logger instance.</param>
        public RefreshStatusController(ILogger<RefreshStatusController> logger)
            : this(logger, Plugin.Instance?.DataFolderPath ?? string.Empty)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RefreshStatusController"/> class with an explicit data directory.
        /// Used in tests to avoid a Jellyfin plugin instance dependency.
        /// </summary>
        /// <param name="logger">Logger instance.</param>
        /// <param name="dataDirectory">Path to the plugin data folder containing last-refresh.json.</param>
        public RefreshStatusController(ILogger<RefreshStatusController> logger, string dataDirectory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dataDirectory = dataDirectory ?? throw new ArgumentNullException(nameof(dataDirectory));
        }

        /// <summary>
        /// Returns the last-refresh.json diagnostics file.
        /// </summary>
        /// <returns>The last-refresh.json content, or 404 if no refresh has run yet.</returns>
        [HttpGet("RefreshStatus")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public IActionResult GetRefreshStatus()
        {
            var path = Path.Combine(_dataDirectory, "last-refresh.json");
            if (!System.IO.File.Exists(path))
            {
                return NotFound();
            }

            var json = System.IO.File.ReadAllText(path);
            return Content(json, "application/json");
        }
    }
}
