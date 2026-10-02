using System;
using ClustrGalaxy.Core.Activities;
using ClustrGalaxy.Core.Domain;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ClustrGalaxy.UI
{
    public sealed class ConstellationDiagram : MonoBehaviour
    {
        private static readonly Color[] Colors = { new(.43f,.86f,.84f), new(.96f,.77f,.42f), new(.72f,.86f,.56f),
            new(.76f,.69f,.96f), new(.95f,.71f,.63f), new(.85f,.90f,.93f) };
        public static Color PaletteColor(int index)=>Colors[index];
        private readonly RectTransform[] lines = new RectTransform[66];
        private Button[] buttons;
        private RectTransform area;
        private ConstellationDesign design;
        public Action<GalaxyEntityId> Selected = delegate { };
        public Action<GalaxyEntityId, float, float> Moved = delegate { };
        public void Initialize(ConstellationDesign value, bool interactive)
        {
            area = (RectTransform)transform; design = value;
            for (var i = 0; i < lines.Length; i++)
            {
                var line = new GameObject("Personal connection", typeof(RectTransform), typeof(Image)); line.transform.SetParent(area,false);
                lines[i] = line.GetComponent<RectTransform>(); line.GetComponent<Image>().raycastTarget = false;
            }
            buttons = new Button[value.Nodes.Count];
            for (var i = 0; i < buttons.Length; i++)
            {
                var node = value.Nodes[i];
                var go = new GameObject("Member " + (i + 1), typeof(RectTransform), typeof(Image), typeof(Button)); go.transform.SetParent(area,false);
                go.GetComponent<RectTransform>().sizeDelta = new Vector2(40,40);
                buttons[i] = go.GetComponent<Button>(); buttons[i].interactable = interactive;
                var colors = buttons[i].colors; colors.disabledColor = Color.white; buttons[i].colors = colors;
                var text = MakeText(go.transform, (i+1).ToString(), 19); text.color = new Color(.04f,.08f,.1f); text.alignment = TextAnchor.MiddleCenter;
                text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one; text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
                if (interactive)
                {
                    var id = node.Id; buttons[i].onClick.AddListener(() => Selected(id));
                    go.AddComponent<DiagramDrag>().Initialize(area, (x,y) => { Selected(id); Moved(id,x,y); });
                }
            }
            Display(value);
        }
        public static Text MakeText(Transform parent, string value, int size)
        {
            var button=parent.GetComponent<Button>();if(button!=null)ConsoleTheme.Style(button);
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent,false);
            var text = go.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value; text.fontSize = size; text.color = new Color(.88f,.95f,.96f); text.raycastTarget = false;
            return text;
        }
        private Vector2 Position(DesignNode node) => new(node.X * Mathf.Max(1, area.rect.width * .5f - 35), node.Y * Mathf.Max(1, area.rect.height * .5f - 35));
        public void Display(ConstellationDesign value)
        {
            design = value;
            for (var i = 0; i < buttons.Length; i++)
            { ((RectTransform)buttons[i].transform).anchoredPosition = Position(value.Nodes[i]); buttons[i].GetComponent<Image>().color = Colors[value.Palette]; }
            for (var i = 0; i < lines.Length; i++)
            {
                lines[i].gameObject.SetActive(i < value.Edges.Count);
                if (i >= value.Edges.Count) continue;
                var edge = value.Edges[i]; var a = Vector2.zero; var b = Vector2.zero;
                foreach (var node in value.Nodes) { if (node.Id == edge.First) a = Position(node); if (node.Id == edge.Second) b = Position(node); }
                var delta = b - a; lines[i].anchoredPosition = (a+b)*.5f; lines[i].sizeDelta = new Vector2(delta.magnitude,3);
                lines[i].localRotation = Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);
                lines[i].GetComponent<Image>().color = Colors[value.Palette];
            }
        }
        public GalaxyEntityId FocusedMember()
        {
            var selected = EventSystem.current?.currentSelectedGameObject;
            for (var i = 0; i < buttons.Length; i++) if (buttons[i].gameObject == selected) return design.Nodes[i].Id;
            return default;
        }
        public void ConfigureNavigation(System.Collections.Generic.IReadOnlyList<Button> actions)
        {
            for (var i = 0; i < buttons.Length; i++)
                buttons[i].navigation = new Navigation { mode = Navigation.Mode.Explicit,
                    selectOnUp = buttons[(i+buttons.Length-1)%buttons.Length], selectOnDown = buttons[(i+1)%buttons.Length],
                    selectOnLeft = actions[0], selectOnRight = actions[0] };
            for (var i = 0; i < actions.Count; i++)
                actions[i].navigation = new Navigation { mode = Navigation.Mode.Explicit,
                    selectOnUp = actions[(i+actions.Count-1)%actions.Count], selectOnDown = actions[(i+1)%actions.Count],
                    selectOnLeft = buttons[0], selectOnRight = buttons[0] };
        }
        public void FocusMember(GalaxyEntityId id)
        { for (var i = 0; i < buttons.Length; i++) if (design.Nodes[i].Id == id) EventSystem.current?.SetSelectedGameObject(buttons[i].gameObject); }
        public void FocusFirst() { if (buttons.Length > 0) EventSystem.current?.SetSelectedGameObject(buttons[0].gameObject); }
    }
    public sealed class DiagramDrag : MonoBehaviour, IDragHandler
    {
        private RectTransform area;
        private Action<float,float> move;
        public void Initialize(RectTransform owner, Action<float,float> callback) { area = owner; move = callback; }
        public void OnDrag(PointerEventData data)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(area,data.position,data.pressEventCamera,out var local))
            {
                local -= area.rect.center;
                move(Mathf.Clamp(local.x / Mathf.Max(1, area.rect.width*.5f-35),-1,1), Mathf.Clamp(local.y / Mathf.Max(1, area.rect.height*.5f-35),-1,1));
            }
        }
    }
}
