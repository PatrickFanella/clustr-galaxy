using System;
using System.Collections.Generic;
using ClustrGalaxy.Bootstrap;
using ClustrGalaxy.Core.Activities;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
namespace ClustrGalaxy.UI
{
    public sealed class JourneyDiary:MonoBehaviour
    {
        private GameObject panel;private ProbeHud hud;private ActivitySession activity;private InputField draft;private Text details;
        private readonly List<Button> buttons=new();private int selected,page;private string message="",draftIntent;private Action closed;private string pendingDelete;
        public bool IsOpen=>panel!=null;public string LastExportPath {get;private set;}
        public void Open(Action onClose=null,ActivitySession model=null)
        {
            if(IsOpen)return;closed=onClose;hud=GetComponent<ProbeHud>();activity=model??GetComponent<GalaxySessionBehaviour>().Session.Activities;
            panel=new GameObject("Journey diary",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));panel.transform.SetParent(transform,false);
            CockpitStationSurface.Mount(panel, this);panel.GetComponent<Canvas>().sortingOrder=13;
            var scaler=panel.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=1;
            var backdrop=Image("Backdrop");backdrop.color=new Color(.025f,.045f,.065f);backdrop.rectTransform.anchorMin=Vector2.zero;backdrop.rectTransform.anchorMax=Vector2.one;backdrop.rectTransform.offsetMin=backdrop.rectTransform.offsetMax=Vector2.zero;
            Label("JOURNEY DIARY · Moments worth keeping",28,30,685,1220,50);details=Label("",18,30,610,1220,300);
            var input=Image("Bookmark text");Place(input.rectTransform,30,290,1220,50);input.color=new Color(.12f,.2f,.25f);draft=input.gameObject.AddComponent<InputField>();draft.characterLimit=1000;
            var text=ConstellationDiagram.MakeText(input.transform,"",18);Stretch(text.rectTransform);draft.textComponent=text;
            var hint=ConstellationDiagram.MakeText(input.transform,"Write a personal bookmark (keyboard entry; explicit Save)",18);Stretch(hint.rectTransform);draft.placeholder=hint;draft.onValueChanged.AddListener(_=>draftIntent=null);
            Add("PREVIOUS MOMENT",30,225,()=>{Move(-1);});Add("NEXT MOMENT",435,225,()=>{Move(1);});Add("EXPORT DIARY POSTCARD",840,225,Export);
            Add("SAVE BOOKMARK",30,155,Save);Add("CLOSE DIARY",435,155,Close);Add("NEXT TEXT PAGE",840,155,()=>{page=1-page;Refresh();});
            Add("KEEP SELECTED MOMENT",30,85,()=>ChangeSelected(ActivityOperation.RetainDiaryEntry));
            Add("REMOVE SAVED BOOKMARK",435,85,()=>
            {
                if(activity.State.Diary.Count==0)return;var entry=activity.State.Diary[selected];
                if(!entry.Retained){message="This automatic moment is not a saved bookmark.";Refresh();return;}
                if(pendingDelete!=entry.Id){pendingDelete=entry.Id;message="Press Remove Saved Bookmark again to confirm. Moving to another moment cancels.";Refresh();return;}
                ChangeSelected(ActivityOperation.DeleteBookmark);pendingDelete=null;
            });
            Add("CANCEL REMOVAL",840,85,()=>{pendingDelete=null;message="Removal cancelled.";Refresh();});
            for(var i=0;i<buttons.Count;i++)buttons[i].navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnDown=buttons[(i+1)%buttons.Count],selectOnRight=buttons[(i+1)%buttons.Count],selectOnUp=buttons[(i+buttons.Count-1)%buttons.Count],selectOnLeft=buttons[(i+buttons.Count-1)%buttons.Count]};
            selected=Math.Max(0,activity.State.Diary.Count-1);message="Latest 200 automatic moments, plus saved bookmarks kept until you remove them.";
            hud.SetModalInputCaptured(true);Refresh();EventSystem.current?.SetSelectedGameObject(buttons[0].gameObject);
        }
        private void Move(int direction){page=0;pendingDelete=null;var count=activity.State.Diary.Count;if(count>0)selected=(selected+direction+count)%count;Refresh();}
        private void Refresh()
        {
            var entries=activity.State.Diary;selected=Math.Clamp(selected,0,Math.Max(0,entries.Count-1));
            var entry=entries.Count==0?null:entries[selected];
            var content=entry?.Text??"";if(content.Length<=500)page=0;var start=page*500;var excerpt=content.Substring(start,Math.Min(500,content.Length-start));
            details.text=(entry==null?"No recorded moments yet.":"MOMENT "+(selected+1)+" / "+entries.Count+" · "+entry.Utc.ToString("u")+(entry.Retained?" · SAVED BOOKMARK":" · AUTOMATIC MOMENT")+"\n\n"+excerpt+(content.Length>500?"\nText page "+(page+1)+" / 2":""))+"\n\n"+message+"\n\n"+Provenance()+"\nA/Enter: select · B/Escape: return · Text entry suspends flight";
        }
        private string Provenance()=>activity.State.Identity.Mode+" · Snapshot "+activity.State.Identity.Snapshot+" · Local diary and personal observations; no source conclusions.";
        private void Save()
        {
            draftIntent??="bookmark:"+Guid.NewGuid().ToString("N");var result=activity.Apply(new ActivityIntent(draftIntent,activity.State.Identity,ActivityOperation.SaveBookmark,DateTime.UtcNow,value:draft.text));
            if(result.Committed){draft.text="";draftIntent=null;selected=activity.State.Diary.Count-1;message="Bookmark saved locally.";hud.InvalidateExploration();}else message="Bookmark not saved: "+result.Error+" Try Save again.";
            Refresh();
        }
        private void ChangeSelected(ActivityOperation operation)
        {
            if(activity.State.Diary.Count==0)return;var entry=activity.State.Diary[selected];
            var outcome=activity.Apply(new ActivityIntent(Guid.NewGuid().ToString("N"),activity.State.Identity,operation,DateTime.UtcNow,value:entry.Id));
            message=outcome.Committed?(operation==ActivityOperation.DeleteBookmark?"Saved bookmark removed.":"Moment kept as a permanent bookmark."):outcome.Error;
            hud.InvalidateExploration();Refresh();
        }
        private void Export()
        {
            try{if(activity.State.Diary.Count==0)throw new InvalidOperationException("Choose a diary moment first.");var entry=activity.State.Diary[selected];LastExportPath=JourneyPostcard.Export("A MOMENT IN THE GALAXY",entry.Text,entry.Utc.ToString("u")+" · "+Provenance());message="1920 × 1080 postcard saved in Local Creations.";}
            catch(Exception error){message="Postcard was not saved: "+error.Message;}Refresh();
        }
        private Image Image(string name){var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(panel.transform,false);return go.GetComponent<Image>();}
        private static void Place(RectTransform rect,float x,float y,float w,float h){rect.anchorMin=rect.anchorMax=Vector2.zero;rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(x,y);rect.sizeDelta=new Vector2(w,h);}
        private static void Stretch(RectTransform rect){rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=new Vector2(10,5);rect.offsetMax=new Vector2(-10,-5);}
        private Text Label(string value,int size,float x,float y,float w,float h){var text=ConstellationDiagram.MakeText(panel.transform,value,size);text.supportRichText=false;Place(text.rectTransform,x,y,w,h);return text;}
        private void Add(string value,float x,float y,UnityEngine.Events.UnityAction action){var image=Image(value);Place(image.rectTransform,x,y,390,50);image.color=new Color(.14f,.23f,.28f);var button=image.gameObject.AddComponent<Button>();button.onClick.AddListener(action);buttons.Add(button);var label=ConstellationDiagram.MakeText(image.transform,value,16);label.alignment=TextAnchor.MiddleCenter;Stretch(label.rectTransform);}
        private void Update(){if(IsOpen&&(Keyboard.current?.escapeKey.wasPressedThisFrame==true||Gamepad.current?.buttonEast.wasPressedThisFrame==true))Close();}
        public void Close(){if(!IsOpen)return;panel.SetActive(false);Destroy(panel);panel=null;buttons.Clear();var callback=closed;closed=null;if(callback!=null)callback();else hud.SetModalInputCaptured(false);}
        private void OnDestroy(){Close();}
    }
}
