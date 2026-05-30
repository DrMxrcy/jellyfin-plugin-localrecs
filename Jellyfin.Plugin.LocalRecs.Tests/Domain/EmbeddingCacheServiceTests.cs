using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Jellyfin.Plugin.LocalRecs.Models;
using Jellyfin.Plugin.LocalRecs.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.LocalRecs.Tests.Domain
{
    public class EmbeddingCacheServiceTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly EmbeddingCacheService _sut;

        public EmbeddingCacheServiceTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);
            _sut = new EmbeddingCacheService(NullLogger<EmbeddingCacheService>.Instance, _tempDir);
        }

        public void Dispose() => Directory.Delete(_tempDir, recursive: true);

        private static List<MediaItemMetadata> MakeItems(int count)
        {
            var items = new List<MediaItemMetadata>();
            for (int i = 0; i < count; i++)
            {
                items.Add(new MediaItemMetadata(Guid.NewGuid(), $"Item {i}", MediaType.Movie)
                {
                    DateModified = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(i)
                });
            }

            return items;
        }

        private static Dictionary<Guid, ItemEmbedding> MakeEmbeddings(List<MediaItemMetadata> items)
        {
            var rng = new Random(42);
            return items.ToDictionary(i => i.Id, i =>
            {
                var v = new float[10];
                for (int j = 0; j < v.Length; j++)
                    v[j] = (float)rng.NextDouble();
                return new ItemEmbedding(i.Id, v);
            });
        }

        [Fact]
        public void ComputeFingerprint_SameItems_ReturnsSameHash()
        {
            var items = MakeItems(5);
            var fp1 = _sut.ComputeFingerprint(items);
            var fp2 = _sut.ComputeFingerprint(items);
            fp1.Should().Be(fp2);
        }

        [Fact]
        public void ComputeFingerprint_DifferentItems_ReturnsDifferentHash()
        {
            var items1 = MakeItems(3);
            var items2 = MakeItems(3); // different Guids on each call
            var fp1 = _sut.ComputeFingerprint(items1);
            var fp2 = _sut.ComputeFingerprint(items2);
            fp1.Should().NotBe(fp2);
        }

        [Fact]
        public void TryLoadCache_NoCacheFile_ReturnsNull()
        {
            var result = _sut.TryLoadCache("any-fingerprint");
            result.Should().BeNull();
        }

        [Fact]
        public void SaveAndLoad_RoundTrip_RestoresEmbeddings()
        {
            var items = MakeItems(5);
            var embeddings = MakeEmbeddings(items);
            var fp = _sut.ComputeFingerprint(items);

            _sut.SaveCache(fp, embeddings, items.Count);
            var loaded = _sut.TryLoadCache(fp);

            loaded.Should().NotBeNull();
            loaded!.Count.Should().Be(embeddings.Count);
            foreach (var (id, emb) in embeddings)
            {
                loaded.Should().ContainKey(id);
                loaded[id].Vector.Should().BeEquivalentTo(emb.Vector, opts => opts.WithStrictOrdering());
            }
        }

        [Fact]
        public void TryLoadCache_FingerprintMismatch_ReturnsNull()
        {
            var items = MakeItems(3);
            var embeddings = MakeEmbeddings(items);
            _sut.SaveCache("fingerprint-A", embeddings, items.Count);

            var result = _sut.TryLoadCache("fingerprint-B");
            result.Should().BeNull();
        }

        [Fact]
        public void TryLoadCache_CorruptFile_ReturnsNull()
        {
            var cachePath = Path.Combine(_tempDir, "embeddings-cache.json");
            File.WriteAllText(cachePath, "not valid json {{{");

            var result = _sut.TryLoadCache("any-fingerprint");
            result.Should().BeNull();
        }

        [Fact]
        public void TryLoadCache_VersionMismatch_ReturnsNull()
        {
            var cachePath = Path.Combine(_tempDir, "embeddings-cache.json");
            File.WriteAllText(cachePath, """{"version":99,"fingerprint":"fp","itemCount":0,"computedAt":"2025-01-01T00:00:00Z","embeddings":{}}""");

            var result = _sut.TryLoadCache("fp");
            result.Should().BeNull();
        }
    }
}
