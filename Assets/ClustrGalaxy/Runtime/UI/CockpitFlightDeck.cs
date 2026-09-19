using UnityEngine;
using ClustrGalaxy.Flight;

namespace ClustrGalaxy.UI
{
    /// <summary>Presentation-only cockpit. No collider, mass or flight input ownership.</summary>
    public sealed class CockpitFlightDeck : MonoBehaviour
    {
        private GameObject deck;
        private ProbeHud hud;
        private Material hull, rim, glass, instrumentLight;
        public void Initialize(Camera camera, ProbeHud display, GalaxyNavigationController nav)
        {
            hud=display;
            deck=new GameObject("Surveyor flight deck"); deck.transform.SetParent(camera.transform,false);
            var shader=Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            hull=Surface(shader,new Color(.025f,.035f,.046f)); rim=Surface(shader,new Color(.105f,.14f,.165f));
            glass=Surface(shader,new Color(.012f,.025f,.031f));instrumentLight=Surface(shader,new Color(.78f,.51f,.24f));
            // Angled sill and canopy ribs leave the central flight view open.
            Beam("Port sill",new Vector3(-1.35f,-.67f,1),new Vector3(-.36f,-.62f,.72f),.16f,.2f,hull);
            Beam("Starboard sill",new Vector3(.36f,-.62f,.72f),new Vector3(1.35f,-.67f,1),.16f,.2f,hull);
            Beam("Instrument brow",new Vector3(-.4f,-.64f,.78f),new Vector3(.4f,-.64f,.78f),.065f,.1f,rim);
            for(var side=-1;side<=1;side+=2)
            {
                Beam("Canopy lower rib",new Vector3(side*1.15f,-.62f,1),new Vector3(side*1.18f,.15f,1.1f),.024f,.035f,rim);
                Beam("Canopy upper rib",new Vector3(side*1.18f,.15f,1.1f),new Vector3(side*.8f,.77f,1.15f),.022f,.03f,hull);
                Beam("Canopy crown",new Vector3(side*.8f,.77f,1.15f),new Vector3(side*.22f,.78f,1.05f),.025f,.04f,rim);
                Beam("Console inset",new Vector3(side*.42f,-.595f,.72f),new Vector3(side*.99f,-.60f,.85f),.035f,.065f,glass);
                Beam("Amber instrument rail",new Vector3(side*.47f,-.58f,.74f),new Vector3(side*.94f,-.59f,.85f),.008f,.012f,instrumentLight);
                for(var i=0;i<5;i++) Beam("Sill switch",new Vector3(side*(.52f+i*.072f),-.65f,.73f),new Vector3(side*(.52f+i*.072f),-.64f,.77f),.022f,.025f,rim);
            }
        }
        public void ApplyCatalog(ClustrGalaxy.World.GalaxyVisualCatalog catalog)
        {
            if (deck == null || catalog?.cockpitConsole == null || deck.transform.Find("Port console") != null) return;
            for (var side = -1; side <= 1; side += 2)
            {
                var console = Instantiate(catalog.cockpitConsole, deck.transform, false);
                console.name = side < 0 ? "Port console" : "Starboard console";
                console.transform.localPosition = new Vector3(side * .72f, -.64f, .92f);
                console.transform.localRotation = Quaternion.Euler(0, 180 - side * 18, 0);
                console.transform.localScale = Vector3.one * .43f;
            }
        }
        private static Material Surface(Shader shader,Color color)
        {var m=new Material(shader);m.color=color;if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",color);return m;}
        private void Beam(string name,Vector3 a,Vector3 b,float width,float depth,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;
            var collider=go.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
            go.transform.SetParent(deck.transform,false);go.transform.localPosition=(a+b)*.5f;
            go.transform.localRotation=Quaternion.FromToRotation(Vector3.right,b-a);
            go.transform.localScale=new Vector3(Vector3.Distance(a,b),width,depth);
            go.GetComponent<Renderer>().sharedMaterial=material;
        }
        private void LateUpdate()
        {if(deck!=null){var show=hud!=null;if(deck.activeSelf!=show)deck.SetActive(show);}}
        private void OnDestroy()
        {if(deck!=null)Destroy(deck);if(hull!=null)Destroy(hull);if(rim!=null)Destroy(rim);if(glass!=null)Destroy(glass);if(instrumentLight!=null)Destroy(instrumentLight);}
    }
}
