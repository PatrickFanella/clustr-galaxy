using System;
using System.IO;
using ClustrGalaxy.Core.Activities;
using ClustrGalaxy.Core.Survey;
using UnityEngine;
using UnityEngine.UI;

namespace ClustrGalaxy.UI
{
    public static class JourneyPostcard
    {
        public static string Export(string title, string body, string provenance, JourneyReplay replay = null, double seconds = 0)
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
                AddText(canvasObject.transform,title,44,80,990,1760,100);
                AddText(canvasObject.transform,body,replay==null?30:24,80,850,replay==null?1760:620,650);
                if(replay!=null)
                {
                    var minX=replay.Poses[0].Position.X;var minZ=replay.Poses[0].Position.Z;var maxX=minX;var maxZ=minZ;
                    foreach(var pose in replay.Poses){minX=Math.Min(minX,pose.Position.X);minZ=Math.Min(minZ,pose.Position.Z);maxX=Math.Max(maxX,pose.Position.X);maxZ=Math.Max(maxZ,pose.Position.Z);}
                    var fit=Math.Min(1000/Math.Max(1,maxX-minX),580/Math.Max(1,maxZ-minZ));
                    Vector2 Point(ReplayPosition p)=>new((float)(790+(p.X-minX)*fit),(float)(270+(p.Z-minZ)*fit));
                    var count=Math.Min(256,replay.Poses.Count);
                    for(var i=1;i<count;i++)
                    {
                        var first=(i-1)*(replay.Poses.Count-1)/(count-1);var last=i*(replay.Poses.Count-1)/(count-1);var gap=false;
                        for(var j=first+1;j<=last;j++)if(replay.Poses[j].Seconds-replay.Poses[j-1].Seconds>.25){gap=true;break;}
                        if(gap)continue;
                        var a=Point(replay.Poses[first].Position);var b=Point(replay.Poses[last].Position);
                        var go=new GameObject("Recorded segment",typeof(RectTransform),typeof(Image));go.transform.SetParent(canvasObject.transform,false);go.GetComponent<Image>().color=new Color(.43f,.86f,.84f);
                        var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=Vector2.zero;rect.pivot=new Vector2(0,.5f);rect.anchoredPosition=a;rect.sizeDelta=new Vector2((b-a).magnitude,4);rect.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg);
                    }
                    var dot=new GameObject("Chosen replay moment",typeof(RectTransform),typeof(Image));dot.transform.SetParent(canvasObject.transform,false);dot.GetComponent<Image>().color=new Color(.96f,.77f,.42f);
                    var marker=dot.GetComponent<RectTransform>();marker.anchorMin=marker.anchorMax=Vector2.zero;marker.anchoredPosition=Point(replay.At(seconds).Position);marker.sizeDelta=Vector2.one*16;
                }
                AddText(canvasObject.transform,"LOCAL JOURNEY · CLUSTR GALAXY\n"+provenance,22,80,145,1760,115);
                foreach (var child in root.GetComponentsInChildren<Transform>()) child.gameObject.layer = 30;
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = texture;
                pixels = new Texture2D(width,height,TextureFormat.RGB24,false); pixels.ReadPixels(new Rect(0,0,width,height),0,0); pixels.Apply();
                var directory = Path.Combine(Application.persistentDataPath,"ClustrGalaxy","exports"); Directory.CreateDirectory(directory);
                var path = Path.Combine(directory,DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ", System.Globalization.CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N") + "-journey-postcard.png");
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
            text.supportRichText = false; text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
        }
    }
}
