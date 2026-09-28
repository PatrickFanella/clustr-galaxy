using System;
using System.Collections.Generic;
using System.Linq;
using ClustrGalaxy.Bootstrap;
using ClustrGalaxy.Core.Activities;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
namespace ClustrGalaxy.UI
{
    public sealed class ProbeObservatory:MonoBehaviour
    {
        private GameObject panel,world;private ProbeHud hud;private ActivitySession activity;
        private ClustrGalaxy.World.GalaxyVisualCatalog assets; private Camera dioramaCamera;
        private readonly string[] exhibitKeys=new string[6];private readonly Material[] ownedMaterials=new Material[6];
        private RenderTexture texture;private Material material;private readonly Transform[] exhibits=new Transform[6];
        private readonly List<ObservatoryDisplay> choices=new();private readonly List<Button> buttons=new();
        private Text detail;private int slot,choice;private string message="";
        private int resumedFrame=-1;private JourneyReplayViewer child;private JourneyDiary diaryChild;
        public bool IsOpen=>panel!=null;
        public void Open()
        {
            if(IsOpen)return;hud=GetComponent<ProbeHud>();activity=GetComponent<GalaxySessionBehaviour>().Session.Activities;assets=GetComponent<GalaxySessionBehaviour>().PresentationAssets;
            panel=new GameObject("Probe observatory",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));panel.transform.SetParent(transform,false);
            CockpitStationSurface.Mount(panel, this);panel.GetComponent<Canvas>().sortingOrder=12;
            var scaler=panel.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=1;
            var background=Image("Backdrop");background.color=new Color(.025f,.045f,.065f);background.rectTransform.anchorMin=Vector2.zero;background.rectTransform.anchorMax=Vector2.one;background.rectTransform.offsetMin=background.rectTransform.offsetMax=Vector2.zero;
            Label("PROBE OBSERVATORY · Six places for things you want to keep",26,30,690,1220,50);
            var viewport=new GameObject("Diorama",typeof(RectTransform),typeof(RawImage));viewport.transform.SetParent(panel.transform,false);Place(viewport.GetComponent<RectTransform>(),30,630,760,410);
            world=new GameObject("Local observatory diorama");world.transform.position=Vector3.zero;
            texture=new RenderTexture(1024,512,24){name="Observatory local view"};texture.Create();viewport.GetComponent<RawImage>().texture=texture;
            var cameraObject=new GameObject("Diorama camera",typeof(Camera));cameraObject.transform.SetParent(world.transform,false);cameraObject.transform.localPosition=new Vector3(0,7,-12);cameraObject.transform.LookAt(world.transform.position+Vector3.up*.8f);
            var camera=dioramaCamera=cameraObject.GetComponent<Camera>();camera.orthographic=true;camera.orthographicSize=3.9f;camera.targetTexture=texture;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.03f,.055f,.08f);camera.cullingMask=1<<31;camera.nearClipPlane=.1f;camera.farClipPlane=50;
            if (assets?.activityMaterial != null) material = new Material(assets.activityMaterial);
            else
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Resources.Load<Shader>("GalaxyPlanetSurface");
                if (shader != null) { material = new Material(shader); material.color = new Color(.4f,.8f,.9f); }
            }
            if(assets?.observatoryPlatformPrefab!=null)
            {
                var platform=Instantiate(assets.observatoryPlatformPrefab,world.transform);platform.transform.localScale=Vector3.one*1.5f;SetLayer(platform);
            }
            var key=new GameObject("Observatory display light",typeof(Light));key.transform.SetParent(world.transform,false);key.transform.rotation=Quaternion.Euler(40,-35,0);var lamp=key.GetComponent<Light>();lamp.type=LightType.Directional;lamp.intensity=1.5f;lamp.cullingMask=1<<31;
            for(var i=0;i<6;i++){exhibits[i]=null;exhibitKeys[i]=null;}
            choices.Clear();
            foreach(var record in activity.State.Records)if(record.Completed&&CampaignContent.Keepsake(record.Id).Length>0)choices.Add(new ObservatoryDisplay(0,ObservatoryItemKind.Keepsake,record.Id,CampaignContent.Keepsake(record.Id)));
            foreach(var design in activity.State.Designs)choices.Add(new ObservatoryDisplay(0,ObservatoryItemKind.Constellation,design.PersonalId,"Personal constellation: "+design.PersonalId));
            foreach(var record in activity.State.Flights)choices.Add(new ObservatoryDisplay(0,ObservatoryItemKind.Flight,record.Key,record.CourseId+" · "+record.Mode+" · "+record.Seconds.ToString("F1")+" seconds"));
            foreach(var entry in activity.State.Diary)choices.Add(new ObservatoryDisplay(0,ObservatoryItemKind.Journal,entry.Id,entry.Text));
            slot=0;choice=0;message="Choose an item and explicitly place it in a slot.";
            detail=Label("",18,820,625,420,410);
            Add("NEXT DISPLAY SLOT",30,190,()=>{slot=(slot+1)%6;Refresh();});Add("PREVIOUS ITEM",435,190,()=>{if(choices.Count>0)choice=(choice+choices.Count-1)%choices.Count;Refresh();});Add("NEXT ITEM",840,190,()=>{if(choices.Count>0)choice=(choice+1)%choices.Count;Refresh();});
            Add("PLACE SELECTED ITEM",30,125,Save);Add("PLAY / PAUSE DISPLAY MUSIC",435,125,PlayMusic);Add("VIEW DISPLAY REPLAY",840,125,ViewReplay);
            Add("CLOSE OBSERVATORY",30,60,Close);Add("CLEAR DISPLAY SLOT",435,60,Clear);Add("BROWSE JOURNEY DIARY",840,60,ViewDiary);
            for(var i=0;i<buttons.Count;i++)buttons[i].navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnDown=buttons[(i+1)%buttons.Count],selectOnRight=buttons[(i+1)%buttons.Count],selectOnUp=buttons[(i+buttons.Count-1)%buttons.Count],selectOnLeft=buttons[(i+buttons.Count-1)%buttons.Count]};
            hud.SetModalInputCaptured(true);Refresh();EventSystem.current?.SetSelectedGameObject(buttons[0].gameObject);
        }
        private static string Preview(string text)=>text.Length<=140?text:text.Substring(0,137)+"...";
        private void Refresh()
        {
            var display=activity.State.Displays.FirstOrDefault(x=>x.Slot==slot);
            detail.text="SLOT "+(slot+1)+" / 6\n"+Preview(DisplayCaption(display))+"\n\nSELECTED ITEM\n"+(choices.Count==0?"Discover or save something first.":Preview(choices[choice].Caption))+"\n\n"+message+"\n\nLocal creations and authored keepsakes; no source claims.\nA/Enter: select · B/Escape: return";
            for(var i=0;i<6;i++)RenderDisplay(i,activity.State.Displays.FirstOrDefault(x=>x.Slot==i));
        }
        private string DisplayCaption(ObservatoryDisplay display)
        {
            if(display==null)return "Empty";
            if(display.Kind!=ObservatoryItemKind.Flight)return display.Caption;
            var flight=activity.State.Flights.FirstOrDefault(x=>x.Key==display.Reference);
            if(flight==null)return "Historical flight record unavailable";
            var name=flight.CourseId=="ring-run"?"Ring run":flight.CourseId=="figure-eight"?"Figure eight":flight.CourseId=="stable-orbit"?"Stable orbit":flight.CourseId;
            return name+" · "+(flight.Mode==ClustrGalaxy.Core.Navigation.NavigationFlightMode.PilotAssist?"Pilot Assist":"Pilot Mastery")+" · "+flight.Seconds.ToString("F1")+" seconds";
        }
        private static void SetLayer(GameObject go){foreach(var t in go.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;}
        private void RenderDisplay(int index,ObservatoryDisplay display)
        {
            var key=display==null?"empty":display.Kind+":"+display.Reference;
            if(exhibitKeys[index]==key)return;exhibitKeys[index]=key;
            if(ownedMaterials[index]!=null){Destroy(ownedMaterials[index]);ownedMaterials[index]=null;}
            if(exhibits[index]!=null){exhibits[index].gameObject.SetActive(false);Destroy(exhibits[index].gameObject);}
            var item=new GameObject("Display slot "+(index+1));item.transform.SetParent(world.transform,false);var angle=index*Mathf.PI/3;
            item.transform.localPosition=new Vector3(Mathf.Cos(angle)*3,1.45f,Mathf.Sin(angle)*3);exhibits[index]=item.transform;
            if(display==null){var marker=GameObject.CreatePrimitive(PrimitiveType.Sphere);Destroy(marker.GetComponent<Collider>());marker.transform.SetParent(item.transform,false);marker.transform.localScale=Vector3.one*.12f;if(material!=null)marker.GetComponent<Renderer>().sharedMaterial=material;}
            else if(display.Kind==ObservatoryItemKind.Keepsake)
            {
                var trails=AuthoredActivityCatalog.Definitions.Where(x=>x.Kind==ActivityKind.Trail).ToArray();var which=Array.FindIndex(trails,x=>x.Id==display.Reference);
                var prefab=which>=0&&assets?.keepsakePrefabs!=null&&which<assets.keepsakePrefabs.Length?assets.keepsakePrefabs[which]:null;
                if(prefab!=null){var model=Instantiate(prefab,item.transform);model.transform.localScale=Vector3.one*.65f;}
                else Card(item.transform,display.Caption);
            }
            else if(display.Kind==ObservatoryItemKind.Constellation)
            {
                var design=activity.State.Designs.FirstOrDefault(x=>x.PersonalId==display.Reference);
                if(design==null)Card(item.transform,"Historical creation unavailable");
                else if(material==null)Card(item.transform,"Creation saved · display material unavailable");
                else
                {
                    var styled=new Material(material);var color=ConstellationDiagram.PaletteColor(design.Palette);styled.color=color;if(styled.HasProperty("_EmissionColor"))styled.SetColor("_EmissionColor",color*.4f);ownedMaterials[index]=styled;
                    foreach(var node in design.Nodes)
                    {var body=GameObject.CreatePrimitive(PrimitiveType.Sphere);Destroy(body.GetComponent<Collider>());body.transform.SetParent(item.transform,false);body.transform.localPosition=new Vector3(node.X*.8f,.2f,node.Y*.8f);body.transform.localScale=Vector3.one*.12f;if(material!=null)body.GetComponent<Renderer>().sharedMaterial=styled;}
                    foreach(var edge in design.Edges)
                    {var first=design.Nodes.First(x=>x.Id==edge.First);var second=design.Nodes.First(x=>x.Id==edge.Second);Line(item.transform,new[]{new Vector3(first.X*.8f,.2f,first.Y*.8f),new Vector3(second.X*.8f,.2f,second.Y*.8f)},styled);}
                }
            }
            else if(display.Kind==ObservatoryItemKind.Flight)
            {
                var record=activity.State.Flights.FirstOrDefault(x=>x.Key==display.Reference);
                if(record==null)Card(item.transform,"Historical flight unavailable");
                else
                {
                    var replay=record.Ghost?.Replay;
                    if(replay!=null)
                    {
                        var origin=replay.Poses[0].Position;double extent=1;
                        foreach(var pose in replay.Poses)extent=Math.Max(extent,Math.Max(Math.Abs(pose.Position.X-origin.X),Math.Max(Math.Abs(pose.Position.Y-origin.Y),Math.Abs(pose.Position.Z-origin.Z))));
                        var points=new List<Vector3>();
                        for(var i=0;i<replay.Poses.Count;i++)
                        {
                            if(i>0&&replay.Poses[i].Seconds-replay.Poses[i-1].Seconds>.25){if(points.Count>1)Line(item.transform,points.ToArray());points.Clear();}
                            var p=replay.Poses[i].Position;points.Add(new Vector3((float)((p.X-origin.X)/extent),.25f+(float)((p.Y-origin.Y)/extent),(float)((p.Z-origin.Z)/extent)));
                        }
                        if(points.Count>1)Line(item.transform,points.ToArray());
                    }
                    Card(item.transform,record.Seconds.ToString("F1")+" s · "+record.Mode+(replay==null?" · No recording":""));
                }
            }
            else Card(item.transform,display.Caption);
            SetLayer(item);
        }
        private void Line(Transform parent,Vector3[] points,Material style=null)
        {var go=new GameObject("Personal display line",typeof(LineRenderer));go.transform.SetParent(parent,false);var line=go.GetComponent<LineRenderer>();line.useWorldSpace=false;line.positionCount=points.Length;line.SetPositions(points);line.startWidth=line.endWidth=.025f;line.sharedMaterial=style??material;}
        private void Card(Transform parent,string caption)
        {
            var go=new GameObject("Journal display",typeof(TextMesh));go.transform.SetParent(parent,false);go.transform.rotation=dioramaCamera.transform.rotation;go.transform.localPosition=Vector3.up*.25f;
            var text=go.GetComponent<TextMesh>();text.fontSize=48;text.characterSize=.035f;text.anchor=TextAnchor.MiddleCenter;text.color=new Color(.95f,.86f,.62f);
            var shortText=caption.Length>90?caption.Substring(0,87)+"...":caption;var words=shortText.Split(' ');var buffer="";var length=0;
            foreach(var word in words){if(length+word.Length>22){buffer+="\n";length=0;}buffer+=word+" ";length+=word.Length+1;}text.text=buffer;
        }
        private void Clear()
        {
            var result=activity.Apply(new ActivityIntent(Guid.NewGuid().ToString("N"),activity.State.Identity,ActivityOperation.ClearDisplay,DateTime.UtcNow,value:slot.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            message=result.Committed?"Display slot cleared; the saved item is retained.":result.Error;Refresh();hud.InvalidateExploration();
        }
        private void Save()
        {
            if(choices.Count==0)return;var item=choices[choice];
            var result=activity.Apply(new ActivityIntent(Guid.NewGuid().ToString("N"),activity.State.Identity,ActivityOperation.PlaceDisplay,DateTime.UtcNow,display:new ObservatoryDisplay(slot,item.Kind,item.Reference)));
            message=result.Committed?"Display saved locally.":result.Error;Refresh();hud.InvalidateExploration();
        }
        private void PlayMusic()
        {
            var display=activity.State.Displays.FirstOrDefault(x=>x.Slot==slot);
            var design=display?.Kind==ObservatoryItemKind.Constellation?activity.State.Designs.FirstOrDefault(x=>x.PersonalId==display.Reference):null;
            if(design==null){message="Select a displayed constellation to hear its phrase.";Refresh();return;}
            var feedback=GetComponent<ExplorationFeedback>();
            if(feedback?.MusicPlaying==true){feedback.ToggleMusic();message="Constellation music paused.";}
            else {feedback?.PlayConstellation(design.Nodes.Select(x=>x.Id).ToArray(),design.MusicPreset);message="Playing the saved constellation instrument preset · artistic sonification.";}
            Refresh();
        }
        private void ViewReplay()
        {
            var display=activity.State.Displays.FirstOrDefault(x=>x.Slot==slot);
            var record=display?.Kind==ObservatoryItemKind.Flight?activity.State.Flights.FirstOrDefault(x=>x.Key==display.Reference):null;
            if(record?.Ghost==null){message="This display has no saved recording. Enable best-ghost saving before completing a new best run.";Refresh();return;}
            var replay=record.Ghost.Replay;panel.SetActive(false);world.SetActive(false);child=this.GetOrAddComponent<JourneyReplayViewer>();
            child.Open(replay,()=>{if(panel==null)return;panel.SetActive(true);world.SetActive(true);resumedFrame=Time.frameCount;Refresh();EventSystem.current?.SetSelectedGameObject(buttons[5].gameObject);});
        }
        private void ViewDiary()
        {
            panel.SetActive(false);world.SetActive(false);diaryChild=this.GetOrAddComponent<JourneyDiary>();
            diaryChild.Open(()=>{if(panel==null)return;panel.SetActive(true);world.SetActive(true);resumedFrame=Time.frameCount;Refresh();EventSystem.current?.SetSelectedGameObject(buttons[8].gameObject);});
        }
        private void Update()
        {
            if(!IsOpen||!panel.activeSelf||Time.frameCount<=resumedFrame)return;
            if(Keyboard.current?.escapeKey.wasPressedThisFrame==true||Gamepad.current?.buttonEast.wasPressedThisFrame==true){Close();return;}
            if(!activity.State.Preferences.ReducedMotion)for(var i=0;i<6;i++)if(exhibits[i]!=null&&exhibitKeys[i].StartsWith("Keepsake:",StringComparison.Ordinal))exhibits[i].Rotate(Vector3.up,Time.unscaledDeltaTime*8,Space.Self);
        }
        private Image Image(string name){var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(panel.transform,false);return go.GetComponent<Image>();}
        private static void Place(RectTransform rect,float x,float y,float w,float h){rect.anchorMin=rect.anchorMax=Vector2.zero;rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(x,y);rect.sizeDelta=new Vector2(w,h);}
        private Text Label(string value,int size,float x,float y,float w,float h){var label=ConstellationDiagram.MakeText(panel.transform,value,size);Place(label.rectTransform,x,y,w,h);return label;}
        private void Add(string value,float x,float y,UnityEngine.Events.UnityAction action)
        {var image=Image(value);Place(image.rectTransform,x,y,390,50);image.color=new Color(.14f,.23f,.28f);var button=image.gameObject.AddComponent<Button>();button.onClick.AddListener(action);buttons.Add(button);var label=ConstellationDiagram.MakeText(image.transform,value,16);label.alignment=TextAnchor.MiddleCenter;label.rectTransform.anchorMin=Vector2.zero;label.rectTransform.anchorMax=Vector2.one;label.rectTransform.offsetMin=label.rectTransform.offsetMax=Vector2.zero;}
        public void Close()
        {if(!IsOpen)return;child?.Close();diaryChild?.Close();panel.SetActive(false);Destroy(panel);panel=null;Destroy(world);world=null;if(texture!=null){texture.Release();Destroy(texture);texture=null;}if(material!=null)Destroy(material);for(var i=0;i<ownedMaterials.Length;i++){if(ownedMaterials[i]!=null)Destroy(ownedMaterials[i]);ownedMaterials[i]=null;}buttons.Clear();choices.Clear();if(hud!=null)hud.SetModalInputCaptured(false);}
        private void OnDestroy(){Close();}
    }
}
