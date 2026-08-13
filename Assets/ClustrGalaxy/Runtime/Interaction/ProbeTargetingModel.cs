namespace ClustrGalaxy.Interaction
{
    public static class ProbeTargetingModel
    {
        public static float Score(float angleDegrees, float distance, bool directReticleHit)
        {
            var score = angleDegrees * 20f + distance * 0.01f;
            return directReticleHit ? score - 10000f : score;
        }
    }
}
