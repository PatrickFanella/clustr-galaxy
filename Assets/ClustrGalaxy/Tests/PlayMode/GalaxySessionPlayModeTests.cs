using System.Collections;
using System.IO;
using ClustrGalaxy.Gameplay;
using ClustrGalaxy.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ClustrGalaxy.Tests.PlayMode
{
    public sealed class GalaxySessionPlayModeTests
    {
        [UnityTest]
        public IEnumerator SurveyStartUsesV2AndAppliesVisibilityAsOneCommittedOutcome()
        {
            var root = Path.Combine(Application.temporaryCachePath, "clustr-playmode-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var v1 = Path.Combine(root, "profiles", "v1", "pilot.json");
            Directory.CreateDirectory(Path.GetDirectoryName(v1));
            File.WriteAllText(v1, "legacy-byte-sentinel");
            var host = new GameObject("session-host");
            var starting = new GameObject("starting-system");
            var remote = new GameObject("remote-system");
            try
            {
                var registry = new GalaxyEntityRegistry();
                starting.AddComponent<GalaxyEntity>().Initialize("subreddit:technology", "r/technology", "subreddit star", 1, registry);
                remote.AddComponent<GalaxyEntity>().Initialize("subreddit:remote", "r/remote", "subreddit star", 1, registry);
                var visibility = host.AddComponent<GalaxySurveyVisibilityController>();
                var modes = new GalaxyModeState();
                visibility.BindRegistry(registry);
                visibility.Initialize(modes, SurveySnapshotKey.Demo, "pilot", root);
                modes.SetWorldMode(GalaxyWorldMode.Survey);
                var outcomes = 0;
                visibility.SurveyTransitionCommitted += _ => outcomes++;

                Assert.That(visibility.StartAt("subreddit:technology"), Is.True);
                Assert.That(outcomes, Is.EqualTo(1));
                Assert.That(visibility.SurveySession.State.StartingSystemId.Value, Is.EqualTo("subreddit:technology"));
                Assert.That(remote.activeSelf, Is.False);
                Assert.That(File.ReadAllText(v1), Is.EqualTo("legacy-byte-sentinel"));
                Assert.That(Directory.Exists(Path.Combine(root, "ClustrGalaxy", "profiles", "v2")), Is.True);
                yield return null;
            }
            finally
            {
                Object.Destroy(host); Object.Destroy(starting); Object.Destroy(remote);
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }
    }
}
