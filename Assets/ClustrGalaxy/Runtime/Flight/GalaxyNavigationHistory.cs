using System;
using System.Collections.Generic;
using UnityEngine;

namespace ClustrGalaxy.Flight
{
    public enum GalaxyNavigationLevel
    {
        Galaxy,
        Region,
        System,
        Inspect
    }

    public readonly struct NavigationPose
    {
        public NavigationPose(Vector3 position, Quaternion rotation)
        {
            Position = position;
            Rotation = rotation;
        }

        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
    }

    public sealed class GalaxyNavigationHistory
    {
        private readonly List<Frame> frames = new();

        public GalaxyNavigationLevel Level => frames.Count == 0
            ? GalaxyNavigationLevel.Galaxy
            : frames[^1].Level;

        public string StableId => frames.Count == 0 ? string.Empty : frames[^1].StableId;
        public int Depth => frames.Count;

        public void Enter(GalaxyNavigationLevel level, string stableId, NavigationPose returnPose)
        {
            if (level == GalaxyNavigationLevel.Galaxy)
            {
                throw new ArgumentException("Galaxy is the history root and cannot be pushed.", nameof(level));
            }
            if (string.IsNullOrWhiteSpace(stableId))
            {
                throw new ArgumentException("A stable entity ID is required.", nameof(stableId));
            }

            frames.Add(new Frame(level, stableId, returnPose));
        }

        public bool TryBack(out NavigationPose returnPose)
        {
            if (frames.Count == 0)
            {
                returnPose = default;
                return false;
            }

            var current = frames[^1];
            frames.RemoveAt(frames.Count - 1);
            returnPose = current.ReturnPose;
            return true;
        }

        public void Reset() => frames.Clear();

        private readonly struct Frame
        {
            public Frame(GalaxyNavigationLevel level, string stableId, NavigationPose returnPose)
            {
                Level = level;
                StableId = stableId;
                ReturnPose = returnPose;
            }

            public GalaxyNavigationLevel Level { get; }
            public string StableId { get; }
            public NavigationPose ReturnPose { get; }
        }
    }
}
