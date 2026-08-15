using System;

namespace ClustrGalaxy.Data
{
    public static class GalaxyLaunchOptions
    {
        private const string OfflineArgument = "-clustr-offline";
        private const string LongOfflineArgument = "--clustr-offline";
        private const string LiveSmokeArgument = "--clustr-live-smoke";
        private const string BetaProfileArgument = "--clustr-beta-profile";

        public static bool IsOfflineOnly(string[] arguments)
        {
            return Contains(arguments, OfflineArgument, LongOfflineArgument);
        }

        public static bool IsLiveSmoke(string[] arguments)
        {
            return Contains(arguments, LiveSmokeArgument);
        }

        public static bool IsBetaProfile(string[] arguments)
        {
            if (arguments == null) return false;
            foreach (var argument in arguments)
            {
                if (argument != null && argument.StartsWith(BetaProfileArgument, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        public static int ProfileDurationSeconds(string[] arguments)
        {
            const int defaultSeconds = 600;
            if (arguments == null) return defaultSeconds;
            foreach (var argument in arguments)
            {
                if (argument == null || !argument.StartsWith(BetaProfileArgument + "=", StringComparison.OrdinalIgnoreCase))
                    continue;
                var value = argument.Substring(BetaProfileArgument.Length + 1);
                return int.TryParse(value, out var seconds) ? Math.Clamp(seconds, 30, 3600) : defaultSeconds;
            }
            return defaultSeconds;
        }

        private static bool Contains(string[] arguments, params string[] expected)
        {
            if (arguments == null)
            {
                return false;
            }

            foreach (var argument in arguments)
            {
                foreach (var candidate in expected)
                {
                    if (string.Equals(argument, candidate, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
