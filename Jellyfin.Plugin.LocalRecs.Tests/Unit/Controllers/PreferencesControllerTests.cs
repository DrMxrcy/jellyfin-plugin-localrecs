using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using FluentAssertions;
using Jellyfin.Plugin.LocalRecs.Api;
using Jellyfin.Plugin.LocalRecs.Models;
using Jellyfin.Plugin.LocalRecs.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.LocalRecs.Tests.Unit.Controllers
{
    public class PreferencesControllerTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly UserPreferencesService _service;
        private readonly PreferencesController _sut;
        private readonly Guid _userId;

        public PreferencesControllerTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);
            _userId = Guid.NewGuid();
            _service = new UserPreferencesService(
                NullLogger<UserPreferencesService>.Instance, _tempDir);
            _sut = new PreferencesController(
                NullLogger<PreferencesController>.Instance, _service);
            SetupUserContext(_userId);
        }

        public void Dispose() => Directory.Delete(_tempDir, recursive: true);

        private void SetupUserContext(Guid userId)
        {
            var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) };
            var identity = new ClaimsIdentity(claims, "Test");
            _sut.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new System.Security.Claims.ClaimsPrincipal(identity) }
            };
        }

        [Fact]
        public void GetPreferences_ReturnsEmpty_WhenNoneExist()
        {
            var result = _sut.GetPreferences();

            result.Result.Should().BeOfType<OkObjectResult>();
            var prefs = ((OkObjectResult)result.Result!).Value as UserPreferences;
            prefs!.ExcludedItemIds.Should().BeEmpty();
            prefs.GenreWeights.Should().BeEmpty();
        }

        [Fact]
        public void AddExclusion_AddsItemId_ReturnsUpdatedPreferences()
        {
            var itemId = Guid.NewGuid();
            var result = _sut.AddExclusion(new AddExclusionRequest { ItemId = itemId });

            result.Result.Should().BeOfType<OkObjectResult>();
            var prefs = ((OkObjectResult)result.Result!).Value as UserPreferences;
            prefs!.ExcludedItemIds.Should().Contain(itemId);
        }

        [Fact]
        public void AddExclusion_EmptyGuid_ReturnsBadRequest()
        {
            var result = _sut.AddExclusion(new AddExclusionRequest { ItemId = Guid.Empty });

            result.Result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public void RemoveExclusion_RemovesExistingItem_ReturnsUpdatedPreferences()
        {
            var itemId = Guid.NewGuid();
            _sut.AddExclusion(new AddExclusionRequest { ItemId = itemId });

            var result = _sut.RemoveExclusion(itemId);

            result.Result.Should().BeOfType<OkObjectResult>();
            var prefs = ((OkObjectResult)result.Result!).Value as UserPreferences;
            prefs!.ExcludedItemIds.Should().NotContain(itemId);
        }

        [Fact]
        public void UpdateGenreWeights_ReplacesWeights_ReturnsUpdatedPreferences()
        {
            var weights = new Dictionary<string, float> { ["Action"] = 2.0f, ["Horror"] = 0.5f };
            var result = _sut.UpdateGenreWeights(new UpdateGenreWeightsRequest { Weights = weights });

            result.Result.Should().BeOfType<OkObjectResult>();
            var prefs = ((OkObjectResult)result.Result!).Value as UserPreferences;
            prefs!.GenreWeights["Action"].Should().BeApproximately(2.0f, 0.001f);
            prefs.GenreWeights["Horror"].Should().BeApproximately(0.5f, 0.001f);
        }

        [Fact]
        public void UpdateGenreWeights_ClampsOutOfRangeValues()
        {
            var weights = new Dictionary<string, float> { ["Action"] = 5.0f, ["Horror"] = -1.0f };
            var result = _sut.UpdateGenreWeights(new UpdateGenreWeightsRequest { Weights = weights });

            var prefs = ((OkObjectResult)result.Result!).Value as UserPreferences;
            prefs!.GenreWeights["Action"].Should().BeApproximately(3.0f, 0.001f);
            prefs.GenreWeights["Horror"].Should().BeApproximately(0.0f, 0.001f);
        }

        [Fact]
        public void UpdateGenreWeights_NullRequest_ReturnsBadRequest()
        {
            var result = _sut.UpdateGenreWeights(null!);

            result.Result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public void UpdateGenreWeights_PreservesExistingExclusions()
        {
            var itemId = Guid.NewGuid();
            _sut.AddExclusion(new AddExclusionRequest { ItemId = itemId });

            _sut.UpdateGenreWeights(new UpdateGenreWeightsRequest
            {
                Weights = new Dictionary<string, float> { ["Drama"] = 1.5f }
            });

            var result = _sut.GetPreferences();
            var prefs = ((OkObjectResult)result.Result!).Value as UserPreferences;
            prefs!.ExcludedItemIds.Should().Contain(itemId, "genre weight update must not clear exclusions");
        }
    }
}
