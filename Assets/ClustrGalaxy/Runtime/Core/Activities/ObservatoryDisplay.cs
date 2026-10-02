using System;
using System.Linq;
namespace ClustrGalaxy.Core.Activities
{
    public enum ObservatoryItemKind { Keepsake, Constellation, Flight, Journal }
    public sealed class ObservatoryDisplay
    {
        public ObservatoryDisplay(int slot,ObservatoryItemKind kind,string reference,string caption="")
        {
            if(slot<0||slot>=6||!Enum.IsDefined(typeof(ObservatoryItemKind),kind)||string.IsNullOrWhiteSpace(reference)||reference.Length>256||caption==null||caption.Length>1000||caption.Any(char.IsControl))throw new ArgumentException("Invalid observatory display.");
            Slot=slot;Kind=kind;Reference=reference;Caption=caption;
        }
        public int Slot {get;} public ObservatoryItemKind Kind {get;} public string Reference {get;} public string Caption {get;}
        public bool Eligible(ActivityState state)=>Kind switch
        {
            ObservatoryItemKind.Keepsake=>state.Find(Reference)?.Completed==true&&CampaignContent.Keepsake(Reference).Length!=0,
            ObservatoryItemKind.Constellation=>state.Designs.Any(x=>x.PersonalId==Reference),
            ObservatoryItemKind.Flight=>state.Flights.Any(x=>x.Key==Reference),
            ObservatoryItemKind.Journal=>state.Diary.Any(x=>x.Id==Reference),_=>false
        };
    }
}
