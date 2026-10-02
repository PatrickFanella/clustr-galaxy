using System;
using System.Linq;
using ClustrGalaxy.Core.Domain;
namespace ClustrGalaxy.Core.Activities
{
    /// <summary>Observation layout only; these IDs are never resident source entities.</summary>
    public sealed class EvidenceBoard
    {
        public EvidenceBoard(string activityId, ConstellationDesign layout)
        {
            if (string.IsNullOrWhiteSpace(activityId) || layout == null || layout.PersonalId != activityId || layout.Nodes.Count != 5)
                throw new ArgumentException("A case board requires its five observation cards.");
            ActivityId = activityId; Layout = layout;
        }
        public string ActivityId { get; }
        public ConstellationDesign Layout { get; }
        public void Validate(ActivityDefinition definition, ActivityRecord record, bool requireConnection)
        {
            if (definition == null || definition.Kind != ActivityKind.Mystery || definition.Id != ActivityId || record == null ||
                !Layout.Nodes.Select(n => n.Id.Value).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(definition.Observations.OrderBy(x => x, StringComparer.Ordinal)) ||
                Layout.Nodes.Any(n => !record.Observations.Contains(n.Id.Value)) || (requireConnection && Layout.Edges.Count == 0))
                throw new ArgumentException("Gather all five observations, arrange their cards and draw at least one personal connection.");
        }
        public static EvidenceBoard Default(ActivityDefinition definition) => new(definition.Id,
            ConstellationDesign.Default(definition.Id, definition.Observations.Select(x => (GalaxyEntityId)x)));
    }
}
