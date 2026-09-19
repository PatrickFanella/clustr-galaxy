using UnityEngine;

namespace ClustrGalaxy.World
{
    /// <summary>Artistic surface and atmosphere, separate from source metrics and gravity.</summary>
    public sealed class SolarBodyVisual : MonoBehaviour
    {
        private Material surface, atmosphere;
        private bool sun, stellar, ownsMesh;
        private static Mesh smoothSphere;
        private static int meshUsers;
        public void Initialize(Color tint,bool isSun,bool illuminates=false)
        {
            sun=illuminates; stellar=isSun;
            var shader=Resources.Load<Shader>("GalaxyPlanetSurface");
            if(shader==null)return;
            if(smoothSphere==null)smoothSphere=BuildSphere();
            GetComponent<MeshFilter>().sharedMesh=smoothSphere;meshUsers++;ownsMesh=true;
            surface=new Material(shader);surface.SetColor("_Tint",tint);surface.SetFloat("_Star",isSun?1:0);
            var renderer=GetComponent<Renderer>();if(renderer!=null){renderer.sharedMaterial=surface;renderer.enabled=true;}
            var haloShader=Resources.Load<Shader>("GalaxyAtmosphere");
            if(haloShader!=null)
            {
                atmosphere=new Material(haloShader);atmosphere.SetColor("_Tint",isSun?new Color(1,.39f,.065f,1):Color.Lerp(tint,new Color(.25f,.65f,1),.45f));
                atmosphere.SetFloat("_Strength",isSun?.75f:.32f);
                var halo=GameObject.CreatePrimitive(PrimitiveType.Sphere);halo.name=isSun?"Solar corona":"Atmospheric limb";
                var collider=halo.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
                halo.transform.SetParent(transform,false);halo.transform.localScale=Vector3.one*(isSun?1.16f:1.035f);
                halo.GetComponent<MeshFilter>().sharedMesh=smoothSphere;halo.GetComponent<Renderer>().sharedMaterial=atmosphere;
            }
        }
        public void ApplyCatalog(GalaxyVisualCatalog catalog)
        {
            var material = catalog?.SurfaceFor(GetComponent<GalaxyEntity>()?.StableId ?? name, stellar);
            if (surface != null && material != null) surface.CopyPropertiesFromMaterial(material);
        }
        private void LateUpdate(){if(sun)Shader.SetGlobalVector("_GalaxySunPosition",transform.position);}
        private static Mesh BuildSphere()
        {
            const int longitude=128,latitude=64;var vertices=new Vector3[(longitude+1)*(latitude+1)];var normals=new Vector3[vertices.Length];var indices=new int[longitude*latitude*6];var cursor=0;
            for(var y=0;y<=latitude;y++)for(var x=0;x<=longitude;x++)
            {
                var azimuth=x*Mathf.PI*2/longitude;var polar=y*Mathf.PI/latitude;
                var normal=new Vector3(Mathf.Sin(polar)*Mathf.Cos(azimuth),Mathf.Cos(polar),Mathf.Sin(polar)*Mathf.Sin(azimuth));
                var index=y*(longitude+1)+x;vertices[index]=normal*.5f;normals[index]=normal;
                if(y==latitude||x==longitude)continue;
                indices[cursor++]=index;indices[cursor++]=index+1;indices[cursor++]=index+longitude+1;
                indices[cursor++]=index+1;indices[cursor++]=index+longitude+2;indices[cursor++]=index+longitude+1;
            }
            var mesh=new Mesh{name="Smooth planetary sphere"};mesh.vertices=vertices;mesh.normals=normals;mesh.triangles=indices;mesh.RecalculateBounds();return mesh;
        }
        private void OnDestroy(){if(surface!=null)Destroy(surface);if(atmosphere!=null)Destroy(atmosphere);if(ownsMesh&&--meshUsers==0){Destroy(smoothSphere);smoothSphere=null;}}
    }
}
