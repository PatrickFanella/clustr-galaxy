using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
namespace ClustrGalaxy.Editor
{
    /// <summary>Editable original lathed/torus mesh authoring for the cozy retro-space collection.</summary>
    public static class ExplorationAssetBuilder
    {
        public const string Folder="Assets/ClustrGalaxy/Presentation/Exploration";
        private static Material enamel,brass,glass,dark;
        private static int meshId;
        [MenuItem("Clustr Galaxy/Author Exploration Assets")]
        public static void Build()
        {
            if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/ClustrGalaxy/Presentation","Exploration");
            enamel=Material("Enamel",new Color(.13f,.37f,.4f),.25f);brass=Material("Brass",new Color(.64f,.43f,.21f),.8f);
            dark=Material("Ink",new Color(.035f,.065f,.095f),.4f);glass=Material("AmberGlass",new Color(.72f,.38f,.11f),.15f,true);
            string[] names={"EchoReceiver","ShySatellite","PocketOrrery","WindowStation","LanternCluster","InvertedBeacon","PocketResonator","WanderingLantern","WindowPrism","ObservatoryPlatform"};
            for(var kind=0;kind<names.Length;kind++)
            {
                var root=new GameObject(names[kind]);meshId=0;
                try
                {
                    if(kind==0||kind==6)Receiver(root.transform);
                    else if(kind==1)Satellite(root.transform);
                    else if(kind==2)Orrery(root.transform);
                    else if(kind==3)Station(root.transform);
                    else if(kind==4)for(var i=0;i<5;i++){var a=i*Mathf.PI*2/5;var g=Group(root.transform,"MotionLantern"+i,new Vector3(Mathf.Cos(a)*1.8f,Mathf.Sin(a*2)*.4f,Mathf.Sin(a)*1.8f));Lantern(g);}
                    else if(kind==5)Beacon(root.transform);
                    else if(kind==7)Lantern(root.transform);
                    else if(kind==8){Lathe(root.transform,"Prism",new[]{new Vector2(.65f,-.8f),new Vector2(.85f,-.65f),new Vector2(.65f,.8f),new Vector2(0,1.1f)},glass,6);Ring(root.transform,"PrismFrame",.75f,.055f,brass,Vector3.zero);}
                    else Platform(root.transform);
                    PrefabUtility.SaveAsPrefabAsset(root,Folder+"/"+names[kind]+".prefab");
                }
                finally{UnityEngine.Object.DestroyImmediate(root);}
            }
            AssetDatabase.SaveAssets();Debug.Log("[ClustrGalaxy.Art] Ten original exploration prefabs authored.");
        }
        private static Transform Group(Transform parent,string name,Vector3 position)
        {var g=new GameObject(name).transform;g.SetParent(parent,false);g.localPosition=position;return g;}
        private static Material Material(string name,Color color,float metal,bool light=false)
        {
            var path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metal);m.SetFloat("_Smoothness",light ? .35f : .45f);
            if(light){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*.65f);}EditorUtility.SetDirty(m);return m;
        }
        private static Transform Shape(Transform parent,string name,Vector3[] vertices,int[] indices,Material material)
        {
            var root=parent;while(root.parent!=null)root=root.parent;
            var path=Folder+"/"+root.name+"-"+(meshId++)+".asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(mesh==null){mesh=new Mesh();AssetDatabase.CreateAsset(mesh,path);}else mesh.Clear();
            mesh.name=name;mesh.vertices=vertices;mesh.triangles=indices;mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=material;return go.transform;
        }
        private static Transform Lathe(Transform parent,string name,Vector2[] profile,Material material,int slices=32)
        {
            var vertices=new Vector3[(slices+1)*profile.Length];var triangles=new List<int>();
            for(var row=0;row<profile.Length;row++)for(var i=0;i<=slices;i++)
            {
                var a=i*Mathf.PI*2/slices;var index=row*(slices+1)+i;vertices[index]=new Vector3(Mathf.Cos(a)*profile[row].x,profile[row].y,Mathf.Sin(a)*profile[row].x);
                if(row==0||i==slices)continue;var below=index-slices-1;
                triangles.AddRange(new[]{below,index,below+1,below+1,index,index+1});
            }
            return Shape(parent,name,vertices,triangles.ToArray(),material);
        }
        private static Transform Ring(Transform parent,string name,float radius,float tube,Material material,Vector3 pos)
        {
            var profile=new Vector2[13];for(var i=0;i<13;i++){var a=i*Mathf.PI*2/12;profile[i]=new Vector2(radius+Mathf.Cos(a)*tube,Mathf.Sin(a)*tube);}
            var t=Lathe(parent,name,profile,material,48);t.localPosition=pos;return t;
        }
        private static void Housing(Transform parent,float size=1)
        {
            var t=Lathe(parent,"EnamelHousing",new[]{new Vector2(0,-.5f),new Vector2(.7f,-.5f),new Vector2(.9f,-.35f),new Vector2(.9f,.25f),new Vector2(.7f,.5f),new Vector2(0,.5f)},enamel);t.localScale=Vector3.one*size;
            Ring(parent,"BrassCollar",.91f*size,.045f*size,brass,new Vector3(0,.12f*size,0));
        }
        private static void Receiver(Transform root)
        {
            Housing(root);var antenna=Group(root,"MotionAntenna",new Vector3(0,.5f,0));
            Lathe(antenna,"Dish",new[]{new Vector2(.05f,0),new Vector2(.2f,.08f),new Vector2(.5f,.27f),new Vector2(.95f,.55f),new Vector2(1,.6f),new Vector2(.95f,.61f),new Vector2(.5f,.35f),new Vector2(.15f,.18f)},brass);
            Lathe(antenna,"SignalNeedle",new[]{new Vector2(.035f,.1f),new Vector2(.035f,1),new Vector2(0,1.05f)},glass);
            for(var i=0;i<8;i++)Ring(root,"ReceiverFin"+i,.92f,.025f,dark,new Vector3(0,-.3f+i*.055f,0));
        }
        private static void Satellite(Transform root)
        {
            Housing(root);for(var side=-1;side<=1;side+=2)
            {var arm=Group(root,"MotionPanel"+side,new Vector3(side*.8f,0,0));var panel=Lathe(arm,"SolarPanel",new[]{new Vector2(0,-.06f),new Vector2(.9f,-.06f),new Vector2(.9f,.06f),new Vector2(0,.06f)},dark,6);panel.localPosition=new Vector3(side*.65f,0,0);Ring(arm,"PanelRim",.9f,.035f,brass,panel.localPosition);}
            Ring(root,"SensorEye",.24f,.06f,glass,new Vector3(0,.52f,0));
        }
        private static void Orrery(Transform root)
        {
            Housing(root,.65f);
            for(var i=0;i<3;i++){var orbit=Group(root,"MotionOrbit"+i,new Vector3(0,.8f,0));orbit.localRotation=Quaternion.Euler(i*27,0,i*19);Ring(orbit,"OrbitRail",.9f+i*.35f,.035f,brass,Vector3.zero);var planet=Lathe(orbit,"MiniatureWorld",new[]{new Vector2(0,-.15f),new Vector2(.15f,0),new Vector2(0,.15f)},i==1?enamel:glass,20);planet.localPosition=new Vector3(.9f+i*.35f,0,0);}
        }
        private static void Station(Transform root)
        {
            Housing(root,1.2f);Ring(root,"HabitatRing",1.65f,.26f,enamel,Vector3.zero);
            for(var i=0;i<8;i++){var a=i*Mathf.PI/4;var window=Group(root,"MotionWindow"+i,new Vector3(Mathf.Cos(a)*1.7f,.12f,Mathf.Sin(a)*1.7f));Ring(window,"WarmWindow",.13f,.055f,glass,Vector3.zero);}
            for(var i=0;i<3;i++)Ring(root,"DockingCollar"+i,.45f,.075f,brass,new Vector3(0,.65f+i*.12f,0));
        }
        private static void Lantern(Transform root)
        {
            Lathe(root,"LampGlass",new[]{new Vector2(0,-.5f),new Vector2(.32f,-.4f),new Vector2(.42f,0),new Vector2(.32f,.45f),new Vector2(0,.5f)},glass,24);
            for(var side=-1;side<=1;side+=2){var ring=Ring(root,"LampFrame"+side,.5f,.035f,brass,Vector3.zero);ring.localRotation=Quaternion.Euler(90,side*45,0);}
            Ring(root,"HangingLoop",.15f,.03f,brass,new Vector3(0,.68f,0)).localRotation=Quaternion.Euler(90,0,0);
        }
        private static void Beacon(Transform root)
        {
            Housing(root);var head=Group(root,"MotionBeacon",new Vector3(0,-.9f,0));
            Lathe(head,"InvertedLens",new[]{new Vector2(0,-.6f),new Vector2(.4f,-.4f),new Vector2(.65f,0),new Vector2(.65f,.2f),new Vector2(0,.2f)},glass,12);
            Ring(head,"DirectionalCage",.8f,.05f,brass,Vector3.zero).localRotation=Quaternion.Euler(90,0,0);
        }
        private static void Platform(Transform root)
        {
            Lathe(root,"ObservatoryDeck",new[]{new Vector2(0,-.25f),new Vector2(3,-.25f),new Vector2(3.1f,-.1f),new Vector2(3,0),new Vector2(0,0)},enamel,64);
            Ring(root,"DeckTrim",3,.045f,brass,Vector3.zero);
            for(var i=0;i<6;i++){var a=i*Mathf.PI/3;var slot=Group(root,"DisplayMount"+i,new Vector3(Mathf.Cos(a)*2,.12f,Mathf.Sin(a)*2));Housing(slot,.4f);}
        }
    }
}
