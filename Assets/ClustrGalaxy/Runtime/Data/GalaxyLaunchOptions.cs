using System;

namespace ClustrGalaxy.Data
{
    public static class GalaxyLaunchOptions
    {
        private const string OfflineArgument = "-clustr-offline";
        private const string LongOfflineArgument = "--clustr-offline";

        public static bool IsOfflineOnly(string[] arguments)
        {
            if (arguments == null)
            {
                return false;
            }

            foreach (var argument in arguments)
            {
                if (string.Equals(argument, OfflineArgument, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(argument, LongOfflineArgument, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
