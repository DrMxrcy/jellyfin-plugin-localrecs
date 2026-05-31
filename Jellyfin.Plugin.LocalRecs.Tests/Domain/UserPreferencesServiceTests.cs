using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Jellyfin.Plugin.LocalRecs.Models;
using Jellyfin.Plugin.LocalRecs.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.LocalRecs.Tests.Domain
{
    public class UserPreferencesServiceTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly UserPreferencesService _sut;

        public UserPreferencesServiceTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);
            _sut = new UserPreferencesService(NullLogger<UserPreferencesService>.Instance, _tempDir);
        }

        public void Dispose() => Directory.Delete(_tempDir, recursive: true);

        [Fact]
        public void GetPreferences_NoFile_ReturnsEmpty()
        {
            var result = _sut.GetPreferences(Guid.NewGuid());
            result.ExcludedItemIds.Should().BeEmpty();
            result.GenreWeights.Should().BeEmpty();
        }

        [Fact]
        public void SaveAndLoad_RoundTrip_RestoresPreferences()
        {
            var userId = Guid.NewGuid();
            var itemId = Guid.NewGuid();
            var prefs = new UserPreferences();
            prefs.ExcludedItemIds.Add(itemId);
            prefs.GenreWeights["Action"] = 2.0f;

            _sut.SavePreferences(userId, prefs);

            // New service instance bypasses cache
            var service2 = new UserPreferencesService(
                NullLogger<UserPreferencesService>.Instance, _tempDir);
            var loaded = service2.GetPreferences(userId);

            loaded.ExcludedItemIds.Should().Contain(itemId);
            loaded.GenreWeights["Action"].Should().BeApproximately(2.0f, 0.001f);
        }

        [Fact]
        public void GetPreferences_CorruptFile_ReturnsEmpty()
        {
            var userId = Guid.NewGuid();
            var dir = Path.Combine(_tempDir, "users", userId.ToString());
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "preferences.json"), "not valid json {{");

            var result = _sut.GetPreferences(userId);

            result.ExcludedItemIds.Should().BeEmpty();
            result.GenreWeights.Should().BeEmpty();
        }

        [Fact]
        public void SavePreferences_UpdatesCache_SubsequentGetReturnsSaved()
        {
            var userId = Guid.NewGuid();
            var itemId = Guid.NewGuid();
            var prefs = new UserPreferences();
            prefs.ExcludedItemIds.Add(itemId);
            _sut.SavePreferences(userId, prefs);

            var result = _sut.GetPreferences(userId);
            result.ExcludedItemIds.Should().Contain(itemId);
        }

        [Fact]
        public async Task ConcurrentWrites_DoNotCorruptFile()
        {
            var userId = Guid.NewGuid();
            var tasks = new List<Task>();
            for (int i = 0; i < 20; i++)
            {
                var idx = i;
                tasks.Add(Task.Run(() =>
                {
                    var p = new UserPreferences();
                    p.GenreWeights[$"Genre{idx}"] = idx;
                    _sut.SavePreferences(userId, p);
                }));
            }
            await Task.WhenAll(tasks);

            var path = Path.Combine(_tempDir, "users", userId.ToString(), "preferences.json");
            File.Exists(path).Should().BeTrue();
            var json = File.ReadAllText(path);
            var deserialized = System.Text.Json.JsonSerializer.Deserialize<UserPreferences>(json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            deserialized.Should().NotBeNull("concurrent writes must not corrupt the file");
            deserialized!.GenreWeights.Should().HaveCount(1, "each write replaces all genre weights with exactly 1 entry");
            deserialized.GenreWeights.Keys.Should().OnlyContain(k => k.StartsWith("Genre"),
                "the surviving key must be one of the 20 Genre<n> keys written by concurrent tasks");
            var survivingValue = deserialized.GenreWeights.Values.Single();
            survivingValue.Should().BeInRange(0, 19, "the surviving value must be one of the 20 indexes written");
        }
    }
}
