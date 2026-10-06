using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using ClustrGalaxy.Flight;
namespace ClustrGalaxy.UI
{
    public sealed class CockpitChartStation : MonoBehaviour
    {
        private GameObject panel;
        private Camera cockpitCamera;
        public Camera ChartCamera { get; private set; }
        private RenderTexture texture;
        private RectTransform viewport, cursor;
        private Text caption;
        private GalaxyNavigationController navigation;
        public void Initialize(Camera pilotCamera, GalaxyNavigationController nav)
        { cockpitCamera = pilotCamera; navigation = nav; }
        public void SetVisible(bool visible)
        {
            if (visible && panel == null) Build();
            if (panel != null) panel.SetActive(visible);
            if (ChartCamera != null) ChartCamera.enabled = visible;
            if (visible) EventSystem.current?.SetSelectedGameObject(null);
        }
        private void Build()
        {
            panel = new GameObject("Cockpit navigation chart",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            CockpitStationSurface.Mount(panel,this,false);
            var background = Image(panel.transform,"Backdrop"); Stretch(background.rectTransform);background.color = new Color(.012f,.025f,.034f,.96f);
            Label("N A V I G A T I O N  /  LOCAL CHART",40,680,1000,36,24);
            caption = Label("",40,86,1160,28,15);
            Label("LS / arrows: point   A / Enter: inspect   B: previous scope   M / Select: close\nLOCAL FLIGHT CONTINUES · Chart inspection does not move the ship",40,51,1160,45,13);
            var feed = new GameObject("Chart optical viewport",typeof(RectTransform),typeof(RawImage),typeof(EventTrigger));feed.transform.SetParent(panel.transform,false);
            viewport = feed.GetComponent<RectTransform>(); Place(viewport,40,635,1200,525);
            texture = new RenderTexture(1920,840,24) {name="Cockpit chart feed"};texture.Create();feed.GetComponent<RawImage>().texture=texture;
            var optical = new GameObject("Chart optical camera",typeof(Camera)); optical.transform.SetParent(transform,false);
            ChartCamera = optical.GetComponent<Camera>();ChartCamera.CopyFrom(cockpitCamera);ChartCamera.targetTexture=texture;
            ChartCamera.cullingMask &= ~(1<<5 | 1<<31);ChartCamera.depth = -10;
            var click = new EventTrigger.Entry{eventID=EventTriggerType.PointerClick};
            click.callback.AddListener(_=>navigation.ConfirmChartSelection());feed.GetComponent<EventTrigger>().triggers.Add(click);
            var marker = Image(viewport,"Chart pointer");marker.color = new Color(1,.72f,.35f,.9f);marker.raycastTarget=false;
            cursor=marker.rectTransform;cursor.sizeDelta=new Vector2(7,7);cursor.anchorMin=cursor.anchorMax=Vector2.zero;
            var close=Image(panel.transform,"Return to flight");Place(close.rectTransform,1050,689,190,34);close.color=new Color(.075f,.12f,.16f);
            var button=close.gameObject.AddComponent<Button>();button.onClick.AddListener(()=>navigation.SetMapOpen(false));ConsoleTheme.Style(button);
            var text=NewText(close.transform,"RETURN TO FLIGHT",14);Stretch(text.rectTransform);text.alignment=TextAnchor.MiddleCenter;
        }
        public bool TryPointer(Vector2 screen, out Vector2 point)
        {
            point=default;
            if(viewport==null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport,screen,cockpitCamera,out var local))return false;
            var bounds=viewport.rect;point=new Vector2((local.x-bounds.xMin)/bounds.width,(local.y-bounds.yMin)/bounds.height);
            return point.x>=0&&point.x<=1&&point.y>=0&&point.y<=1;
        }
        private void LateUpdate()
        {
            if(panel==null || !panel.activeSelf)return;
            cursor.anchoredPosition = Vector2.Scale(navigation.MapCursorPosition,viewport.rect.size);
            var text=navigation.MapHoverStatus;if(caption.text!=text)caption.text=text;
        }
        private static void Place(RectTransform rect,float x,float y,float width,float height)
        {rect.anchorMin=rect.anchorMax=Vector2.zero;rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(x,y);rect.sizeDelta=new Vector2(width,height);}
        private static void Stretch(RectTransform rect)
        {rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;}
        private static Image Image(Transform parent,string name)
        {var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);return go.GetComponent<Image>();}
        private static Text NewText(Transform parent,string value,int size)
        {var go=new GameObject(value,typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);var text=go.GetComponent<Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=size;text.text=value;text.color=new Color(.75f,.9f,.92f);text.raycastTarget=false;return text;}
        private Text Label(string value,float x,float y,float w,float h,int size)
        {var text=NewText(panel.transform,value,size);Place(text.rectTransform,x,y,w,h);return text;}
        private void OnDestroy()
        {if(panel!=null)Destroy(panel);if(ChartCamera!=null)Destroy(ChartCamera.gameObject);if(texture!=null){texture.Release();Destroy(texture);}}
    }
}
