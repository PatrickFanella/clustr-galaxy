namespace ClustrGalaxy.Tests
{
    /// <summary>
    /// Stable, deterministic content shared by the Survey gameplay test plan.
    /// It deliberately mirrors IDs emitted by <c>GalaxyPrototypeBootstrap</c>, so
    /// future Survey tests never depend on live graph data.
    /// </summary>
    internal static class SurveyFixture
    {
        public const long RevisionId = 9;
        public const long CatalogId = 3;
        public const string StartingSystemId = "subreddit:technology";

        public static readonly string[] ConstellationMembers =
        {
            "subreddit:technology",
            "post:open-source-ai",
            "post:linux-handhelds",
            "post:right-to-repair",
            "post:privacy-tools",
            "post:retro-computing"
        };

        public static readonly SurveyRoute[] Routes =
        {
            new SurveyRoute("subreddit:technology", "post:open-source-ai"),
            new SurveyRoute("subreddit:technology", "post:linux-handhelds")
        };

        public static readonly SurveyThreadNode CommentThread = new SurveyThreadNode(
            "comment:open-source-ai-1",
            new SurveyThreadNode(
                "comment:open-source-ai-1-reply-1",
                new SurveyThreadNode("comment:open-source-ai-1-reply-3")));

        internal readonly struct SurveyRoute
        {
            public SurveyRoute(string sourceId, string destinationId)
            {
                SourceId = sourceId;
                DestinationId = destinationId;
            }

            public string SourceId { get; }
            public string DestinationId { get; }
        }

        internal sealed class SurveyThreadNode
        {
            public SurveyThreadNode(string stableId, SurveyThreadNode child = null)
            {
                StableId = stableId;
                Child = child;
            }

            public string StableId { get; }
            public SurveyThreadNode Child { get; }
        }
    }
}
