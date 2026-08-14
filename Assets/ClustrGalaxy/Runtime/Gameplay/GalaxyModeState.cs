using System;

namespace ClustrGalaxy.Gameplay
{
    /// <summary>
    /// The player's information mode and flight-control mode are deliberately
    /// independent. A caller must make an explicit world-mode change; cycling
    /// controls can only affect <see cref="FlightMode"/>.
    /// </summary>
    public sealed class GalaxyModeState
    {
        public GalaxyWorldMode WorldMode { get; private set; } = GalaxyWorldMode.Explore;
        public FlightControlMode FlightMode { get; private set; } = FlightControlMode.PilotAssist;

        public event Action Changed = delegate { };
        public event Action<GalaxyWorldMode> WorldModeChanged = delegate { };
        public event Action<FlightControlMode> FlightModeChanged = delegate { };

        public void SetWorldMode(GalaxyWorldMode value)
        {
            if (WorldMode == value)
            {
                return;
            }

            WorldMode = value;
            WorldModeChanged(value);
            Changed();
        }

        public void SetFlightMode(FlightControlMode value)
        {
            if (FlightMode == value)
            {
                return;
            }

            FlightMode = value;
            FlightModeChanged(value);
            Changed();
        }

        public void CycleFlightMode(int direction)
        {
            if (direction == 0)
            {
                return;
            }

            const int count = 3;
            var next = ((int)FlightMode + Math.Sign(direction) + count) % count;
            SetFlightMode((FlightControlMode)next);
        }
    }

    public enum GalaxyWorldMode
    {
        Explore,
        Survey
    }

    public enum FlightControlMode
    {
        Autopilot,
        PilotAssist,
        PilotMastery
    }
}
