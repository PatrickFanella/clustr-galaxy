using System;
using System.Collections.Generic;

namespace ClustrGalaxy.Progression
{
    public enum SurveyContractMetric
    {
        ScannedPosts,
        RevealedRoutes,
        CompletedSystems,
        CompletedAuthoredConstellations
    }

    /// <summary>Immutable authored objective. Its counter is snapshot-local player progress.</summary>
    public sealed class SurveyContractDefinition
    {
        public SurveyContractDefinition(string id, string displayName, SurveyContractMetric metric, int required)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A contract ID is required.", nameof(id));
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("A display name is required.", nameof(displayName));
            if (required < 1) throw new ArgumentOutOfRangeException(nameof(required));
            Id = id; DisplayName = displayName; Metric = metric; Required = required;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public SurveyContractMetric Metric { get; }
        public int Required { get; }

        public static readonly IReadOnlyList<SurveyContractDefinition> Defaults = new[]
        {
            new SurveyContractDefinition("contract:scan-three-posts", "Map three post signals", SurveyContractMetric.ScannedPosts, 3),
            new SurveyContractDefinition("contract:reveal-two-routes", "Reveal two routes", SurveyContractMetric.RevealedRoutes, 2),
            new SurveyContractDefinition("contract:complete-one-system", "Complete one system", SurveyContractMetric.CompletedSystems, 1),
            new SurveyContractDefinition("contract:complete-one-authored-constellation", "Complete one authored constellation", SurveyContractMetric.CompletedAuthoredConstellations, 1)
        };
    }
}
