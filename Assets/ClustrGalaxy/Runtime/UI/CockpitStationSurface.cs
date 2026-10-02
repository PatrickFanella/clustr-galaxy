using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace ClustrGalaxy.UI
{
    /// <summary>Shared projected cockpit surface. Content, persistence and dioramas stay in their feature adapters.</summary>
    public sealed class CockpitStationSurface : MonoBehaviour
    {
        private ProbeHud hud;
        private long lease;
        private GameObject previousFocus;
        public static void Mount(GameObject panel, Component owner, bool pause = true)
        {
            var surface = panel.AddComponent<CockpitStationSurface>();
            surface.hud = owner.GetComponent<ProbeHud>();
            surface.previousFocus = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (pause) surface.lease = surface.hud.AcquireStation();
            var camera = owner.GetComponentInChildren<Camera>();
            var canvas = panel.GetComponent<Canvas>();
            panel.transform.SetParent(camera.transform, false);
            canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera;
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(.5f,.5f); rect.pivot = new Vector2(.5f,.5f);
            rect.sizeDelta = new Vector2(1280,720);
            const float distance = .58f;
            panel.transform.localPosition = new Vector3(0,0,distance);
            panel.transform.localRotation = Quaternion.Euler(0,-2.5f,0);
            panel.transform.localScale = Vector3.one * (2 * distance * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f) * .89f / 720);
            var scaler = panel.GetComponent<CanvasScaler>();
            if (scaler != null) scaler.dynamicPixelsPerUnit = 1;
        }
        private void Start()
        {
            // Content is created synchronously by Open after Mount. Theme it once.
            foreach (var item in GetComponentsInChildren<Transform>(true)) item.gameObject.layer = 5;
            foreach (var button in GetComponentsInChildren<Button>(true)) ConsoleTheme.Style(button);
            foreach (var image in GetComponentsInChildren<Image>(true))
                if (image.name == "Backdrop") image.color = new Color(.012f,.025f,.034f,.94f);
            Rule("Port station rail", 0, new Color(.35f,.79f,.84f,.85f));
            Rule("Starboard station rail", 1278, new Color(.96f,.65f,.3f,.8f));
        }
        private void Rule(string title, float x, Color color)
        {
            var go = new GameObject(title, typeof(RectTransform), typeof(Image)); go.transform.SetParent(transform,false); go.layer = 5;
            var rect = go.GetComponent<RectTransform>();rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;rect.anchoredPosition = new Vector2(x,12);rect.sizeDelta = new Vector2(2,696);
            var image = go.GetComponent<Image>();image.color = color;image.raycastTarget = false;
        }
        private void OnDestroy()
        {
            if (hud == null) return;
            if (lease != 0) hud.ReleaseStation(lease);
            if (previousFocus != null && previousFocus.activeInHierarchy && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(previousFocus);
        }
    }
}
