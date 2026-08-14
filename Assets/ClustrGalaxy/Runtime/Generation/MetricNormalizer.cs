using System;

namespace ClustrGalaxy.Generation
{
    public static class MetricNormalizer
    {
        public static double NormalizeLog(long value, long lowerReference, long upperReference)
        {
            var lower = Math.Max(0L, lowerReference);
            var upper = Math.Max(lower, upperReference);
            if (upper == lower)
            {
                return value <= lower ? 0d : 1d;
            }

            var bounded = Math.Max(lower, Math.Min(upper, value));
            var logLower = Math.Log(1d + lower);
            var logUpper = Math.Log(1d + upper);
            return (Math.Log(1d + bounded) - logLower) / (logUpper - logLower);
        }
    }
}
