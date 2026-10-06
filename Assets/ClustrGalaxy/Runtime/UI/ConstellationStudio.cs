using System;
using System.Linq;
using ClustrGalaxy.Bootstrap;
using ClustrGalaxy.Core.Activities;
using ClustrGalaxy.Core.Domain;
using ClustrGalaxy.Core.Survey;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.EventSystems;

namespace ClustrGalaxy.UI
{
    public sealed class ConstellationStudio : MonoBehaviour
    {
        private ProbeHud hud;
        private GalaxySessionBehaviour owner;
        private PersonalConstellationRecord personal;
        private ConstellationDesign draft;
        private ConstellationDiagram diagram;
        private GameObject panel;
        private Text status;
        private GalaxyEntityId selected, linkFrom;
        private bool changed;
        private StudioNavigationScope navigationScope;
        private readonly System.Collections.Generic.List<Button> actionButtons = new();
        public ConstellationDesign Draft => draft;
        public string LastExportPath { get; private set; }
        public void Open(PersonalConstellationRecord record)
        {
            if (panel != null) return;
            owner = GetComponent<GalaxySessionBehaviour>(); hud = GetComponent<ProbeHud>(); personal = record;
            var stored = owner.Session.Activities.State.Designs.FirstOrDefault(x => x.PersonalId == record.Id);
            draft = stored != null && stored.Nodes.Select(n => n.Id).SequenceEqual(record.Members) ? stored : ConstellationDesign.Default(record.Id,record.Members);
            actionButtons.Clear();
            selected = draft.Nodes[0].Id; changed = false; linkFrom = default;
            hud.SetModalInputCaptured(true);
            navigationScope = new StudioNavigationScope(EventSystem.current.GetComponent<InputSystemUIInputModule>());
            panel = new GameObject("Constellation studio",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster)); panel.transform.SetParent(transform,false);
            CockpitStationSurface.Mount(panel, this); panel.GetComponent<Canvas>().sortingOrder = 10;
            var scaler = panel.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280,720); scaler.matchWidthOrHeight = 1;
            var backdrop = new GameObject("Backdrop",typeof(RectTransform),typeof(Image)); backdrop.transform.SetParent(panel.transform,false);
            var background = backdrop.GetComponent<RectTransform>(); background.anchorMin = Vector2.zero; background.anchorMax = Vector2.one; background.offsetMin = background.offsetMax = Vector2.zero;
            backdrop.GetComponent<Image>().color = new Color(.025f,.045f,.065f,.99f);
            var title = ConstellationDiagram.MakeText(panel.transform,"CONSTELLATION STUDIO · " + record.Name,24); Place(title.rectTransform,30,680,1220,45);
            status = ConstellationDiagram.MakeText(panel.transform,"",16); Place(status.rectTransform,30,625,880,70);
            var go = new GameObject("Diagram",typeof(RectTransform),typeof(ConstellationDiagram)); go.transform.SetParent(panel.transform,false);
            var area = go.GetComponent<RectTransform>(); Place(area,30,550,900,490);
            Canvas.ForceUpdateCanvases(); diagram = go.GetComponent<ConstellationDiagram>(); diagram.Initialize(draft,true);
            diagram.Selected += Select; diagram.Moved += Move;
            AddButton("SAVE DESIGN",0,Save);
            AddButton("LINK / UNLINK SELECTED",1,() => { linkFrom = selected; RefreshStatus("Choose the other member to toggle a personal connection."); });
            AddButton("NEXT COLOR PALETTE",2,() => SetDraft(draft.Appearance((draft.Palette+1)%6,draft.MusicPreset)));
            AddButton("NEXT MUSIC PRESET",3,() => SetDraft(draft.Appearance(draft.Palette,(draft.MusicPreset+1)%3)));
            AddButton("PLAY CONSTELLATION",4,() => GetComponent<ExplorationFeedback>()?.PlayConstellation(personal.Members, draft.MusicPreset));
            AddButton("EXPORT SAVED POSTCARD",5,Export);
            AddButton("CLOSE / DISCARD DRAFT",6,Close);
            var help = ConstellationDiagram.MakeText(panel.transform,"D-pad: focus · A/Enter: select · LB/RB: member · Right stick / WASD: move\nMouse: drag · Personal links are your interpretation. Save explicitly.",15);
            Place(help.rectTransform,30,55,1220,50); RefreshStatus(); diagram.ConfigureNavigation(actionButtons); diagram.FocusFirst();
        }
        private static void Place(RectTransform rect,float x,float y,float width,float height)
        { rect.anchorMin = rect.anchorMax = Vector2.zero; rect.pivot = new Vector2(0,1); rect.anchoredPosition = new Vector2(x,y); rect.sizeDelta = new Vector2(width,height); }
        private void AddButton(string label,int index,UnityEngine.Events.UnityAction action)
        {
            var go = new GameObject(label,typeof(RectTransform),typeof(Image),typeof(Button)); go.transform.SetParent(panel.transform,false);
            Place(go.GetComponent<RectTransform>(),955,550-index*59,290,48); go.GetComponent<Image>().color = new Color(.14f,.23f,.28f);
            var button = go.GetComponent<Button>(); actionButtons.Add(button); button.onClick.AddListener(action);
            var text = ConstellationDiagram.MakeText(go.transform,label,15); text.alignment = TextAnchor.MiddleCenter;
            text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one; text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
        }
        private void Select(GalaxyEntityId id)
        {
            selected = id;
            if (!linkFrom.IsEmpty && linkFrom != id) { SetDraft(draft.Connect(linkFrom,id)); linkFrom = default; }
            RefreshStatus();
        }
        private void Move(GalaxyEntityId id,float x,float y) { selected = id; SetDraft(draft.Move(id,x,y)); }
        private void SetDraft(ConstellationDesign value) { draft = value; changed = true; diagram.Display(draft); RefreshStatus(); }
        private void RefreshStatus(string message = "")
        {
            if (status == null) return;
            status.text = (changed ? "UNSAVED DRAFT" : "SAVED / DEFAULT LAYOUT") + " · Palette " + (draft.Palette+1) + " · Instrument " + (draft.MusicPreset+1) +
                "\nSelected: " + selected.Value + (message.Length == 0 ? "" : "\n" + message);
        }
        private void Update()
        {
            if (panel == null) return;
            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true || Gamepad.current?.buttonEast.wasPressedThisFrame == true) { Close(); return; }
            var focused = diagram.FocusedMember(); if (!focused.IsEmpty && focused != selected) { selected = focused; RefreshStatus(); }
            var step = 0;
            if (Gamepad.current?.rightShoulder.wasPressedThisFrame == true || Keyboard.current?.eKey.wasPressedThisFrame == true) step = 1;
            if (Gamepad.current?.leftShoulder.wasPressedThisFrame == true || Keyboard.current?.qKey.wasPressedThisFrame == true) step = -1;
            if (step != 0)
            {
                var index = 0; for (var i = 0; i < draft.Nodes.Count; i++) if (draft.Nodes[i].Id == selected) index = i;
                selected = draft.Nodes[(index+step+draft.Nodes.Count)%draft.Nodes.Count].Id; diagram.FocusMember(selected); RefreshStatus();
            }
            var delta = Gamepad.current?.rightStick.ReadValue() ?? Vector2.zero;
            if (Keyboard.current != null)
            {
                if (Keyboard.current.aKey.isPressed) delta.x -= 1; if (Keyboard.current.dKey.isPressed) delta.x += 1;
                if (Keyboard.current.sKey.isPressed) delta.y -= 1; if (Keyboard.current.wKey.isPressed) delta.y += 1;
            }
            if (delta.sqrMagnitude > .04f)
            {
                var node = draft.Nodes.First(n => n.Id == selected); delta *= Time.unscaledDeltaTime * .7f;
                Move(selected,Mathf.Clamp(node.X+delta.x,-1,1),Mathf.Clamp(node.Y+delta.y,-1,1));
            }
        }
        public void Save()
        {
            var activities = owner.Session.Activities;
            var result = activities.Apply(new ActivityIntent(Guid.NewGuid().ToString("N"),activities.State.Identity,ActivityOperation.SaveDesign,DateTime.UtcNow,design:draft));
            if (result.Committed) changed = false;
            RefreshStatus(result.Committed ? "Design saved locally. Queue membership is unchanged." : result.Error);
        }
        public void Export()
        {
            var saved = owner.Session.Activities.State.Designs.FirstOrDefault(d => d.PersonalId == personal.Id);
            if (changed || saved == null) { RefreshStatus("Save the design before exporting."); return; }
            try
            {
                var snapshot = owner.Session.Snapshots.Manifest.SurveySnapshot;
                LastExportPath = ConstellationPostcard.Export(saved,personal,"Snapshot " + snapshot + " · Personal connections, not source relationships");
                RefreshStatus("Postcard saved to ClustrGalaxy/exports.");
            }
            catch (Exception exception) { RefreshStatus("Could not export. Retry: " + exception.Message); }
        }
        public void Close()
        {
            if (panel == null) return; panel.SetActive(false); Destroy(panel); panel = null;
            navigationScope?.Dispose(); navigationScope = null;
            if (hud != null) hud.SetModalInputCaptured(false);
        }
        private void OnDestroy() { if (panel != null) Close(); }
    }
}
