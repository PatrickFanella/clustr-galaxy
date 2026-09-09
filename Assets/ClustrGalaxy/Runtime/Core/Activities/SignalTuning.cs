using System;
namespace ClustrGalaxy.Core.Activities
{
    /// <summary>Optional skill evaluation; never controls ordinary scan completion.</summary>
    public sealed class SignalTuning
    {
        public const float RequiredHoldSeconds = 1.5f;
        public const float RequiredAlignmentSeconds = .75f;
        public const float AlignmentDegrees = 5f;
        public float AlignedSeconds { get; private set; }
        public bool Clean => AlignedSeconds >= RequiredAlignmentSeconds;
        public void Reset() => AlignedSeconds = 0;
        public void Advance(float seconds, float angleDegrees, bool automatic)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0 ||
                float.IsNaN(angleDegrees) || float.IsInfinity(angleDegrees) || angleDegrees < 0) return;
            if (automatic || angleDegrees <= AlignmentDegrees)
                AlignedSeconds = Math.Min(RequiredAlignmentSeconds, AlignedSeconds + seconds);
        }
    }
}
