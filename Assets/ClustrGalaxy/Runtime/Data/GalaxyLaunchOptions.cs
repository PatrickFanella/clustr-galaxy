using System;

namespace ClustrGalaxy.Data
{
    public static class GalaxyLaunchOptions
    {
        private const string OfflineArgument = "-clustr-offline";
        private const string LongOfflineArgument = "--clustr-offline";
        private const string LiveSmokeArgument = "--clustr-live-smoke";
        private const string BetaProfileArgument = "--clustr-beta-profile";

        public static string FixtureApi(string[] arguments)
        {
            const string prefix = "--clustr-fixture-url=";
            foreach (var argument in arguments ?? Array.Empty<string>())
            {
                if (argument == null || !argument.StartsWith(prefix, StringComparison.Ordinal)) continue;
                if (!Uri.TryCreate(argument.Substring(prefix.Length), UriKind.Absolute, out var uri) ||
                    uri.Scheme != "http" || uri.Host != "127.0.0.1" || !string.IsNullOrEmpty(uri.UserInfo) ||
                    !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
                    throw new ArgumentException("The deterministic fixture must use an explicit http://127.0.0.1 endpoint.");
                if (IsDemo(arguments))
                    throw new ArgumentException("Fixture and demo launch modes are mutually exclusive.");
                return uri.AbsoluteUri.TrimEnd('/');
            }
            return null;
        }

        public static string LocalProfileId(string[] arguments)
        {
            const string prefix = "--clustr-profile=";
            var profile = "offline-pilot";
            var found = false;
            foreach (var argument in arguments ?? Array.Empty<string>())
            {
                if (argument == null || !argument.StartsWith(prefix, StringComparison.Ordinal)) continue;
                if (found) throw new ArgumentException("Choose one local profile.");
                found = true; profile = argument.Substring(prefix.Length);
                if (profile.Length < 1 || profile.Length > 64) throw new ArgumentException("Local profile must contain 1–64 letters, digits, hyphens or underscores.");
                foreach (var c in profile)
                    if (!(c >= 'a' && c <= 'z') && !(c >= 'A' && c <= 'Z') && !(c >= '0' && c <= '9') && c != '-' && c != '_')
                        throw new ArgumentException("Local profile contains an invalid character.");
            }
            return profile;
        }

        public static bool IsDemo(string[] arguments) => Contains(arguments, "--clustr-demo");

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
