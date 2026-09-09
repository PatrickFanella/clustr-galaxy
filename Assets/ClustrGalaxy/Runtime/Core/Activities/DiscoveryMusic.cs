using System;
namespace ClustrGalaxy.Core.Activities
{
    public static class DiscoveryMusic
    {
        private static readonly int[] Pentatonic = { 0, 2, 4, 7, 9 };
        public static int SemitoneFor(string stableId)
        {
            if (string.IsNullOrWhiteSpace(stableId)) throw new ArgumentException("Stable identity is required.");
            uint hash = 2166136261;
            foreach (var character in stableId) hash = unchecked((hash ^ character) * 16777619);
            return Pentatonic[hash % 5] + (int)(hash / 5 % 2) * 12;
        }
    }
}
