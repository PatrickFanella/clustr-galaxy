using System;
using ClustrGalaxy.Bootstrap;
using ClustrGalaxy.Core.Activities;
using ClustrGalaxy.Core.Domain;
using ClustrGalaxy.Flight;
using ClustrGalaxy.Gameplay;
using ClustrGalaxy.World;
using UnityEngine;

namespace ClustrGalaxy.UI
{
    /// <summary>Opt-in presentation workload. Synthetic practice data never grants progression.</summary>
    public sealed class ActiveFeatureProfileScenario : MonoBehaviour
    {
        private GalaxySessionBehaviour owner;
        private FlightActivitiesController courses;
        private ExplorationFeedback audioFeedback;
        private ExplorationCelebrations effects;
        private Camera view;
        private CelebrationCue[] cues;
        private int nextCue;
        private float nextCelebration;
        public bool Initialized { get; private set; }
        public int Frames { get; private set; }
        public int GuidanceFrames { get; private set; }
        public int GhostFrames { get; private set; }
        public int GhostInViewFrames { get; private set; }
        public int AudioFrames { get; private set; }
        public int CelebrationFrames { get; private set; }
        public int MaximumVoices { get; private set; }
        public int MaximumCelebrations { get; private set; }

        public void Initialize(int seconds)
        {
            if (seconds > 600) throw new ArgumentException("Active-feature capture is bounded to 600 seconds.");
            owner = GetComponent<GalaxySessionBehaviour>();
            courses = GetComponent<FlightActivitiesController>();
            audioFeedback = GetComponent<ExplorationFeedback>();
            effects = GetComponent<ExplorationCelebrations>();
            view = Camera.main;
            if (owner?.Session?.Activities == null || courses == null || audioFeedback == null || effects == null || view == null)
                throw new InvalidOperationException("Required activity presentation is unavailable.");
            owner.Modes.SetFlightMode(FlightControlMode.PilotAssist);
            var visibility = GetComponent<GalaxySurveyVisibilityController>();
            GalaxyEntity anchor = null;
            foreach (var entity in owner.Registry.Entities)
                if (visibility.CanLabel(entity)) { anchor = entity; break; }
            if (anchor == null) throw new InvalidOperationException("No eligible resident body for ambient music.");
            var body = GetComponent<Rigidbody>();
            body.position = anchor.transform.position + new Vector3(0, 0, -800);
            body.rotation = Quaternion.identity;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            Physics.SyncTransforms();
            courses.StartPlayground(FlightActivityKind.RingRun, false);
            var run = owner.Session.FlightActivity;
            if (run?.Active != true || run.Preview) throw new InvalidOperationException("Manual course did not initialize.");
            var first = run.Course.Points[0];
            var poses = new ReplayPose[JourneyReplay.MaximumSamples];
            for (var i = 0; i < poses.Length; i++)
            {
                var angle = i * .02;
                poses[i] = new ReplayPose(i * .1, new ReplayPosition(first.X + Math.Sin(angle) * 50,
                    first.Y, first.Z - 340 + Math.Cos(angle) * 50), System.Numerics.Quaternion.Identity);
            }
            var identity = run.Identity.Value;
            var replay = new JourneyReplay(new ReplayIdentity(identity.Snapshot, identity.Mode, 1,
                run.TuningId, run.Mode, run.Course.Id, run.Course.Version), poses, first);
            owner.Session.SelectPracticeGhost(replay);
            run.AttachGhost(owner.Session.SelectedPracticeGhost);
            // Ambient playback uses eligible resident entities. The ghost and ribbons are labelled fixture content.
            audioFeedback.PlayConstellation(Array.Empty<GalaxyEntityId>());
            cues = new CelebrationCue[(seconds / 3 + 2) * 3];
            for (var i = 0; i < cues.Length; i++)
                cues[i] = new CelebrationCue("profile-fixture-ribbon-" + i, CelebrationKind.FlightRibbon, Array.Empty<GalaxyEntityId>());
            Initialized = true;
        }

        public void Sample()
        {
            Frames++;
            if (owner.Session.FlightActivity?.Active == true) GuidanceFrames++;
            if (courses.GhostDisplayed)
            {
                GhostFrames++;
                var point = view.WorldToViewportPoint(courses.GhostPosition);
                if (point.z > 0 && point.x >= 0 && point.x <= 1 && point.y >= 0 && point.y <= 1) GhostInViewFrames++;
            }
            var voices = audioFeedback.ActiveVoiceCount;
            if (voices > 0) AudioFrames++;
            MaximumVoices = Math.Max(MaximumVoices, voices);
            if (effects.ActiveCount > 0) CelebrationFrames++;
            MaximumCelebrations = Math.Max(MaximumCelebrations, effects.ActiveCount);
            if (Time.unscaledTime < nextCelebration || nextCue + 3 > cues.Length) return;
            nextCelebration = Time.unscaledTime + 3;
            for (var i = 0; i < 3; i++) effects.Show(cues[nextCue++]);
        }

        public void CopyTo(GalaxyBetaProfileReport report)
        {
            report.active_features_requested = true;
            report.active_features_initialized = Initialized;
            report.feature_frames = Frames;
            report.guidance_frames = GuidanceFrames;
            report.ghost_frames = GhostFrames;
            report.ghost_in_view_frames = GhostInViewFrames;
            report.audio_frames = AudioFrames;
            report.celebration_frames = CelebrationFrames;
            report.maximum_audio_voices = MaximumVoices;
            report.maximum_celebrations = MaximumCelebrations;
        }
    }
}
