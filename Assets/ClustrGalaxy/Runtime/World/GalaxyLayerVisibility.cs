using ClustrGalaxy.Flight;

namespace ClustrGalaxy.World
{
    public readonly struct GalaxyLayerVisibility
    {
        private GalaxyLayerVisibility(bool overview, bool region, bool system, bool discussion, bool thread)
        {
            Overview = overview;
            Region = region;
            System = system;
            Discussion = discussion;
            Thread = thread;
        }

        public bool Overview { get; }
        public bool Region { get; }
        public bool System { get; }
        public bool Discussion { get; }
        public bool Thread { get; }

        public static GalaxyLayerVisibility For(
            GalaxyNavigationLevel level,
            bool hasRegion,
            bool hasSystem,
            bool hasDiscussion,
            bool hasThread)
        {
            if (level == GalaxyNavigationLevel.Galaxy)
            {
                return new GalaxyLayerVisibility(true, false, false, false, false);
            }

            if (level == GalaxyNavigationLevel.Region || !hasSystem)
            {
                return new GalaxyLayerVisibility(!hasRegion, hasRegion, false, false, false);
            }

            if (level == GalaxyNavigationLevel.System || !hasDiscussion)
            {
                return new GalaxyLayerVisibility(false, false, hasSystem, false, false);
            }

            return new GalaxyLayerVisibility(false, false, hasSystem, hasDiscussion, hasThread);
        }
    }
}
