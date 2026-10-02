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
    public sealed class CampaignNotebook:MonoBehaviour
    {
        private GameObject panel;private ProbeHud hud;private ActivitySession activity;
        private Text title,body,status;private InputField explanation;private int page;
        private readonly List<Button> buttons=new();
        private EvidenceBoardEditor board; private JourneyDiary diary;private string noteIntent;private int resumedFrame=-1;
        public bool IsOpen=>panel!=null;
        public void Open(ActivitySession model=null)
        {
            if(IsOpen)return;activity=model??GetComponent<GalaxySessionBehaviour>().Session.Activities;hud=GetComponent<ProbeHud>();
            panel=new GameObject("Campaign notebook",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));panel.transform.SetParent(transform,false);
            CockpitStationSurface.Mount(panel, this);panel.GetComponent<Canvas>().sortingOrder=12;
            var scale=panel.GetComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1280,720);scale.matchWidthOrHeight=1;
            var background=NewImage("Background");background.color=new Color(.025f,.045f,.065f);background.rectTransform.anchorMin=Vector2.zero;background.rectTransform.anchorMax=Vector2.one;background.rectTransform.offsetMin=background.rectTransform.offsetMax=Vector2.zero;
            title=Label("",28,30,685,1220,50);body=Label("",18,30,625,1220,360);status=Label("",16,30,195,1220,45);
            var field=NewImage("Optional written explanation");Place(field.rectTransform,30,255,1220,45);field.color=new Color(.12f,.2f,.25f);
            explanation=field.gameObject.AddComponent<InputField>();explanation.characterLimit=500;explanation.onValueChanged.AddListener(_=>noteIntent=null);
            var text=ConstellationDiagram.MakeText(field.transform,"",18);text.rectTransform.anchorMin=Vector2.zero;text.rectTransform.anchorMax=Vector2.one;text.rectTransform.offsetMin=new Vector2(10,5);text.rectTransform.offsetMax=new Vector2(-10,-5);explanation.textComponent=text;
            var placeholder=ConstellationDiagram.MakeText(field.transform,"Optional explanation or personal notebook entry (keyboard text)",16);placeholder.rectTransform.anchorMin=Vector2.zero;placeholder.rectTransform.anchorMax=Vector2.one;placeholder.rectTransform.offsetMin=new Vector2(10,5);placeholder.rectTransform.offsetMax=new Vector2(-10,-5);explanation.placeholder=placeholder;
            buttons.Clear();Add("PREVIOUS",30,75,()=>MovePage(-1));Add("NEXT",340,75,()=>MovePage(1));Add("CLOSE NOTEBOOK",650,75,Close);
            for(var i=0;i<3;i++){var choice=i;Add("",30+i*410,140,()=>Interpret(choice),390);}
            Add("OPEN EVIDENCE BOARD",30,140,OpenBoard,800);
            for(var i=0;i<buttons.Count;i++)buttons[i].navigation=new Navigation{mode=Navigation.Mode.Automatic};
            hud.SetModalInputCaptured(true);page=activity.State.Identity.Snapshot.StorageNamespace=="demo"?0:Math.Max(0,activity.State.Diary.Count-1);Refresh();EventSystem.current.SetSelectedGameObject(buttons[0].gameObject);
        }
        private void Refresh()
        {
            var demo=activity.State.Identity.Snapshot.StorageNamespace=="demo";
            for(var i=3;i<7;i++)buttons[i].gameObject.SetActive(false);
            if(!demo)
            {
                var notes=activity.State.Diary;page=Math.Clamp(page,0,Math.Max(0,notes.Count-1));
                title.text="PERSONAL INVESTIGATION NOTEBOOK";
                body.supportRichText=false;body.text="Snapshot "+activity.State.Identity.Snapshot+" · "+activity.State.Identity.Mode+"\nPersonal observations; no generated factual conclusions.\n\n"+
                    (notes.Count==0?"Write a personal observation below. Saving creates a permanent local bookmark.":"NOTE "+(page+1)+" / "+notes.Count+" · "+notes[page].Utc.ToString("u")+"\n\n"+notes[page].Text);
                buttons[3].gameObject.SetActive(true);buttons[3].GetComponentInChildren<Text>().text="SAVE PERSONAL NOTE";
                buttons[4].gameObject.SetActive(true);buttons[4].GetComponentInChildren<Text>().text="BROWSE SAVED NOTES";
                status.text="A/Enter: select · B/Escape: return · Saved notes persist until explicitly removed in the diary.";LinkFocus();return;
            }
            var definition=AuthoredActivityCatalog.Definitions[page];var record=activity.State.Find(definition.Id);
            title.text=definition.Title+" · "+(page+1)+" / 11";
            var text="Authored demo fiction · "+(record?.Observations.Count??0)+" / "+definition.Observations.Count+" observations\n\n";
            if(record==null)text+="Not yet discovered. Look toward identified nearby bodies for three seconds while exploring. No quest acceptance is required.";
            else foreach(var id in record.Observations){var clue=CampaignContent.Find(definition.Id,id);text+=clue.Title+" — "+clue.Text+"\n\n";}
            if(record!=null&&definition.Kind==ActivityKind.Trail)
                text+=record.Completed?"Keepsake: "+CampaignContent.Keepsake(definition.Id):CampaignContent.Find(definition.Id,record.Observations.Last()).Hint;
            if(record?.Completed==true&&definition.Kind==ActivityKind.Mystery)text+="Your interpretation: "+record.Interpretation+"\n"+record.Explanation;
            body.text=text;
            if(definition.Kind==ActivityKind.Mystery&&record?.Observations.Count==5)buttons[6].gameObject.SetActive(true);
            LinkFocus();
            status.text="A/Enter: confirm · B/Escape: return · Text entry is optional · Interpretations have no hidden correct answer";
        }
        private void MovePage(int direction)
        {
            var count=activity.State.Identity.Snapshot.StorageNamespace=="demo"?11:activity.State.Diary.Count;
            if(count>0)page=(page+direction+count)%count;Refresh();
        }
        private void OpenDiary()
        {
            panel.SetActive(false);diary=this.GetOrAddComponent<JourneyDiary>();
            diary.Open(()=>{if(panel==null)return;panel.SetActive(true);resumedFrame=Time.frameCount;Refresh();EventSystem.current.SetSelectedGameObject(buttons[4].gameObject);},activity);
        }
        private void LinkFocus()
        {
            var active=buttons.Where(x=>x.gameObject.activeSelf).ToArray();
            for(var i=0;i<active.Length;i++)active[i].navigation=new Navigation{mode=Navigation.Mode.Explicit,
                selectOnDown=active[(i+1)%active.Length],selectOnRight=active[(i+1)%active.Length],
                selectOnUp=active[(i+active.Length-1)%active.Length],selectOnLeft=active[(i+active.Length-1)%active.Length]};
            if(EventSystem.current.currentSelectedGameObject==null||!EventSystem.current.currentSelectedGameObject.activeInHierarchy)
                EventSystem.current.SetSelectedGameObject(active[0].gameObject);
        }
        private void OpenBoard()
        {
            panel.SetActive(false);board=this.GetOrAddComponent<EvidenceBoardEditor>();
            board.Open(activity,AuthoredActivityCatalog.Definitions[page],()=>{if(panel==null)return;panel.SetActive(true);resumedFrame=Time.frameCount;Refresh();EventSystem.current.SetSelectedGameObject(buttons[6].gameObject);});
        }
        private void Interpret(int choice)
        {
            var demo=activity.State.Identity.Snapshot.StorageNamespace=="demo";var definition=demo?AuthoredActivityCatalog.Definitions[page]:null;
            if(!demo&&choice==1){OpenDiary();return;}
            if(!demo&&string.IsNullOrWhiteSpace(explanation.text)){status.text="Write a personal observation before saving.";return;}
            noteIntent??="bookmark:"+Guid.NewGuid().ToString("N");
            var result=activity.Apply(new ActivityIntent(noteIntent,activity.State.Identity,demo?ActivityOperation.Interpret:ActivityOperation.SaveBookmark,DateTime.UtcNow,
                demo?definition.Id:"",demo?definition.Interpretations[choice]:explanation.text,explanation:demo?explanation.text:""));
            if(result.Committed&&!demo){explanation.text="";noteIntent=null;page=activity.State.Diary.Count-1;}
            Refresh();status.text=result.Committed?(demo?"Interpretation saved locally.":"Personal note saved locally; source data is unchanged."):result.Error;hud.InvalidateExploration();
        }
        private Image NewImage(string name){var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(panel.transform,false);return go.GetComponent<Image>();}
        private static void Place(RectTransform rect,float x,float y,float width,float height){rect.anchorMin=rect.anchorMax=Vector2.zero;rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(x,y);rect.sizeDelta=new Vector2(width,height);}
        private Text Label(string value,int size,float x,float y,float width,float height){var label=ConstellationDiagram.MakeText(panel.transform,value,size);Place(label.rectTransform,x,y,width,height);return label;}
        private void Add(string value,float x,float y,UnityEngine.Events.UnityAction action,float width=290)
        {var image=NewImage(value);Place(image.rectTransform,x,y,width,50);image.color=new Color(.14f,.23f,.28f);var button=image.gameObject.AddComponent<Button>();button.onClick.AddListener(action);buttons.Add(button);var label=ConstellationDiagram.MakeText(image.transform,value,16);label.alignment=TextAnchor.MiddleCenter;label.rectTransform.anchorMin=Vector2.zero;label.rectTransform.anchorMax=Vector2.one;label.rectTransform.offsetMin=label.rectTransform.offsetMax=Vector2.zero;}
        private void Update(){if(IsOpen&&panel.activeSelf&&Time.frameCount>resumedFrame&&((Keyboard.current?.escapeKey.wasPressedThisFrame==true&&!(explanation!=null&&explanation.isFocused))||Gamepad.current?.buttonEast.wasPressedThisFrame==true))Close();}
        public void Close(){if(!IsOpen)return;board?.Close();diary?.Close();panel.SetActive(false);Destroy(panel);panel=null;if(hud!=null)hud.SetModalInputCaptured(false);}
        private void OnDestroy(){Close();}
    }
}
