using System;
using System.Collections;
using System.IO;
using ClustrGalaxy.Bootstrap;
using ClustrGalaxy.Core.Activities;
using ClustrGalaxy.Persistence;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
namespace ClustrGalaxy.UI
{
    public sealed class LocalImageViewer : MonoBehaviour
    {
        private GameObject panel;private Texture2D texture;private ProbeHud hud;private Action closed;
        private ActivityIdentity identity;private DateTime capturedUtc;private bool capturing,saved;
        private Text status;private Button closeButton;
        public bool IsOpen=>panel!=null||capturing;
        public string LastSavedPath {get;private set;}
        public void Capture(){if(IsOpen)return;StartCoroutine(CaptureFrame());}
        private IEnumerator CaptureFrame()
        {
            capturing=true;saved=false;LastSavedPath=null;hud=GetComponent<ProbeHud>();identity=GetComponent<GalaxySessionBehaviour>().Session.Activities.State.Identity;
            capturedUtc=DateTime.UtcNow;hud.SetMenuOpen(false);hud.SetModalInputCaptured(true);
            var canvases=GetComponentsInChildren<Canvas>(true);var enabled=new bool[canvases.Length];
            for(var i=0;i<canvases.Length;i++){enabled[i]=canvases[i].enabled;canvases[i].enabled=false;}
            yield return new WaitForEndOfFrame();
            string error=null;
            try{texture=ScreenCapture.CaptureScreenshotAsTexture();if(texture==null)throw new InvalidOperationException("Rendered image unavailable.");}
            catch(Exception e){error=e.Message;}
            finally{for(var i=0;i<canvases.Length;i++)if(canvases[i]!=null)canvases[i].enabled=enabled[i];capturing=false;}
            if(error!=null){hud.SetModalInputCaptured(false);Debug.LogWarning("[ClustrGalaxy.Image] Capture failed: "+error);yield break;}
            OpenPanel("SCREENSHOT PREVIEW",true);
        }
        public void OpenSaved(string path,Action onClose=null)
        {
            if(IsOpen)return;var bytes=LocalImageStore.ReadPng(path);var image=new Texture2D(2,2,TextureFormat.RGBA32,false);
            if(!image.LoadImage(bytes)){Destroy(image);throw new InvalidDataException("PNG image could not be decoded.");}
            texture=image;closed=onClose;saved=true;hud=GetComponent<ProbeHud>();OpenPanel("LOCAL IMAGE · "+Path.GetFileName(path),false);
        }
        private void OpenPanel(string title,bool canSave)
        {
            panel=new GameObject("Local image preview",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));panel.transform.SetParent(transform,false);
            CockpitStationSurface.Mount(panel, this);panel.GetComponent<Canvas>().sortingOrder=16;
            var scaler=panel.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=1;
            var bg=new GameObject("Backdrop",typeof(RectTransform),typeof(Image));bg.transform.SetParent(panel.transform,false);var rect=bg.GetComponent<RectTransform>();rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;bg.GetComponent<Image>().color=new Color(.025f,.045f,.065f);
            Label(title,26,30,695,1220,40);status=Label(texture.width+" × "+texture.height+" · Local image · "+(canSave?"Nothing is saved until you choose Save.":"Viewing creates no discoveries or awards."),16,30,650,1220,40);
            var area=new GameObject("Image area",typeof(RectTransform));area.transform.SetParent(panel.transform,false);Place(area.GetComponent<RectTransform>(),30,605,1220,480);
            var image=new GameObject("Image",typeof(RectTransform),typeof(RawImage),typeof(AspectRatioFitter));image.transform.SetParent(area.transform,false);image.GetComponent<RawImage>().texture=texture;
            var aspect=image.GetComponent<AspectRatioFitter>();aspect.aspectMode=AspectRatioFitter.AspectMode.FitInParent;aspect.aspectRatio=(float)texture.width/texture.height;
            var save=canSave?Button("SAVE IMAGE",30,Save):null;closeButton=Button(canSave?"DISCARD IMAGE":"CLOSE IMAGE",650,Close);
            if(save!=null){save.navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnRight=closeButton,selectOnDown=closeButton};closeButton.navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnLeft=save,selectOnUp=save};}
            hud.SetModalInputCaptured(true);EventSystem.current?.SetSelectedGameObject((save??closeButton).gameObject);
        }
        private void Save()
        {
            if(saved)return;
            try
            {
                var bytes=texture.EncodeToPNG();LastSavedPath=new LocalImageStore(Path.Combine(Application.persistentDataPath,"ClustrGalaxy")).Save(bytes,identity,capturedUtc);
                saved=true;status.text="Saved locally · "+texture.width+" × "+texture.height+" · "+bytes.Length+" bytes · Snapshot "+identity.Snapshot;
                closeButton.GetComponentInChildren<Text>().text="CLOSE IMAGE";
            }
            catch(Exception e){status.text="Image was not saved. Retry: "+e.Message;}
        }
        private static void Place(RectTransform r,float x,float y,float w,float h){r.anchorMin=r.anchorMax=Vector2.zero;r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);}
        private Text Label(string value,int size,float x,float y,float w,float h){var text=ConstellationDiagram.MakeText(panel.transform,value,size);Place(text.rectTransform,x,y,w,h);return text;}
        private Button Button(string value,float x,UnityEngine.Events.UnityAction action)
        {var go=new GameObject(value,typeof(RectTransform),typeof(Image),typeof(Button));go.transform.SetParent(panel.transform,false);Place(go.GetComponent<RectTransform>(),x,85,600,50);go.GetComponent<Image>().color=new Color(.14f,.23f,.28f);var button=go.GetComponent<Button>();button.onClick.AddListener(action);var label=ConstellationDiagram.MakeText(go.transform,value,18);label.alignment=TextAnchor.MiddleCenter;label.rectTransform.anchorMin=Vector2.zero;label.rectTransform.anchorMax=Vector2.one;label.rectTransform.offsetMin=label.rectTransform.offsetMax=Vector2.zero;return button;}
        private void Update(){if(panel!=null&&(Keyboard.current?.escapeKey.wasPressedThisFrame==true||Gamepad.current?.buttonEast.wasPressedThisFrame==true))Close();}
        public void Close(){if(panel==null)return;panel.SetActive(false);Destroy(panel);panel=null;if(texture!=null)Destroy(texture);texture=null;var callback=closed;closed=null;if(callback!=null)callback();else hud.SetModalInputCaptured(false);}
        private void OnDestroy(){Close();}
    }
}
