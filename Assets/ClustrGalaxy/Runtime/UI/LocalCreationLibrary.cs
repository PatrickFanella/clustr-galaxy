using System;
using System.IO;
using ClustrGalaxy.Persistence;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
namespace ClustrGalaxy.UI
{
    public sealed class LocalCreationLibrary:MonoBehaviour
    {
        private GameObject panel;private ProbeHud hud;private LocalReplayLibrary library;private string[] files=Array.Empty<string>();private int selected;
        private InputField source;private Text details;private string pendingDelete,message="";private readonly System.Collections.Generic.List<Button> buttons=new();
        private int resumedFrame=-1; private JourneyReplayViewer child;private LocalImageViewer imageChild;private string selectedGhostPath;
        public bool IsOpen=>panel!=null;
        public string SelectedPath=>files.Length>0&&selected<files.Length?files[selected]:null;
        public void Open()
        {
            if(IsOpen)return;hud=GetComponent<ProbeHud>();library=new LocalReplayLibrary(Path.Combine(Application.persistentDataPath,"ClustrGalaxy"));
            panel=new GameObject("Local creation library",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));panel.transform.SetParent(transform,false);
            CockpitStationSurface.Mount(panel, this);panel.GetComponent<Canvas>().sortingOrder=12;var scale=panel.GetComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1280,720);scale.matchWidthOrHeight=1;
            var background=Image("Backdrop");background.color=new Color(.025f,.045f,.065f);background.rectTransform.anchorMin=Vector2.zero;background.rectTransform.anchorMax=Vector2.one;background.rectTransform.offsetMin=background.rectTransform.offsetMax=Vector2.zero;
            Label("LOCAL CREATIONS · Replays, postcards and screenshots",28,30,685,1220,50);details=Label("",18,30,610,1220,270);
            var input=Image("Local replay import path");Place(input.rectTransform,30,325,1220,50);input.color=new Color(.12f,.2f,.25f);source=input.gameObject.AddComponent<InputField>();source.characterLimit=4096;
            var text=ConstellationDiagram.MakeText(input.transform,"",18);text.rectTransform.anchorMin=Vector2.zero;text.rectTransform.anchorMax=Vector2.one;text.rectTransform.offsetMin=new Vector2(10,5);text.rectTransform.offsetMax=new Vector2(-10,-5);source.textComponent=text;
            var hint=ConstellationDiagram.MakeText(input.transform,"Absolute local .clustr-replay path (keyboard entry)",18);hint.rectTransform.anchorMin=Vector2.zero;hint.rectTransform.anchorMax=Vector2.one;hint.rectTransform.offsetMin=new Vector2(10,5);hint.rectTransform.offsetMax=new Vector2(-10,-5);source.placeholder=hint;
            Add("PREVIOUS FILE",30,245,()=>Move(-1));Add("NEXT FILE",435,245,()=>Move(1));Add("VIEW SELECTED ITEM",840,245,View);
            Add("IMPORT LOCAL REPLAY",30,175,Import);Add("REQUEST DELETE SELECTED",435,175,()=>{if(files.Length>0)pendingDelete=files[selected];message="Confirm deletion below, or cancel. This only removes the selected local file.";Refresh();});Add("CONFIRM DELETE",840,175,Delete);
            Add("CANCEL DELETE",30,105,()=>{pendingDelete=null;message="Deletion cancelled.";Refresh();});Add("CLOSE LIBRARY",435,105,Close);Add("USE SELECTED PRACTICE GHOST",840,105,UseGhost);
            for(var i=0;i<buttons.Count;i++)buttons[i].navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnDown=buttons[(i+1)%buttons.Count],selectOnRight=buttons[(i+1)%buttons.Count],selectOnUp=buttons[(i+buttons.Count-1)%buttons.Count],selectOnLeft=buttons[(i+buttons.Count-1)%buttons.Count]};
            selected=0;pendingDelete=null;message="Files stay on this computer. Share them yourself; this game never uploads them.";hud.SetModalInputCaptured(true);Refresh();EventSystem.current?.SetSelectedGameObject(buttons[0].gameObject);
        }
        private void Move(int direction){if(files.Length>0)selected=(selected+direction+files.Length)%files.Length;pendingDelete=null;Refresh();}
        private void Refresh()
        {
            try
            {
                var prior=files.Length>0&&selected<files.Length?files[selected]:null;files=library.Files();var retained=prior==null?-1:Array.IndexOf(files,prior);selected=retained>=0?retained:Math.Min(selected,Math.Max(0,files.Length-1));long bytes=0;foreach(var path in files)bytes+=library.BytesFor(path);
                details.text=files.Length+" explicitly saved files · "+(bytes/1048576d).ToString("F2")+" MiB\n\n"+
                    (files.Length==0?"No local creations yet.":"FILE "+(selected+1)+" / "+files.Length+"\n"+(Path.GetFileName(files[selected])=="image.png"?"Screenshot · "+File.GetLastWriteTimeUtc(files[selected]).ToString("u"):Path.GetFileName(files[selected]))+" · "+library.BytesFor(files[selected])+" bytes")+
                    "\n\n"+message+"\n\nImports: declarative poses only, maximum 64 MiB / 9,000 samples. Imported playback grants no discoveries or awards.\nPNG postcards and screenshots can be viewed here; files stay on this computer.\nA/Enter: confirm · B/Escape: return";
            }
            catch(Exception e){details.text="Local files unavailable: "+e.Message;}
        }
        private void Import(){try{library.Import(source.text);message="Imported locally for standalone playback. Source identity is preserved; no progression was granted.";}catch(Exception e){message="Import rejected: "+e.Message;}Refresh();}
        private void View()
        {
            try
            {
                if(files.Length==0)throw new InvalidOperationException("Choose a saved replay first.");
                if(files[selected].EndsWith(".png",StringComparison.Ordinal))
                {
                    imageChild=this.GetOrAddComponent<LocalImageViewer>();
                    imageChild.OpenSaved(files[selected],()=>{if(panel==null)return;panel.SetActive(true);resumedFrame=Time.frameCount;Refresh();EventSystem.current?.SetSelectedGameObject(buttons[2].gameObject);});
                    panel.SetActive(false);return;
                }
                var replay=library.Read(files[selected]);panel.SetActive(false);child=this.GetOrAddComponent<JourneyReplayViewer>();
                child.Open(replay,()=>{if(panel==null)return;panel.SetActive(true);resumedFrame=Time.frameCount;Refresh();EventSystem.current?.SetSelectedGameObject(buttons[2].gameObject);});
            }
            catch(Exception e){panel.SetActive(true);message="Cannot view this file: "+e.Message;Refresh();EventSystem.current?.SetSelectedGameObject(buttons[2].gameObject);}
        }
        private void UseGhost()
        {
            try
            {
                if(files.Length==0||!files[selected].EndsWith(".clustr-replay",StringComparison.Ordinal))throw new InvalidOperationException("Choose a course replay first.");
                var replay=library.Read(files[selected]);var owner=GetComponent<ClustrGalaxy.Bootstrap.GalaxySessionBehaviour>();
                var tuning=GetComponent<ClustrGalaxy.Flight.ObserverProbeController>().ReplayTuningIdentity();
                if(replay.Identity.TuningId!=tuning)throw new InvalidOperationException("Different flight tuning; standalone playback is still available.");
                owner.Session.SelectPracticeGhost(replay);selectedGhostPath=files[selected];
                message="Selected practice ghost for "+replay.Identity.CourseId+" · "+replay.Identity.FlightMode+". Start/restart the matching course and mode. Playback grants no awards.";
            }
            catch(Exception e){message="Practice ghost unavailable: "+e.Message;}
            Refresh();
        }
        private void Delete(){try{if(pendingDelete==null)throw new InvalidOperationException("Request a deletion first.");library.Delete(pendingDelete);if(pendingDelete==selectedGhostPath){GetComponent<ClustrGalaxy.Bootstrap.GalaxySessionBehaviour>().Session.ClearPracticeGhost();selectedGhostPath=null;}pendingDelete=null;message="Selected local file deleted.";}catch(Exception e){message=e.Message;}Refresh();}
        private Image Image(string name){var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(panel.transform,false);return go.GetComponent<Image>();}
        private static void Place(RectTransform rect,float x,float y,float w,float h){rect.anchorMin=rect.anchorMax=Vector2.zero;rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(x,y);rect.sizeDelta=new Vector2(w,h);}
        private Text Label(string value,int size,float x,float y,float w,float h){var label=ConstellationDiagram.MakeText(panel.transform,value,size);Place(label.rectTransform,x,y,w,h);return label;}
        private void Add(string value,float x,float y,UnityEngine.Events.UnityAction action)
        {var image=Image(value);Place(image.rectTransform,x,y,390,50);image.color=new Color(.14f,.23f,.28f);var button=image.gameObject.AddComponent<Button>();button.onClick.AddListener(action);buttons.Add(button);var label=ConstellationDiagram.MakeText(image.transform,value,16);label.alignment=TextAnchor.MiddleCenter;label.rectTransform.anchorMin=Vector2.zero;label.rectTransform.anchorMax=Vector2.one;label.rectTransform.offsetMin=label.rectTransform.offsetMax=Vector2.zero;}
        private void Update(){if(IsOpen&&panel.activeSelf&&Time.frameCount>resumedFrame&&((Keyboard.current?.escapeKey.wasPressedThisFrame==true&&!(source!=null&&source.isFocused))||Gamepad.current?.buttonEast.wasPressedThisFrame==true))Close();}
        public void Close(){if(!IsOpen)return;var closingPanel=panel;panel=null;child?.Close();imageChild?.Close();closingPanel.SetActive(false);Destroy(closingPanel);buttons.Clear();if(hud!=null)hud.SetModalInputCaptured(false);}
        private void OnDestroy(){Close();}
    }
}
