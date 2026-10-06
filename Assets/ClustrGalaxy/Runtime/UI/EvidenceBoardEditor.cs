using System;
using System.Collections.Generic;
using System.Linq;
using ClustrGalaxy.Core.Activities;
using ClustrGalaxy.Core.Domain;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
namespace ClustrGalaxy.UI
{
    public sealed class EvidenceBoardEditor : MonoBehaviour
    {
        private GameObject panel; private ActivitySession activity; private ActivityDefinition definition;
        private ConstellationDesign draft; private ConstellationDiagram diagram; private GalaxyEntityId selected, linkFrom;
        private Text status, observation; private InputField explanation; private Action closed;
        private StudioNavigationScope navigation; private readonly List<Button> buttons = new();
        public bool IsOpen => panel != null;
        public ConstellationDesign Draft => draft;
        public void Open(ActivitySession session, ActivityDefinition content, Action onClose)
        {
            if (IsOpen) return;
            activity=session; definition=content; closed=onClose;
            draft=activity.State.EvidenceBoards.FirstOrDefault(x=>x.ActivityId==definition.Id)?.Layout ?? EvidenceBoard.Default(definition).Layout;
            selected=draft.Nodes[0].Id; buttons.Clear();
            panel=new GameObject("Mystery evidence board",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));panel.transform.SetParent(transform,false);
            CockpitStationSurface.Mount(panel, this);panel.GetComponent<Canvas>().sortingOrder=15;
            var scaler=panel.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=1;
            var bg=Image("Background");bg.color=new Color(.025f,.045f,.065f);bg.rectTransform.anchorMin=Vector2.zero;bg.rectTransform.anchorMax=Vector2.one;bg.rectTransform.offsetMin=bg.rectTransform.offsetMax=Vector2.zero;
            Label("EVIDENCE BOARD · "+definition.Title,24,30,695,1220,40);
            status=Label("",16,30,650,850,50);observation=Label("",17,30,160,850,90);
            var go=new GameObject("Observation cards",typeof(RectTransform),typeof(ConstellationDiagram));go.transform.SetParent(panel.transform,false);Place(go.GetComponent<RectTransform>(),30,590,850,415);
            Canvas.ForceUpdateCanvases();diagram=go.GetComponent<ConstellationDiagram>();diagram.Initialize(draft,true);
            diagram.Selected+=Select;diagram.Moved+=(id,x,y)=>{selected=id;Set(draft.Move(id,x,y));};
            Add("LINK / UNLINK SELECTED",()=>{linkFrom=selected;status.text="Select another card to draw or remove your connection.";});
            Add("CONNECT TO NEXT CARD",()=>{var next=draft.Nodes[(Index()+1)%5].Id;Set(draft.Connect(selected,next));});
            Add("SAVE BOARD DRAFT",()=>Save(false,0));
            for(var i=0;i<3;i++){var choice=i;Add(definition.Interpretations[i],()=>Save(true,choice));}
            Add("CLOSE / DISCARD DRAFT",Close);
            var field=Image("Optional explanation");Place(field.rectTransform,30,60,850,40);field.color=new Color(.12f,.2f,.25f);
            explanation=field.gameObject.AddComponent<InputField>();explanation.characterLimit=500;
            var text=ConstellationDiagram.MakeText(field.transform,"",16);Fill(text.rectTransform);explanation.textComponent=text;explanation.text=activity.State.Find(definition.Id)?.Explanation??"";
            var placeholder=ConstellationDiagram.MakeText(field.transform,"Optional written interpretation (keyboard text)",16);Fill(placeholder.rectTransform);explanation.placeholder=placeholder;
            Label("D-pad: focus · LB/RB: card · Right stick/WASD: move · Mouse: drag\nConnections are your interpretation of authored demo fiction.",14,915,155,340,120);
            var module=EventSystem.current.GetComponent<InputSystemUIInputModule>();navigation=new StudioNavigationScope(module);
            diagram.ConfigureNavigation(buttons);Refresh();EventSystem.current.SetSelectedGameObject(buttons[0].gameObject);
        }
        private int Index(){for(var i=0;i<5;i++)if(draft.Nodes[i].Id==selected)return i;return 0;}
        private void Select(GalaxyEntityId id){selected=id;if(!linkFrom.IsEmpty&&linkFrom!=id){Set(draft.Connect(linkFrom,id));linkFrom=default;}Refresh();}
        private void Set(ConstellationDesign value){draft=value;diagram.Display(draft);Refresh();}
        private void Refresh()
        {
            var clue=CampaignContent.Find(definition.Id,selected.Value);
            observation.text="CARD "+(Index()+1)+" · "+clue.Title+"\n"+clue.Text;
            status.text="Five discovered observations · "+draft.Edges.Count+" personal connections\nArrange the cards and connect at least one pair before submitting. Save explicitly.";
        }
        private void Save(bool submit,int choice)
        {
            var result=activity.Apply(new ActivityIntent(Guid.NewGuid().ToString("N"),activity.State.Identity,
                submit?ActivityOperation.Interpret:ActivityOperation.SaveEvidenceBoard,DateTime.UtcNow,definition.Id,
                submit?definition.Interpretations[choice]:"",explanation.text,evidenceBoard:new EvidenceBoard(definition.Id,draft)));
            status.text=result.Committed?(submit?"Interpretation and board saved locally.":"Board draft saved locally."):result.Error;
            GetComponent<ProbeHud>().InvalidateExploration();
        }
        private void Update()
        {
            if(!IsOpen)return;
            if(Keyboard.current?.escapeKey.wasPressedThisFrame==true||Gamepad.current?.buttonEast.wasPressedThisFrame==true){Close();return;}
            if(explanation.isFocused)return;
            var focused=diagram.FocusedMember();if(!focused.IsEmpty&&focused!=selected){selected=focused;Refresh();}
            var step=Gamepad.current?.rightShoulder.wasPressedThisFrame==true?1:Gamepad.current?.leftShoulder.wasPressedThisFrame==true?-1:0;
            if(step!=0){selected=draft.Nodes[(Index()+step+5)%5].Id;diagram.FocusMember(selected);Refresh();}
            var delta=Gamepad.current?.rightStick.ReadValue()??Vector2.zero;
            if(Keyboard.current!=null){if(Keyboard.current.aKey.isPressed)delta.x-=1;if(Keyboard.current.dKey.isPressed)delta.x+=1;if(Keyboard.current.wKey.isPressed)delta.y+=1;if(Keyboard.current.sKey.isPressed)delta.y-=1;}
            if(delta.sqrMagnitude>.04f){var node=draft.Nodes[Index()];delta*=Time.unscaledDeltaTime*.7f;Set(draft.Move(selected,Mathf.Clamp(node.X+delta.x,-1,1),Mathf.Clamp(node.Y+delta.y,-1,1)));}
        }
        private Image Image(string name){var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(panel.transform,false);return go.GetComponent<Image>();}
        private Text Label(string value,int size,float x,float y,float w,float h){var t=ConstellationDiagram.MakeText(panel.transform,value,size);Place(t.rectTransform,x,y,w,h);return t;}
        private static void Place(RectTransform r,float x,float y,float w,float h){r.anchorMin=r.anchorMax=Vector2.zero;r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);}
        private static void Fill(RectTransform r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=new Vector2(8,4);r.offsetMax=new Vector2(-8,-4);}
        private void Add(string caption,UnityEngine.Events.UnityAction action){var image=Image(caption);Place(image.rectTransform,915,590-buttons.Count*59,335,48);image.color=new Color(.14f,.23f,.28f);var b=image.gameObject.AddComponent<Button>();b.onClick.AddListener(action);buttons.Add(b);var t=ConstellationDiagram.MakeText(image.transform,caption,15);Fill(t.rectTransform);t.alignment=TextAnchor.MiddleCenter;}
        public void Close(){if(!IsOpen)return;navigation?.Dispose();navigation=null;panel.SetActive(false);Destroy(panel);panel=null;var callback=closed;closed=null;callback?.Invoke();}
        private void OnDestroy(){Close();}
    }
}
