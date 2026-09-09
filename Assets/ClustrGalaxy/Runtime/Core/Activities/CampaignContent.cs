using System;
using System.Collections.Generic;
using System.Linq;
using ClustrGalaxy.Core.Domain;
namespace ClustrGalaxy.Core.Activities
{
    public sealed class CampaignObservation
    {
        public CampaignObservation(string activity,string id,string anchor,string title,string text,string hint)
        {ActivityId=activity;Id=id;Anchor=(GalaxyEntityId)anchor;Title=title;Text=text;Hint=hint;}
        public string ActivityId {get;} public string Id {get;} public GalaxyEntityId Anchor {get;}
        public string Title {get;} public string Text {get;} public string Hint {get;}
        public string EventId => "campaign:"+ActivityId+":"+Id;
    }
    public static class CampaignContent
    {
        public static IReadOnlyList<CampaignObservation> Observations {get;}=Array.AsReadOnly(new[]
        {
            new CampaignObservation("trail-harmonic","harmonic-1","subreddit:technology","A note between worlds","A low note repeats after every third orbit. A second voice seems to answer near the old computing archive.","Look near the retro-computing archive."),
            new CampaignObservation("trail-harmonic","harmonic-2","post:retro-computing","The patient reply","A worn resonator answers the low note, then leaves room for another instrument. A small drawing points toward the open-source workshop.","Listen by the open-source workshop."),
            new CampaignObservation("trail-harmonic","harmonic-3","post:open-source-ai","Room for one more","Three differently built instruments share the same five notes. Their maker left the last bar empty beside a sketch of a handheld sky.","Find the final voice near the handheld exhibit."),
            new CampaignObservation("trail-harmonic","harmonic-4","post:linux-handhelds","A chord to carry","Your probe fills the quiet bar with a soft tone. No instrument leads; each makes space. A tiny resonator is yours to keep.","Keepsake: Pocket resonator."),
            new CampaignObservation("trail-beacon","beacon-1","post:right-to-repair","A lantern without a timetable","A repair tag says: if found, let it wander. Its scratched map remembers a quiet place beside the privacy garden.","Look for the next light by the privacy garden."),
            new CampaignObservation("trail-beacon","beacon-2","post:privacy-tools","A resting place","The lantern rested here long enough for its light to warm a blank sign. A doodle of a broad ring points back toward the central system.","Return to the central technology system."),
            new CampaignObservation("trail-beacon","beacon-3","subreddit:technology","A borrowed window","Someone hung the wandering light outside a station window. Its reflection carries the outline of an old computer.","Visit the retro-computing archive."),
            new CampaignObservation("trail-beacon","beacon-4","post:retro-computing","Home is a place to leave again","The lantern has returned to its repair bench. A note invites the next traveller to take a miniature copy, leaving the original free to roam.","Keepsake: Wandering lantern."),
            new CampaignObservation("trail-archive","archive-1","post:open-source-ai","One square of light","A station window glows in a slow four-part pattern. A pencilled first square names the repair yard. There is no hurry to finish the sequence.","Continue at the repair yard whenever you wish."),
            new CampaignObservation("trail-archive","archive-2","post:right-to-repair","The repaired square","A replaced pane scatters the second pattern into many smaller lights. The next square resembles a portable screen.","Look near the handheld exhibit."),
            new CampaignObservation("trail-archive","archive-3","post:linux-handhelds","A light in a pocket","A portable viewer stores the third pattern, with a tiny privacy shutter. Its last frame points toward the privacy garden.","Finish in the privacy garden, in this session or another."),
            new CampaignObservation("trail-archive","archive-4","post:privacy-tools","Leave a light for yourself","The four patterns form an open window. Beside it is a blank nameplate: you can return without explaining where you have been.","Keepsake: Open-window prism."),
            new CampaignObservation("anomaly-echo","echo","subreddit:technology","An echo with good manners","The signal waits until your probe has finished, then answers one octave lower. It never interrupts.","The repeating harmonic trail begins nearby."),
            new CampaignObservation("anomaly-shy","shy","post:privacy-tools","A shy little satellite","A tiny satellite slips behind its screen as you approach, then peeks around the edge. Leaving it room brings it back into view.","A standalone authored encounter."),
            new CampaignObservation("anomaly-orrery","orrery","post:retro-computing","A pocket-sized sky","Six brass beads orbit a seventh. One bead carries an even smaller sky, too tiny to count.","A standalone authored encounter."),
            new CampaignObservation("anomaly-station","station","post:open-source-ai","Someone left the light on","A station slowly lights one window at a time. The empty rooms look ready for visitors rather than abandoned.","The archival light trail begins nearby."),
            new CampaignObservation("anomaly-lanterns","lanterns","post:right-to-repair","The drifting lanterns","Paper lanterns drift together without touching. One carries a repair tag and a map that has been folded many times.","The wandering beacon trail begins nearby."),
            new CampaignObservation("anomaly-inverted","inverted","post:linux-handhelds","An upside-down hello","The beacon is upside down only if you insist on your own up. Its welcoming blink reads the same from either side.","A standalone authored encounter."),
            new CampaignObservation("mystery-chorus","chorus-1","subreddit:technology","A shared interval","The central signal uses the same five notes as the small resonators. This is an authored clue, not a measurement of community sentiment.","Evidence for the archive chorus."),
            new CampaignObservation("mystery-chorus","chorus-2","post:retro-computing","Different hands","Tool marks on two resonators differ, but their tuning notches match.","Evidence for the archive chorus."),
            new CampaignObservation("mystery-chorus","chorus-3","post:open-source-ai","An unfinished score","The score has spaces labelled for whoever arrives next. No composer signed it.","Evidence for the archive chorus."),
            new CampaignObservation("mystery-chorus","chorus-4","post:linux-handhelds","Directions in a melody","A portable chart pairs a rising phrase with an outward route, but also calls it a greeting.","Evidence for the archive chorus."),
            new CampaignObservation("mystery-chorus","chorus-5","post:privacy-tools","A margin full of alternatives","A listener wrote three possibilities: greeting, navigation, or a pattern we happen to enjoy. None is crossed out.","Assemble the evidence and choose your interpretation."),
            new CampaignObservation("mystery-lights","lights-1","post:right-to-repair","A repaired invitation","The lantern tag asks its finder to leave a window lit for someone unknown.","Evidence for the waiting lights."),
            new CampaignObservation("mystery-lights","lights-2","post:privacy-tools","Names on the reverse","Several blank nameplates have handwritten names on their hidden sides. Others are still empty.","Evidence for the waiting lights."),
            new CampaignObservation("mystery-lights","lights-3","subreddit:technology","A pattern passed on","Station windows repeat the lantern sequence with one playful change each time.","Evidence for the waiting lights."),
            new CampaignObservation("mystery-lights","lights-4","post:open-source-ai","An empty chair","A small chair faces a lit window. Its cushion is new; its frame has been repaired many times.","Evidence for the waiting lights."),
            new CampaignObservation("mystery-lights","lights-5","post:retro-computing","Three notes beside a switch","Welcome, remember, play: three notes describe the same light switch. You may keep any reading that fits the evidence.","Assemble the evidence and choose your interpretation."),
        });
        public static CampaignObservation Find(string activity,string observation)
        {foreach(var item in Observations)if(item.ActivityId==activity&&item.Id==observation)return item;return null;}
        public static bool Eligible(ActivityState state,CampaignObservation clue)
        {
            if(state==null||state.Identity.Snapshot.StorageNamespace!="demo")return false;
            var record=state.Find(clue.ActivityId);
            if(record!=null&&(record.Completed||record.Observations.Contains(clue.Id)))return false;
            ActivityDefinition definition=null;
            for(var i=0;i<AuthoredActivityCatalog.Definitions.Count;i++)if(AuthoredActivityCatalog.Definitions[i].Id==clue.ActivityId){definition=AuthoredActivityCatalog.Definitions[i];break;}
            return definition.Kind!=ActivityKind.Trail||definition.Observations[record?.Observations.Count??0]==clue.Id;
        }
        public static void Validate()
        {
            foreach(var definition in AuthoredActivityCatalog.Definitions)
            foreach(var id in definition.Observations)
                if(Observations.Count(x=>x.ActivityId==definition.Id&&x.Id==id)!=1)
                    throw new ArgumentException("Campaign observation missing or duplicated: "+id);
            if(Observations.Count!=AuthoredActivityCatalog.Definitions.Sum(x=>x.Observations.Count))throw new ArgumentException("Unexpected campaign content.");
            foreach(var clue in Observations)
                if(string.IsNullOrWhiteSpace(clue.Text)||string.IsNullOrWhiteSpace(clue.Hint)||string.IsNullOrWhiteSpace(clue.Title))throw new ArgumentException("Incomplete campaign text.");
        }
        public static string Keepsake(string activity)=>activity switch
        {"trail-harmonic"=>"Pocket resonator","trail-beacon"=>"Wandering lantern","trail-archive"=>"Open-window prism",_=>""};
    }
}
