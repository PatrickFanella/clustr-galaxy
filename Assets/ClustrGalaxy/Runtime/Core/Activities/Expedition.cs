using System;
using System.Collections.Generic;
using System.Linq;
using ClustrGalaxy.Core.Domain;
using ClustrGalaxy.Core.World;
namespace ClustrGalaxy.Core.Activities
{
    public enum ExpeditionMood { QuietDrift, UnexpectedConnection, PilotPractice }
    public enum ExpeditionStatus { Active, Paused, ReadyToClose, Completed, Abandoned }
    public sealed class ExpeditionPlan
    {
        public ExpeditionPlan(string id,ExpeditionMood mood,IEnumerable<GalaxyEntityId> stops)
        {
            var copy=(stops??Array.Empty<GalaxyEntityId>()).ToArray();
            if(!ReplayIdentity.Token(id,false)||!Enum.IsDefined(typeof(ExpeditionMood),mood)||copy.Any(x=>string.IsNullOrEmpty(x.Value))||copy.Distinct().Count()!=copy.Length||
                (mood==ExpeditionMood.PilotPractice?copy.Length!=0:copy.Length<1||copy.Length>(mood==ExpeditionMood.QuietDrift?3:5)))throw new ArgumentException("Invalid expedition plan.");
            Id=id;Mood=mood;Stops=Array.AsReadOnly(copy);
        }
        public string Id {get;} public ExpeditionMood Mood {get;} public IReadOnlyList<GalaxyEntityId> Stops {get;}
        public int Count=>Mood==ExpeditionMood.PilotPractice?3:Stops.Count;
        public bool Shorter=>Mood!=ExpeditionMood.PilotPractice&&Stops.Count<(Mood==ExpeditionMood.QuietDrift?3:5);
        public static ExpeditionPlan Select(ExpeditionMood mood,IEnumerable<GalaxyEntityId> eligible,IEnumerable<ResidentRoute> published)
        {
            var ids=eligible.Distinct().OrderBy(x=>x).ToArray();var selected=new List<GalaxyEntityId>();
            if(mood==ExpeditionMood.QuietDrift)selected.AddRange(ids.Take(3));
            if(mood==ExpeditionMood.UnexpectedConnection&&ids.Length>0)
            {
                var routes=published.Where(x=>ids.Contains(x.SourceId)&&ids.Contains(x.TargetId)).OrderBy(x=>x.Id).ToArray();
                if(routes.Length==0)throw new InvalidOperationException("No eligible loaded connection is available.");
                selected.Add(routes[0].SourceId);
                while(selected.Count<5)
                {
                    var current=selected[selected.Count-1];GalaxyEntityId next=default;
                    foreach(var route in routes)
                    {
                        if(route.SourceId==current&&!selected.Contains(route.TargetId)){next=route.TargetId;break;}
                        if(route.TargetId==current&&!selected.Contains(route.SourceId)){next=route.SourceId;break;}
                    }
                    if(next==default)break;selected.Add(next);
                }
            }
            if(mood!=ExpeditionMood.PilotPractice&&selected.Count==0)throw new InvalidOperationException("No eligible loaded destinations are available.");
            return new ExpeditionPlan(Guid.NewGuid().ToString("N"),mood,selected);
        }
    }
    public sealed class ExpeditionRecord
    {
        public ExpeditionRecord(ExpeditionPlan plan,int current=0,ExpeditionStatus status=ExpeditionStatus.Active)
        {
            Plan=plan??throw new ArgumentNullException(nameof(plan));
            if(!Enum.IsDefined(typeof(ExpeditionStatus),status)||current<0||current>plan.Count||
                ((status==ExpeditionStatus.ReadyToClose||status==ExpeditionStatus.Completed)!=(current==plan.Count))&&status!=ExpeditionStatus.Abandoned)
                throw new ArgumentException("Invalid expedition progress.");
            Current=current;Status=status;
        }
        public ExpeditionPlan Plan {get;} public int Current {get;} public ExpeditionStatus Status {get;}
        public ExpeditionRecord Advance()=>new(Plan,Current+1,Current+1==Plan.Count?ExpeditionStatus.ReadyToClose:ExpeditionStatus.Active);
        public string CourseId=>Current switch{0=>"ring-run",1=>"figure-eight",_=>"stable-orbit"};
    }
}
