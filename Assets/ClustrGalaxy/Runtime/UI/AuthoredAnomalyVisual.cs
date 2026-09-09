using System;
using UnityEngine;
namespace ClustrGalaxy.UI
{
    /// <summary>Presentation only. Discovery timing and persistence belong to the campaign service.</summary>
    public sealed class AuthoredAnomalyVisual : MonoBehaviour
    {
        private Transform[] parts; private Vector3[] positions,scales; private Quaternion[] rotations;
        private Renderer[] renderers;private Color[] emission;private MaterialPropertyBlock tint;private double pulseUntil;private bool highlighted;
        private int kind; private double previousSeconds; private float watchedBlend;
        public void Initialize(int value)
        {
            kind=value;renderers=GetComponentsInChildren<Renderer>(true);emission=new Color[renderers.Length];tint=new MaterialPropertyBlock();
            for(var i=0;i<renderers.Length;i++)emission[i]=renderers[i].sharedMaterial!=null&&renderers[i].sharedMaterial.HasProperty("_EmissionColor")?renderers[i].sharedMaterial.GetColor("_EmissionColor"):Color.black;
            var all=GetComponentsInChildren<Transform>(true);var count=0;
            foreach(var part in all)if(part.name.StartsWith("Motion",StringComparison.Ordinal))count++;
            parts=new Transform[count];positions=new Vector3[count];rotations=new Quaternion[count];scales=new Vector3[count];count=0;
            foreach(var part in all)if(part.name.StartsWith("Motion",StringComparison.Ordinal))
            {parts[count]=part;positions[count]=part.localPosition;rotations[count]=part.localRotation;scales[count]=part.localScale;count++;}
        }
        public void Pulse(double seconds,float intensity)
        {
            if(renderers==null)return;pulseUntil=seconds+.6;highlighted=true;
            for(var i=0;i<renderers.Length;i++){tint.SetColor("_EmissionColor",emission[i]+new Color(.5f,.32f,.08f)*Mathf.Clamp01(intensity));renderers[i].SetPropertyBlock(tint);}
        }
        public void Present(double seconds,bool watched,bool reducedMotion)
        {
            if(parts==null)return;
            if(highlighted&&seconds>=pulseUntil){highlighted=false;foreach(var renderer in renderers)renderer.SetPropertyBlock(null);}
            watchedBlend=Mathf.MoveTowards(watchedBlend,watched?1:0,(float)Math.Clamp(seconds-previousSeconds,0,.1)*.65f);previousSeconds=seconds;
            var t=reducedMotion?0f:(float)(seconds%3600);
            for(var i=0;i<parts.Length;i++)
            {
                var part=parts[i];part.localPosition=positions[i];part.localRotation=rotations[i];part.localScale=scales[i];
                switch(kind)
                {
                    case 0: part.localRotation=rotations[i]*Quaternion.Euler(15+Mathf.Sin(t*.6f)*8,t*12,0);break;
                    case 1: part.localRotation=rotations[i]*Quaternion.Euler(0,0,reducedMotion?0:(65*watchedBlend+Mathf.Sin(t*.8f)*8)*(i==0?-1:1));break;
                    case 2: part.localRotation=rotations[i]*Quaternion.Euler(0,t*(12+i*7),0);break;
                    case 3: part.localScale=scales[i]*(reducedMotion?1:Mathf.Lerp(.35f,1,Mathf.Clamp01((t%8)-i*.65f)));break;
                    case 4: part.localPosition=positions[i]+new Vector3(0,Mathf.Sin(t*.55f+i)*.28f,0);part.localRotation=rotations[i]*Quaternion.Euler(0,Mathf.Sin(t*.35f+i)*12,0);break;
                    case 5: part.localRotation=rotations[i]*Quaternion.Euler(0,t*22,0);break;
                }
            }
        }
    }
}
