using UnityEngine;
using UnityEngine.UI;
using ClustrGalaxy.Core.Presentation;

namespace ClustrGalaxy.UI
{
    public sealed partial class ProbeHud
    {
        private GameObject flightDisplay;
        private Transform sectionRail;
        private Text orbitInstrument;
        private Text flightModeLabel, targetLabel, targetDescription, missionLabel, provenanceLabel;
        private readonly Text[] headingDigits=new Text[3], distanceDigits=new Text[6];
        private RectTransform targetBrackets;
        private int lastHeading=-1,lastDistance=-1;
        private Text DeckText(string name,string value,float x,float y,float width,int size,Color color)
        {
            var text=Label(flightDisplay.transform,name);PlaceMeter(text,x,y,width);text.text=value;text.fontSize=size;text.color=color;
            text.rectTransform.sizeDelta=new Vector2(width,size*2.8f);return text;
        }
        private void DeckLine(Transform parent,string name,float x,float y,float w,float h,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);
            var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);
            rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(w,h);
            var image=go.GetComponent<Image>();image.color=color;image.raycastTarget=false;
        }
        private void BuildFlightDisplay()
        {
            flightDisplay=new GameObject("Flight instruments",typeof(RectTransform));flightDisplay.transform.SetParent(canvasObject.transform,false);
            var root=flightDisplay.GetComponent<RectTransform>();root.anchorMin=Vector2.zero;root.anchorMax=Vector2.one;root.offsetMin=root.offsetMax=Vector2.zero;
            var amber=new Color(.95f,.69f,.35f);var cyan=new Color(.47f,.8f,.84f);var quiet=new Color(.58f,.69f,.73f);
            DeckText("Ship designation","C L U S T R  /  S U R V E Y O R",48,27,390,17,Color.white);
            flightModeLabel=DeckText("Flight mode","",998,28,230,15,amber);
            provenanceLabel=DeckText("Source context","",48,55,480,11,quiet);
            DeckLine(root,"Port header rule",48,79,285,1,new Color(.36f,.5f,.55f,.55f));
            DeckLine(root,"Starboard header rule",998,79,230,1,new Color(.36f,.5f,.55f,.55f));
            DeckText("Heading legend","HEADING",580,22,130,10,quiet);
            for(var i=0;i<3;i++)headingDigits[i]=DeckText("Heading digit","0",606+i*16,37,18,20,cyan);
            // Thin central aiming marks leave the object itself unobscured.
            DeckLine(root,"Aim left",625,359,8,1,cyan);DeckLine(root,"Aim right",647,359,8,1,cyan);
            DeckLine(root,"Aim top",639,345,1,8,cyan);DeckLine(root,"Aim bottom",639,367,1,8,cyan);
            for(var i=-2;i<=2;i++)if(i!=0){DeckLine(root,"Pitch left",594,360+i*28,15,1,new Color(.4f,.7f,.72f,.3f));DeckLine(root,"Pitch right",671,360+i*28,15,1,new Color(.4f,.7f,.72f,.3f));}
            DeckLine(root,"Velocity panel",55,565,265,110,new Color(.015f,.025f,.036f,.8f));
            DeckLine(root,"Target panel",959,530,270,165,new Color(.015f,.025f,.036f,.85f));
            DeckLine(root,"Velocity accent",55,565,2,110,amber);DeckLine(root,"Target accent",1227,530,2,145,cyan);
            BuildMeter("VELOCITY / m/s",speedDigits,74,600,true);
            BuildMeter("SCAN / %",chargeDigits,977,651,false);
            DeckText("Velocity help","TRIGGERS  THRUST    B  BRAKE",74,649,245,10,quiet);
            targetLabel=DeckText("Target name","NO TARGET",977,544,235,16,Color.white);
            targetDescription=DeckText("Target context","F / A  ACQUIRE SIGNAL",977,570,235,10,quiet);
            DeckText("Range label","RANGE / m",977,597,88,10,quiet);
            for(var i=0;i<6;i++)distanceDigits[i]=DeckText("Range digit","0",1070+i*12,594,14,14,cyan);
            missionLabel=DeckText("Current intention","",360,601,560,14,new Color(.82f,.87f,.85f));missionLabel.alignment=TextAnchor.MiddleCenter;
            orbitInstrument=DeckText("Orbital instrument","GRAVITY ACTIVE · THRUST ASSIST AVAILABLE",420,558,460,12,amber);orbitInstrument.alignment=TextAnchor.MiddleCenter;
            BuildTrajectoryDisplay();
            DeckText("Console hint","TAB / START   COMMAND CONSOLE       M / SELECT   CHART",385,681,540,11,quiet);
            var bracket=new GameObject("Target brackets",typeof(RectTransform));bracket.transform.SetParent(root,false);targetBrackets=bracket.GetComponent<RectTransform>();
            targetBrackets.anchorMin=targetBrackets.anchorMax=new Vector2(.5f,.5f);targetBrackets.sizeDelta=new Vector2(52,52);
            for(var side=0;side<2;side++)for(var top=0;top<2;top++){
                DeckLine(targetBrackets,"Target corner",side*43,top*51,9,1,amber);
                DeckLine(targetBrackets,"Target corner",side*51,top*43,1,9,amber);
            }
            targetBrackets.gameObject.SetActive(false);
        }
        private void RefreshFlightDisplay(ProbeHudReadModel model)
        {
            flightModeLabel.text=(model.Survey?"SURVEY":"EXPLORE")+" / "+model.FlightMode.ToUpperInvariant().Replace("PILOT", "").Trim();
            provenanceLabel.text=model.AuthoredContent?"AUTHORED SYSTEM  /  LOCAL DISCOVERIES":model.DataStatus;
            targetLabel.text=string.IsNullOrEmpty(model.TargetId)?"NO TARGET":model.TargetIdentified?model.TargetName:"UNIDENTIFIED SIGNAL";
            targetDescription.text=string.IsNullOrEmpty(model.TargetId)?"F / A  ACQUIRE SIGNAL":(model.AuthoredContent?"AUTHORED DISCOVERY":"SOURCE ENTITY")+"  ·  "+(model.ScanAvailable?"HOLD X / R TO SCAN":model.TargetKind);
            missionLabel.text=!string.IsNullOrEmpty(model.Notification)?model.Notification:!string.IsNullOrEmpty(model.NavigationStatus)?model.NavigationStatus:!string.IsNullOrEmpty(model.Objective)?model.Objective:"Free flight · Follow your curiosity";
        }
        private void UpdateFlightInstruments()
        {
            var show=!InputCaptured&&navigation?.IsMapOpen!=true;
            if(flightDisplay.activeSelf!=show)flightDisplay.SetActive(show);
            if(!show)return;
            var orbitActive = navigation != null && navigation.OrbitActive;
            var caption = flight.DriveVelocity.HasValue ? "SYSTEM CRUISE · FICTIONAL DRIVE" : orbitActive ? "AUTO ORBIT GUIDANCE" : "GRAVITY ACTIVE · THRUST ASSIST AVAILABLE";
            if (orbitInstrument.text != caption) orbitInstrument.text = caption;
            UpdateTrajectoryDisplay();
            var heading=Mathf.RoundToInt(transform.eulerAngles.y)%360;if(heading!=lastHeading){lastHeading=heading;UpdateDigits(headingDigits,heading);}
            var distance=targeting?.Target!=null?Mathf.RoundToInt(targeting.Target.SurfaceDistance(transform.position)):0;
            if(distance!=lastDistance){lastDistance=distance;UpdateDigits(distanceDigits,distance);}
            var camera=targetingCamera;
            if(camera==null){targetingCamera=GetComponentInChildren<Camera>();camera=targetingCamera;}
            if(camera==null)return;
            var point=targeting?.Target!=null?camera.WorldToViewportPoint(targeting.Target.transform.position):new Vector3(-1,-1,-1);
            var visible=point.z>0&&point.x>.02f&&point.x<.98f&&point.y>.05f&&point.y<.95f;
            if(targetBrackets.gameObject.activeSelf!=visible)targetBrackets.gameObject.SetActive(visible);
            if(visible)targetBrackets.anchoredPosition=new Vector2((point.x-.5f)*1280,(point.y-.5f)*720);
        }
        private Camera targetingCamera;
    }
}
