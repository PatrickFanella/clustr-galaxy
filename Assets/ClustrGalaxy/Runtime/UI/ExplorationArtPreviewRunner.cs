using System;
using System.Collections;
using System.IO;
using ClustrGalaxy.World;
using UnityEngine;
namespace ClustrGalaxy.UI
{
    public sealed class ExplorationArtPreviewRunner : MonoBehaviour
    {
        private GalaxyVisualCatalog catalog;
        public void Initialize(GalaxyVisualCatalog value){catalog=value;}
        private IEnumerator Start()
        {
            yield return null;
            if(catalog==null||catalog.anomalyPrefabs.Length!=6||catalog.keepsakePrefabs.Length!=3||catalog.observatoryPlatformPrefab==null){Debug.LogError("[ClustrGalaxy.Art] Missing catalogue assets.");Application.Quit(2);yield break;}
            var root=new GameObject("Owned art preview stage");root.transform.position=Vector3.zero;
            var cameraObject=new GameObject("Preview camera",typeof(Camera));cameraObject.transform.SetParent(root.transform,false);
            var camera=cameraObject.GetComponent<Camera>();camera.depth=100;camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.015f,.025f,.04f);camera.orthographic=true;camera.orthographicSize=8.5f;
            camera.transform.localPosition=new Vector3(0,9,-18);camera.transform.LookAt(root.transform.position);
            var lightObject=new GameObject("Preview key",typeof(Light));lightObject.transform.SetParent(root.transform,false);var light=lightObject.GetComponent<Light>();light.type=LightType.Directional;light.intensity=2;light.cullingMask=1<<31;light.transform.rotation=Quaternion.Euler(45,-30,0);
            for(var i=0;i<10;i++)
            {
                var prefab=i<6?catalog.anomalyPrefabs[i]:i<9?catalog.keepsakePrefabs[i-6]:catalog.observatoryPlatformPrefab;
                if(prefab==null){Application.Quit(2);yield break;}
                var model=Instantiate(prefab,root.transform);model.transform.localPosition=new Vector3((i%5-2)*5,0,(i/5==0?6:-6));
                foreach(var part in model.GetComponentsInChildren<Transform>())part.gameObject.layer=31;
                var label=new GameObject("Asset label",typeof(TextMesh));label.transform.SetParent(root.transform,false);label.layer=31;label.transform.localPosition=model.transform.localPosition+new Vector3(0,-1.5f,-1.5f);label.transform.rotation=camera.transform.rotation;
                var text=label.GetComponent<TextMesh>();text.text=prefab.name;text.fontSize=48;text.characterSize=.09f;text.anchor=TextAnchor.MiddleCenter;text.color=new Color(.9f,.94f,.95f);
            }
            foreach(var canvas in FindObjectsByType<Canvas>())canvas.enabled=false;
            yield return new WaitForSecondsRealtime(2);
            var path=Path.Combine(Application.persistentDataPath,"ClustrGalaxy","art-previews");Directory.CreateDirectory(path);
            ScreenCapture.CaptureScreenshot(Path.Combine(path,"exploration-assets.png"));yield return new WaitForSecondsRealtime(1);
            Debug.Log("[ClustrGalaxy.Art] Preview captured: "+path);Application.Quit(0);
        }
    }
}
