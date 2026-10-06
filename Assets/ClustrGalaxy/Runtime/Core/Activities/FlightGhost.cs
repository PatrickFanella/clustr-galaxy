using System;
namespace ClustrGalaxy.Core.Activities
{
    public sealed class FlightGhost
    {
        public FlightGhost(JourneyReplay replay, ReplayPosition anchor)
        { Replay=replay??throw new ArgumentNullException(nameof(replay));Anchor=anchor; }
        public JourneyReplay Replay {get;} public ReplayPosition Anchor {get;}
        public bool Compatible(FlightActivityRun run)
        {
            if(run==null||run.Preview||!run.Identity.HasValue)return false;
            var id=run.Identity.Value;
            return Replay.Identity.IsCompetitiveWith(new ReplayIdentity(id.Snapshot,id.Mode,1,run.TuningId,run.Mode,run.Course.Id,run.Course.Version));
        }
        // Validate compatibility once when attaching, not in the per-frame playback path.
        public ReplayPose At(double seconds,ReplayPosition newAnchor)
        {
            var p=Replay.At(seconds);return new ReplayPose(p.Seconds,new ReplayPosition(
                newAnchor.X+(p.Position.X-Anchor.X),newAnchor.Y+(p.Position.Y-Anchor.Y),newAnchor.Z+(p.Position.Z-Anchor.Z)),p.Rotation);
        }
    }
}
