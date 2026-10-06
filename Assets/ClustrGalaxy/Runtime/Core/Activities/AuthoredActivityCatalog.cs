using System;
using System.Collections.Generic;

namespace ClustrGalaxy.Core.Activities
{
    public static class AuthoredActivityCatalog
    {
        public static IReadOnlyList<ActivityDefinition> Definitions { get; } = Array.AsReadOnly(new[]
        {
            new ActivityDefinition("trail-harmonic", "A note looking for its chord", ActivityKind.Trail,
                new[] { "harmonic-1", "harmonic-2", "harmonic-3", "harmonic-4" }),
            new ActivityDefinition("trail-beacon", "The lantern that wandered", ActivityKind.Trail,
                new[] { "beacon-1", "beacon-2", "beacon-3", "beacon-4" }),
            new ActivityDefinition("trail-archive", "A light left on", ActivityKind.Trail,
                new[] { "archive-1", "archive-2", "archive-3", "archive-4" }),
            new ActivityDefinition("anomaly-echo", "An echo with good manners", ActivityKind.Anomaly, new[] { "echo" }),
            new ActivityDefinition("anomaly-shy", "A shy little satellite", ActivityKind.Anomaly, new[] { "shy" }),
            new ActivityDefinition("anomaly-orrery", "A pocket-sized sky", ActivityKind.Anomaly, new[] { "orrery" }),
            new ActivityDefinition("anomaly-station", "Someone left the light on", ActivityKind.Anomaly, new[] { "station" }),
            new ActivityDefinition("anomaly-lanterns", "The drifting lanterns", ActivityKind.Anomaly, new[] { "lanterns" }),
            new ActivityDefinition("anomaly-inverted", "An upside-down hello", ActivityKind.Anomaly, new[] { "inverted" }),
            new ActivityDefinition("mystery-chorus", "Who taught the archive to sing?", ActivityKind.Mystery,
                new[] { "chorus-1", "chorus-2", "chorus-3", "chorus-4", "chorus-5" },
                new[] { "A shared greeting", "A navigation song", "A beautiful coincidence" }),
            new ActivityDefinition("mystery-lights", "Who are the lights waiting for?", ActivityKind.Mystery,
                new[] { "lights-1", "lights-2", "lights-3", "lights-4", "lights-5" },
                new[] { "A welcome for wanderers", "A garden of memories", "A game passed between friends" })
        });
    }
}
