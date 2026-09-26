using System;
using System.IO;
using ClustrGalaxy.Core.Activities;
using ClustrGalaxy.Core.Survey;
using UnityEngine;
using UnityEngine.UI;

namespace ClustrGalaxy.UI
{
    public static class ConstellationPostcard
    {
        public static string Export(ConstellationDesign design, PersonalConstellationRecord personal, string provenance)
        {
            const int width = 1920, height = 1080;
            var root = new GameObject("Local postcard renderer"); root.transform.position = new Vector3(0,0,-10000);
            var texture = new RenderTexture(width,height,24); Texture2D pixels = null; var previous = RenderTexture.active;
            try
            {
                var camera = root.AddComponent<Camera>(); camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 5;
                camera.cullingMask = 1 << 30; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f,.045f,.065f);
                camera.targetTexture = texture;
                var canvasObject = new GameObject("Postcard",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler)); canvasObject.layer = 30;
                canvasObject.transform.SetParent(root.transform,false); var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
                var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                var diagramObject = new GameObject("Personal interpretation",typeof(RectTransform),typeof(ConstellationDiagram)); diagramObject.transform.SetParent(canvasObject.transform,false);
                var area = diagramObject.GetComponent<RectTransform>(); area.anchorMin = area.anchorMax = new Vector2(0,.5f); area.pivot = new Vector2(0,.5f);
                area.anchoredPosition = new Vector2(50,0); area.sizeDelta = new Vector2(1130,880);
                Canvas.ForceUpdateCanvases(); diagramObject.GetComponent<ConstellationDiagram>().Initialize(design,false);
                AddText(canvasObject.transform, personal.Name, 42, 1220, 970, 630, 120);
                AddText(canvasObject.transform, personal.Reason, 24, 1220, 805, 630, 260);
                var legend = ""; for (var i = 0; i < design.Nodes.Count; i++) legend += (i+1) + ". " + design.Nodes[i].Id.Value + "\n";
                AddText(canvasObject.transform,legend,20,1220,510,630,350);
                AddText(canvasObject.transform,"PERSONAL INTERPRETATION · CLUSTR GALAXY\n" + provenance,20,70,130,1780,95);
                foreach (var child in root.GetComponentsInChildren<Transform>()) child.gameObject.layer = 30;
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = texture;
                pixels = new Texture2D(width,height,TextureFormat.RGB24,false); pixels.ReadPixels(new Rect(0,0,width,height),0,0); pixels.Apply();
                var directory = Path.Combine(Application.persistentDataPath,"ClustrGalaxy","exports"); Directory.CreateDirectory(directory);
                var path = Path.Combine(directory,DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ", System.Globalization.CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N") + "-postcard.png");
                var temporary = path + ".tmp";
                try
                {
                    var bytes = pixels.EncodeToPNG();
                    using (var file = new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                    { file.Write(bytes,0,bytes.Length); file.Flush(true); }
                    File.Move(temporary,path); return path;
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
            finally
            {
                RenderTexture.active = previous; root.SetActive(false); UnityEngine.Object.Destroy(root);
                texture.Release(); UnityEngine.Object.Destroy(texture); if (pixels != null) UnityEngine.Object.Destroy(pixels);
            }
        }
        private static void AddText(Transform parent,string value,int size,float x,float y,float width,float height)
        {
            var text = ConstellationDiagram.MakeText(parent,value,size); var rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = Vector2.zero; rect.pivot = new Vector2(0,1); rect.anchoredPosition = new Vector2(x,y); rect.sizeDelta = new Vector2(width,height);
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
        }
    }
}
