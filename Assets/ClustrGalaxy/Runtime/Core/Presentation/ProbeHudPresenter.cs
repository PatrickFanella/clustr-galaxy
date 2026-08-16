using System;
using System.Collections.Generic;
using System.Linq;

namespace ClustrGalaxy.Core.Presentation
{
    public enum ProbeHudAction
    {
        EnterSurvey, ReturnToExplore, ChooseStartingSystem, Scan, FastTravel, PlotAutopilot,
        TrackMastery, AddConstellationMember, SavePersonalConstellation, RequestLocalQueue,
        ConfirmLocalQueue, CancelLocalQueue
    }

    public sealed class ProbeHudReadModel
    {
        public float Speed { get; set; }
        public string FlightMode { get; set; } = string.Empty;
        public bool Survey { get; set; }
        public bool RequiresSurveyStart { get; set; }
        public string DataStatus { get; set; } = string.Empty;
        public string NavigationLevel { get; set; } = string.Empty;
        public string NavigationStatus { get; set; } = string.Empty;
        public string TargetName { get; set; } = string.Empty;
        public string TargetKind { get; set; } = string.Empty;
        public string TargetId { get; set; } = string.Empty;
        public bool TargetKnown { get; set; }
        public bool TargetIdentified { get; set; }
        public bool TargetNavigable { get; set; }
        public bool ScanAvailable { get; set; }
        public float ScanCharge { get; set; }
        public int ScannerLevel { get; set; } = 1;
        public string Objective { get; set; } = string.Empty;
        public string Notification { get; set; } = string.Empty;
        public string QueueStatus { get; set; } = string.Empty;
        public bool HasSavedPersonalConstellation { get; set; }
        public bool AwaitingQueueConfirmation { get; set; }
        public IReadOnlyList<ProbeHudThreadRow> ThreadRows { get; set; } = Array.Empty<ProbeHudThreadRow>();
    }

    public sealed class ProbeHudThreadRow
    {
        public ProbeHudThreadRow(int depth, string author, long score, long replies, string excerpt, bool hasMore)
        { Depth = Math.Max(0, depth); Author = author ?? string.Empty; Score = score; Replies = replies;
            Excerpt = excerpt ?? string.Empty; HasMore = hasMore; }
        public int Depth { get; }
        public string Author { get; }
        public long Score { get; }
        public long Replies { get; }
        public string Excerpt { get; }
        public bool HasMore { get; }
    }

    public sealed class ProbeHudPresentation
    {
        internal ProbeHudPresentation(IEnumerable<string> lines, IEnumerable<ProbeHudAction> actions,
            IReadOnlyList<ProbeHudThreadRow> threadRows)
        { Lines = (lines ?? Array.Empty<string>()).ToArray(); Actions = (actions ?? Array.Empty<ProbeHudAction>()).Distinct().ToArray();
            ThreadRows = threadRows ?? Array.Empty<ProbeHudThreadRow>(); }
        public IReadOnlyList<string> Lines { get; }
        public IReadOnlyList<ProbeHudAction> Actions { get; }
        public IReadOnlyList<ProbeHudThreadRow> ThreadRows { get; }
        public const string AuthoredProvenance = "Authored discovery — not source data";
        public const string LocalOnlyQueueNotice = "LOCAL ONLY — nothing will be sent over the network.";
    }

    public sealed class ProbeHudPresenter
    {
        public ProbeHudPresentation Present(ProbeHudReadModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            var lines = new List<string>
            {
                $"SPEED  {model.Speed:000.0}",
                "FLIGHT  " + model.FlightMode,
                "WORLD  " + (model.Survey ? "SURVEY" : "EXPLORE"),
                "DATA  " + model.DataStatus,
                "VIEW  " + model.NavigationLevel,
                ProbeHudPresentation.AuthoredProvenance
            };
            var actions = new List<ProbeHudAction>();
            if (!model.Survey) actions.Add(ProbeHudAction.EnterSurvey);
            else if (model.RequiresSurveyStart) actions.Add(ProbeHudAction.ChooseStartingSystem);
            else actions.Add(ProbeHudAction.ReturnToExplore);

            if (!string.IsNullOrWhiteSpace(model.TargetId))
            {
                lines.Add(model.TargetIdentified ? "TARGET  " + model.TargetName : "TARGET  UNIDENTIFIED SIGNAL");
                if (model.TargetIdentified) lines.Add($"{model.TargetKind}  •  {model.TargetId}");
                if (model.ScanAvailable) actions.Add(ProbeHudAction.Scan);
                if (model.TargetNavigable)
                {
                    actions.Add(ProbeHudAction.FastTravel);
                    actions.Add(ProbeHudAction.PlotAutopilot);
                    actions.Add(ProbeHudAction.TrackMastery);
                }
                if (model.Survey && model.TargetIdentified) actions.Add(ProbeHudAction.AddConstellationMember);
            }
            if (model.Survey && !model.RequiresSurveyStart)
            {
                lines.Add("SCANNER LVL " + Math.Max(1, model.ScannerLevel));
                if (!string.IsNullOrWhiteSpace(model.Objective)) lines.Add("OBJECTIVE  " + model.Objective);
                actions.Add(ProbeHudAction.SavePersonalConstellation);
            }
            if (model.HasSavedPersonalConstellation && !model.AwaitingQueueConfirmation)
                actions.Add(ProbeHudAction.RequestLocalQueue);
            if (model.AwaitingQueueConfirmation)
            {
                lines.Add(ProbeHudPresentation.LocalOnlyQueueNotice);
                actions.Add(ProbeHudAction.ConfirmLocalQueue);
                actions.Add(ProbeHudAction.CancelLocalQueue);
            }
            if (!string.IsNullOrWhiteSpace(model.QueueStatus)) lines.Add(model.QueueStatus);
            if (!string.IsNullOrWhiteSpace(model.Notification)) lines.Add(model.Notification);
            if (!string.IsNullOrWhiteSpace(model.NavigationStatus)) lines.Add("TRAVEL  " + model.NavigationStatus);
            return new ProbeHudPresentation(lines, actions, model.ThreadRows);
        }
    }
}
