using System;
namespace ClustrGalaxy.Core.Activities
{
    /// <summary>Transient observation timing; only durable activity outcomes reveal journal content.</summary>
    public sealed class CampaignDiscovery
    {
        private readonly ActivitySession activities;
        private readonly double[] held=new double[CampaignContent.Observations.Count];
        private ActivityIntent pending;
        public CampaignDiscovery(ActivitySession activities){this.activities=activities??throw new ArgumentNullException(nameof(activities));CampaignContent.Validate();}
        public string Error {get;private set;}="";
        public void ResetObservation() => Array.Clear(held, 0, held.Length);
        public ActivityOutcome Advance(int index,bool visible,double distance,double angle,double seconds)
        {
            if(index<0||index>=held.Length||double.IsNaN(seconds)||double.IsInfinity(seconds)||seconds<=0||seconds>.25)throw new ArgumentException("Invalid campaign sample.");
            var clue=CampaignContent.Observations[index];
            if(!visible||!CampaignContent.Eligible(activities.State,clue)||double.IsNaN(distance)||double.IsInfinity(distance)||distance<0||distance>220||double.IsNaN(angle)||double.IsInfinity(angle)||angle<0||angle>25)
            {held[index]=0;return null;}
            if(pending!=null)return null;
            held[index]+=seconds;if(held[index]<3)return null;
            pending=new ActivityIntent(clue.EventId,activities.State.Identity,ActivityOperation.Observe,DateTime.UtcNow,clue.ActivityId,clue.Id);
            return Retry();
        }
        public ActivityOutcome Retry()
        {
            if(pending==null)return null;
            var result=activities.Apply(pending);Error=result.Committed?"":result.Error;
            if(result.Committed)pending=null;
            return result;
        }
    }
}
