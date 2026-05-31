using System;
using System.Collections.Generic;
using System.Security.Claims;
using Jellyfin.Plugin.LocalRecs.Models;
using Jellyfin.Plugin.LocalRecs.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.LocalRecs.Api
{
    /// <summary>
    /// Manages per-user recommendation preferences (exclusions and genre weights).
    /// Accessible to any authenticated Jellyfin user — does not require admin elevation.
    /// </summary>
    [ApiController]
    [Route("LocalRecs/Preferences")]
    [Authorize]
    public class PreferencesController : ControllerBase
    {
        private readonly ILogger<PreferencesController> _logger;
        private readonly UserPreferencesService _preferencesService;

        /// <summary>
        /// Initializes a new instance of the <see cref="PreferencesController"/> class.
        /// </summary>
        /// <param name="logger">Logger instance.</param>
        /// <param name="preferencesService">User preferences service.</param>
        public PreferencesController(
            ILogger<PreferencesController> logger,
            UserPreferencesService preferencesService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _preferencesService = preferencesService ?? throw new ArgumentNullException(nameof(preferencesService));
        }

        /// <summary>Returns the calling user's recommendation preferences.</summary>
        /// <returns>The user's current preferences.</returns>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public ActionResult<UserPreferences> GetPreferences()
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized();
            }

            return Ok(_preferencesService.GetPreferences(userId));
        }

        /// <summary>Adds an item to the calling user's exclusion list.</summary>
        /// <param name="request">The item to exclude.</param>
        /// <returns>The updated preferences.</returns>
        [HttpPost("Exclusions")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public ActionResult<UserPreferences> AddExclusion([FromBody] AddExclusionRequest request)
        {
            if (request?.ItemId == Guid.Empty)
            {
                return BadRequest("ItemId is required");
            }

            if (!TryGetUserId(out var userId))
            {
                return Unauthorized();
            }

            var prefs = _preferencesService.GetPreferences(userId);
            prefs.ExcludedItemIds.Add(request!.ItemId);
            _preferencesService.SavePreferences(userId, prefs);

            return Ok(prefs);
        }

        /// <summary>Removes an item from the calling user's exclusion list.</summary>
        /// <param name="itemId">The item ID to remove.</param>
        /// <returns>The updated preferences.</returns>
        [HttpDelete("Exclusions/{itemId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public ActionResult<UserPreferences> RemoveExclusion(Guid itemId)
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized();
            }

            var prefs = _preferencesService.GetPreferences(userId);
            prefs.ExcludedItemIds.Remove(itemId);
            _preferencesService.SavePreferences(userId, prefs);

            return Ok(prefs);
        }

        /// <summary>
        /// Replaces the calling user's genre weights. Omitted genres revert to 1.0 (neutral).
        /// Values are clamped to [0.0, 3.0].
        /// </summary>
        /// <param name="request">The new genre weights.</param>
        /// <returns>The updated preferences.</returns>
        [HttpPut("GenreWeights")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public ActionResult<UserPreferences> UpdateGenreWeights([FromBody] UpdateGenreWeightsRequest request)
        {
            if (request?.Weights == null)
            {
                return BadRequest("Weights is required");
            }

            if (!TryGetUserId(out var userId))
            {
                return Unauthorized();
            }

            var prefs = _preferencesService.GetPreferences(userId);
            prefs.GenreWeights = new Dictionary<string, float>();
            foreach (var (genre, weight) in request.Weights)
            {
                prefs.GenreWeights[genre] = Math.Clamp(weight, 0.0f, 3.0f);
            }

            _preferencesService.SavePreferences(userId, prefs);

            return Ok(prefs);
        }

        private bool TryGetUserId(out Guid userId)
        {
            var claim = HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(claim, out userId);
        }
    }
}
