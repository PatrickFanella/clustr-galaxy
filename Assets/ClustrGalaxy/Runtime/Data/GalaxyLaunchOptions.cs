using System;

namespace ClustrGalaxy.Data
{
    public static class GalaxyLaunchOptions
    {
        private const string OfflineArgument = "-clustr-offline";
        private const string LongOfflineArgument = "--clustr-offline";
        private const string LiveSmokeArgument = "--clustr-live-smoke";

        public static bool IsOfflineOnly(string[] arguments)
        {
            return Contains(arguments, OfflineArgument, LongOfflineArgument);
        }

        public static bool IsLiveSmoke(string[] arguments)
        {
            return Contains(arguments, LiveSmokeArgument);
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
