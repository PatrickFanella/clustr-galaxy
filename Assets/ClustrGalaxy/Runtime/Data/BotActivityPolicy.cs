using System;

namespace ClustrGalaxy.Data
{
    public static class BotActivityPolicy
    {
        private const int BroadCommunityThreshold = 20;

        public static bool ShouldSuppress(
            string authorName,
            bool explicitlyAutomated,
            int distinctCommunityCount)
        {
            if (explicitlyAutomated)
            {
                return true;
            }

            var normalized = Normalize(authorName);
            if (normalized.Equals("automoderator", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (distinctCommunityCount < BroadCommunityThreshold)
            {
                return false;
            }

            return normalized.EndsWith("bot", StringComparison.OrdinalIgnoreCase) ||
                normalized.Contains("_bot", StringComparison.OrdinalIgnoreCase) ||
                normalized.Contains("-bot", StringComparison.OrdinalIgnoreCase);
        }

        private static string Normalize(string authorName)
        {
            var normalized = (authorName ?? string.Empty).Trim();
            if (normalized.StartsWith("u/", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(2);
            }
            else if (normalized.StartsWith("@", StringComparison.Ordinal))
            {
                normalized = normalized.Substring(1);
            }

            return normalized;
        }
    }
}
