using System;
using System.IO;
using FluentAssertions;
using Jellyfin.Plugin.LocalRecs.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.LocalRecs.Tests.Unit.Controllers
{
    public class RefreshStatusControllerTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly RefreshStatusController _sut;

        public RefreshStatusControllerTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);
            _sut = new RefreshStatusController(NullLogger<RefreshStatusController>.Instance, _tempDir);
        }

        public void Dispose() => Directory.Delete(_tempDir, recursive: true);

        [Fact]
        public void GetRefreshStatus_NoFile_Returns404()
        {
            var result = _sut.GetRefreshStatus();
            result.Should().BeOfType<NotFoundResult>();
        }

        [Fact]
        public void GetRefreshStatus_FileExists_ReturnsJsonContent()
        {
            var json = """{"completedAt":"2026-05-30T04:00:00Z","durationSeconds":42.1}""";
            File.WriteAllText(Path.Combine(_tempDir, "last-refresh.json"), json);

            var result = _sut.GetRefreshStatus();

            result.Should().BeOfType<ContentResult>();
            var content = (ContentResult)result;
            content.ContentType.Should().Be("application/json");
            content.Content.Should().Be(json);
        }
    }
}
