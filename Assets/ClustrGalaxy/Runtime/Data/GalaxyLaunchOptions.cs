using System;

namespace ClustrGalaxy.Data
{
    public static class GalaxyLaunchOptions
    {
        private const string OfflineArgument = "-clustr-offline";

        public static bool IsOfflineOnly(string[] arguments)
        {
            if (arguments == null)
            {
                return false;
            }

            foreach (var argument in arguments)
            {
                if (string.Equals(argument, OfflineArgument, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
