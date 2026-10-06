using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ClustrGalaxy.Core.Data;
using ClustrGalaxy.Core.Domain;
using ClustrGalaxy.Core.Persistence;
using ClustrGalaxy.Data;
using NUnit.Framework;
using UnityEngine;

namespace ClustrGalaxy.Tests
{
    public sealed class SnapshotSourceTests
    {
        private sealed class Transport : ISpatialJsonTransport
        {
            public readonly Queue<string> Replies = new();
            public readonly List<string> Requests = new();
            public Task<string> GetAsync(string uri, CancellationToken token)
            { token.ThrowIfCancellationRequested(); Requests.Add(uri); return Task.FromResult(Replies.Dequeue()); }
        }
        private static string Manifest => JsonUtility.ToJson(new SpatialManifest {
            revision_id = 9, spatial_catalog_id = 3, dimensions = 3, scene_contract = "spatial-scene-v1" });
        private static string Scene(string scope = "", string root = "", string id = "community:a", string cursor = "") =>
            JsonUtility.ToJson(new SpatialScene { revision_id = 9, spatial_catalog_id = 3, scope = scope, root_id = root,
                truncated = cursor != "", next_cursor = cursor,
                nodes = new[] { new SpatialNode { id = id, type = "community", name = id } }, links = Array.Empty<SpatialLink>() });
        [TestCase("missing-catalog")]
        [TestCase("missing-nodes")]
        [TestCase("duplicate-property")]
        [TestCase("conflicting-node")]
        public async Task MalformedOverviewIsRejectedBeforeCacheMutation(string fault)
        {
            var cache = new MemoryDurableDocumentStore(); var transport = new Transport();
            transport.Replies.Enqueue(Manifest);
            var json = Scene();
            if (fault == "missing-catalog") json = json.Replace("\"spatial_catalog_id\":3,", "");
            if (fault == "missing-nodes") json = "{\"revision_id\":9,\"spatial_catalog_id\":3,\"links\":[]}";
            if (fault == "duplicate-property") json = "{\"revision_id\":9," + json.Substring(1);
            if (fault == "conflicting-node") json = "{\"revision_id\":9,\"spatial_catalog_id\":3,\"nodes\":[{\"id\":\"a\",\"type\":\"community\",\"val\":1},{\"id\":\"a\",\"type\":\"community\",\"val\":2}],\"links\":[]}";
            transport.Replies.Enqueue(json);
            var source = new SpatialSceneSnapshotSource("https://fixture.invalid/api", false, cache, transport);
            Assert.That((await source.OpenAsync(default)).Succeeded, Is.False);
            Assert.That(cache.WriteCount, Is.Zero);
        }

        [Test]
        public async Task IdenticalOverviewDuplicatesProduceOneResidentIdentity()
        {
            var cache = new MemoryDurableDocumentStore(); var transport = new Transport();
            transport.Replies.Enqueue(Manifest);
            var scene = SpatialSceneJson.ParseScene(Scene());
            scene.nodes = new[] { scene.nodes[0], scene.nodes[0] };
            transport.Replies.Enqueue(JsonUtility.ToJson(scene));
            var source = new SpatialSceneSnapshotSource("https://fixture.invalid/api", false, cache, transport);
            var result = await source.OpenAsync(default);
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Scene.Nodes.Count, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task ConflictingRouteDuplicatesCannotMutateCache(bool acrossPages)
        {
            var cache = new MemoryDurableDocumentStore(); var transport = new Transport();
            var first = SpatialSceneJson.ParseScene(Scene("community", "region", "a", acrossPages ? "next" : ""));
            var original = new SpatialLink { source = "a", target = "b", type = "route", weight = 1 };
            var conflict = new SpatialLink { source = "a", target = "b", type = "route", weight = 2 };
            first.links = acrossPages ? new[] { original } : new[] { original, conflict };
            transport.Replies.Enqueue(JsonUtility.ToJson(first));
            if (acrossPages)
            {
                var second = SpatialSceneJson.ParseScene(Scene("community", "region", "b"));
                second.links = new[] { conflict };
                transport.Replies.Enqueue(JsonUtility.ToJson(second));
            }
            var source = new SpatialSceneSnapshotSource("https://fixture.invalid/api", false, cache, transport);
            var result = await source.LoadAsync(new GalaxySnapshotManifest(9, 3, "spatial-scene-v1"),
                new SpatialScopeRequest(SpatialScope.Community, (GalaxyEntityId)"region"), default);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(cache.WriteCount, Is.Zero);
        }

        [TestCase("", "", true)]
        [TestCase("community", "", true)]
        [TestCase("", "region", true)]
        [TestCase("system", "region", false)]
        [TestCase("community", "wrong", false)]
        public async Task CommunityBindsOptionalMetadataToPinnedRequest(string scope, string root, bool accepted)
        {
            var cache = new MemoryDurableDocumentStore(); var transport = new Transport();
            transport.Replies.Enqueue(Scene(scope, root));
            var source = new SpatialSceneSnapshotSource("https://fixture.invalid/api", false, cache, transport);
            var result = await source.LoadAsync(new GalaxySnapshotManifest(9, 3, "spatial-scene-v1"),
                new SpatialScopeRequest(SpatialScope.Community, (GalaxyEntityId)"region"), default);
            Assert.That(result.Succeeded, Is.EqualTo(accepted));
            if (!accepted) Assert.That(cache.WriteCount, Is.Zero);
        }

        [TestCase(SpatialScope.System)]
        [TestCase(SpatialScope.Discussion)]
        [TestCase(SpatialScope.Thread)]
        public async Task EntityExpansionStillRequiresPublishedScopeMetadata(SpatialScope scope)
        {
            var cache = new MemoryDurableDocumentStore(); var transport = new Transport();
            transport.Replies.Enqueue(Scene());
            var source = new SpatialSceneSnapshotSource("https://fixture.invalid/api", false, cache, transport);
            var result = await source.LoadAsync(new GalaxySnapshotManifest(9, 3, "spatial-scene-v1"),
                new SpatialScopeRequest(scope, (GalaxyEntityId)"region"), default);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(cache.WriteCount, Is.Zero);
        }

        [Test]
        public async Task OfflineReusesValidatedV2BaseWithoutCallingTransport()
        {
            var cache = new MemoryDurableDocumentStore(); var transport = new Transport();
            transport.Replies.Enqueue(Manifest); transport.Replies.Enqueue(Scene());
            var live = new SpatialSceneSnapshotSource("https://fixture.invalid/api", false, cache, transport);
            Assert.That((await live.OpenAsync(default)).Succeeded, Is.True);
            var offline = new SpatialSceneSnapshotSource("https://fixture.invalid/api", true, cache, transport);
            Assert.That((await offline.OpenAsync(default)).Succeeded, Is.True);
            Assert.That(transport.Requests.Count, Is.EqualTo(2));
            Assert.That(cache.Documents.Keys, Has.All.Contains("/v2/"));
        }
        [Test]
        public async Task PagingEscapesCursorAndRejectsWrongResponseRootWithoutCachingIt()
        {
            var cache = new MemoryDurableDocumentStore(); var transport = new Transport();
            transport.Replies.Enqueue(Scene("community", "region", "a", "opaque:1"));
            transport.Replies.Enqueue(Scene("community", "WRONG", "b"));
            var source = new SpatialSceneSnapshotSource("https://fixture.invalid/api", false, cache, transport);
            var result = await source.LoadAsync(new GalaxySnapshotManifest(9, 3, "spatial-scene-v1"),
                new SpatialScopeRequest(SpatialScope.Community, (GalaxyEntityId)"region"), default);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(transport.Requests[1], Does.Contain("cursor=opaque%3A1"));
            Assert.That(cache.WriteCount, Is.Zero);
        }
        [Test]
        public async Task RepeatedCursorCannotLoopOrCommitPartialScope()
        {
            var cache = new MemoryDurableDocumentStore(); var transport = new Transport();
            transport.Replies.Enqueue(Scene("community", "region", "a", "repeat"));
            transport.Replies.Enqueue(Scene("community", "region", "b", "repeat"));
            var source = new SpatialSceneSnapshotSource("https://fixture.invalid/api", false, cache, transport);
            var result = await source.LoadAsync(new GalaxySnapshotManifest(9, 3, "spatial-scene-v1"),
                new SpatialScopeRequest(SpatialScope.Community, (GalaxyEntityId)"region"), default);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(transport.Requests.Count, Is.EqualTo(2)); Assert.That(cache.WriteCount, Is.Zero);
        }
    }
}
