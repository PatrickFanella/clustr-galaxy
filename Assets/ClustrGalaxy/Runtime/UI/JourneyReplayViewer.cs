using System;
using System.Collections.Generic;
using ClustrGalaxy.Core.Activities;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ClustrGalaxy.UI
{
    /// <summary>Standalone top-down playback; it never changes the resident graph or dispatches gameplay intents.</summary>
    public sealed class JourneyReplayViewer : MonoBehaviour
    {
        private GameObject panel;
        private ProbeHud hud;
        private JourneyReplay replay;
        private RectTransform marker;
        private Text clock;
        private double time,minX,minZ,scale;
        private int displayedSecond=-1,eventIndex=-1;private Text annotation;
        private bool playing; private Action closed; private Slider timeline; private float playbackSpeed=1;
        public float PlaybackSpeed=>playbackSpeed;
        public string LastExportPath {get;private set;}
        public void Seek(double seconds){if(!double.IsFinite(seconds))throw new ArgumentException("Invalid replay time.");time=Math.Clamp(seconds,0,replay.Duration);displayedSecond=-1;}
        public bool IsOpen=>panel!=null;
        public double PlaybackSeconds=>time;
        public void Open(JourneyReplay value,Action onClose=null)
        {
            if(panel!=null)return;closed=onClose;replay=value??throw new ArgumentNullException(nameof(value));hud=GetComponent<ProbeHud>();
            panel=new GameObject("Local flight replay",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            panel.transform.SetParent(transform,false);CockpitStationSurface.Mount(panel, this);panel.GetComponent<Canvas>().sortingOrder=12;
            var scaler=panel.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=1;
            var background=Image("Backdrop",panel.transform);background.color=new Color(.025f,.045f,.065f,1);
            background.rectTransform.anchorMin=Vector2.zero;background.rectTransform.anchorMax=Vector2.one;background.rectTransform.offsetMin=background.rectTransform.offsetMax=Vector2.zero;
            TextAt("LOCAL REPLAY · "+replay.Identity.CourseId,28,30,680,1220,50);
            TextAt("Top-down projection · "+replay.Identity.FlightMode+" · Snapshot "+replay.Identity.Snapshot+"\nPlayback only · no discoveries or awards",18,30,625,1200,60);
            var area=new GameObject("Recorded flight",typeof(RectTransform));area.transform.SetParent(panel.transform,false);Place(area.GetComponent<RectTransform>(),60,530,1160,320);
            minX=replay.Poses[0].Position.X;minZ=replay.Poses[0].Position.Z;var maxX=minX;var maxZ=minZ;
            foreach(var pose in replay.Poses){minX=Math.Min(minX,pose.Position.X);minZ=Math.Min(minZ,pose.Position.Z);maxX=Math.Max(maxX,pose.Position.X);maxZ=Math.Max(maxZ,pose.Position.Z);}
            scale=Math.Min(1100/Math.Max(1,maxX-minX),270/Math.Max(1,maxZ-minZ));
            var count=Math.Min(256,replay.Poses.Count);
            for(var i=1;i<count;i++)
            {
                var first=(i-1)*(replay.Poses.Count-1)/(count-1);var last=i*(replay.Poses.Count-1)/(count-1);var gap=false;
                for(var sample=first+1;sample<=last;sample++)if(replay.Poses[sample].Seconds-replay.Poses[sample-1].Seconds>.25){gap=true;break;}
                if(gap)continue;
                var a=Point(replay.Poses[first].Position);var b=Point(replay.Poses[last].Position);
                var line=Image("Recorded segment",area.transform);line.color=new Color(.25f,.6f,.65f);var rect=line.rectTransform;
                rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,.5f);rect.anchoredPosition=a;
                rect.sizeDelta=new Vector2((b-a).magnitude,2);rect.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg);
            }
            var probe=Image("Recorded probe",area.transform);probe.color=Color.yellow;marker=probe.rectTransform;marker.anchorMin=marker.anchorMax=new Vector2(0,1);marker.sizeDelta=new Vector2(14,14);
            annotation=TextAt("",16,30,228,1220,30);eventIndex=-1;
            clock=TextAt("",18,30,195,1220,30);
            var track=Image("Replay timeline",panel.transform);Place(track.rectTransform,30,150,1220,25);track.color=new Color(.12f,.22f,.26f);
            timeline=track.gameObject.AddComponent<Slider>();timeline.minValue=0;timeline.maxValue=(float)replay.Duration;
            var handle=Image("Timeline handle",track.transform);handle.color=new Color(.9f,.73f,.32f);handle.rectTransform.sizeDelta=new Vector2(20,25);
            timeline.handleRect=handle.rectTransform;timeline.targetGraphic=handle;timeline.onValueChanged.AddListener(value=>Seek(value));
            var buttons=new List<Button>();buttons.Add(Button("PLAY / PAUSE",30,()=>playing=!playing));buttons.Add(Button("RESTART",340,()=>{time=0;playing=true;}));buttons.Add(Button("CLOSE REPLAY",650,Close));
            buttons.Add(Button("SPEED: 1×",960,()=>{playbackSpeed=playbackSpeed==1?2:playbackSpeed==2 ? .5f : 1;buttons[3].GetComponentInChildren<Text>().text="SPEED: "+playbackSpeed+"×";displayedSecond=-1;}));
            buttons.Add(Button("EXPORT POSTCARD",30,()=>
            {
                try{LastExportPath=JourneyPostcard.Export("A FLIGHT THROUGH THE GALAXY",replay.Identity.FlightMode+" · "+replay.Identity.CourseId+"\n"+time.ToString("F1")+" / "+replay.Duration.ToString("F1")+" seconds\nTop-down recorded path; gaps omitted.","Snapshot "+replay.Identity.Snapshot+" · "+replay.Identity.WorldMode+" · Recorded local motion; no source relationships",replay,time);clock.text="1920 × 1080 replay postcard saved in Local Creations.";}
                catch(Exception error){clock.text="Postcard was not saved: "+error.Message;}
            }));
            for(var i=0;i<buttons.Count;i++)Place(buttons[i].GetComponent<RectTransform>(),30+i*246,85,236,50);
            for(var i=0;i<buttons.Count;i++)buttons[i].navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnLeft=buttons[(i+buttons.Count-1)%buttons.Count],selectOnRight=buttons[(i+1)%buttons.Count],selectOnUp=timeline,selectOnDown=buttons[(i+1)%buttons.Count]};
            timeline.navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnUp=buttons[0],selectOnDown=buttons[0]};
            time=0;displayedSecond=-1;playing=true;playbackSpeed=1;hud.SetModalInputCaptured(true);EventSystem.current?.SetSelectedGameObject(buttons[0].gameObject);
        }
        private Vector2 Point(ReplayPosition p)=>new((float)(30+(p.X-minX)*scale),(float)(-295+(p.Z-minZ)*scale));
        private static Image Image(string name,Transform parent)
        {var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);return go.GetComponent<Image>();}
        private static void Place(RectTransform rect,float x,float y,float width,float height)
        {rect.anchorMin=rect.anchorMax=Vector2.zero;rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(x,y);rect.sizeDelta=new Vector2(width,height);}
        private Text TextAt(string text,int size,float x,float y,float width,float height)
        {var label=ConstellationDiagram.MakeText(panel.transform,text,size);Place(label.rectTransform,x,y,width,height);return label;}
        private Button Button(string text,float x,UnityEngine.Events.UnityAction action)
        {
            var image=Image(text,panel.transform);Place(image.rectTransform,x,85,290,50);image.color=new Color(.14f,.23f,.28f);
            var button=image.gameObject.AddComponent<Button>();button.onClick.AddListener(action);
            var label=ConstellationDiagram.MakeText(image.transform,text,18);label.alignment=TextAnchor.MiddleCenter;label.rectTransform.anchorMin=Vector2.zero;label.rectTransform.anchorMax=Vector2.one;label.rectTransform.offsetMin=label.rectTransform.offsetMax=Vector2.zero;return button;
        }
        private void Update()
        {
            if(panel==null)return;
            if(Keyboard.current?.escapeKey.wasPressedThisFrame==true||Gamepad.current?.buttonEast.wasPressedThisFrame==true){Close();return;}
            if(playing){time=Math.Min(replay.Duration,time+Time.unscaledDeltaTime*playbackSpeed);if(time>=replay.Duration)playing=false;}
            var next=-1;for(var i=0;i<replay.Events.Count;i++){if(replay.Events[i].Seconds>time)break;next=i;}
            if(next!=eventIndex){eventIndex=next;annotation.supportRichText=false;annotation.text=next<0?"":"Recorded annotation · "+replay.Events[next].Kind+" · "+replay.Events[next].Label+" · Playback only";}
            marker.anchoredPosition=Point(replay.At(time).Position);timeline.SetValueWithoutNotify((float)time);
            if(displayedSecond!=(int)time){displayedSecond=(int)time;clock.text=time.ToString("F1")+" / "+replay.Duration.ToString("F1")+" seconds · "+playbackSpeed+"× · Timeline: seek · A/Enter: select · B/Escape: return";}
        }
        public void Close(){if(panel==null)return;panel.SetActive(false);Destroy(panel);panel=null;var callback=closed;closed=null;if(callback!=null)callback();else hud.SetModalInputCaptured(false);}
        private void OnDestroy(){Close();}
    }
}
