Shader "Clustr/Galaxy Planet Surface" {
Properties { _Tint("Surface palette",Color)=(.2,.6,.8,1) _Star("Stellar surface",Float)=0 _SurfaceMap("Artistic surface map",2D)="white" {} _UseSurfaceMap("Use artistic map",Float)=0 }
SubShader { Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
Pass { HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
struct A { float4 positionOS:POSITION;float3 normalOS:NORMAL; };
struct V { float4 positionCS:SV_POSITION;float3 normal:TEXCOORD0;float3 local:TEXCOORD1;float3 world:TEXCOORD2; };
CBUFFER_START(UnityPerMaterial)
float4 _Tint;float _Star;float _UseSurfaceMap;
CBUFFER_END
TEXTURE2D(_SurfaceMap); SAMPLER(sampler_SurfaceMap);
float3 _GalaxySunPosition;
V vert(A v){V o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.normal=TransformObjectToWorldNormal(v.normalOS);o.local=v.normalOS;o.world=TransformObjectToWorld(v.positionOS.xyz);return o;}
float hash(float3 p){return frac(sin(dot(p,float3(127.1,311.7,74.7)))*43758.5453);}
float noise(float3 p){float3 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(lerp(hash(i),hash(i+float3(1,0,0)),f.x),lerp(hash(i+float3(0,1,0)),hash(i+float3(1,1,0)),f.x),f.y),lerp(lerp(hash(i+float3(0,0,1)),hash(i+float3(1,0,1)),f.x),lerp(hash(i+float3(0,1,1)),hash(i+1),f.x),f.y),f.z);}
float fbm(float3 p){return noise(p)*.55+noise(p*2.04)*.27+noise(p*4.13)*.13+noise(p*8.1)*.05;}
half4 frag(V i):SV_Target {
float3 n=normalize(i.normal),p=normalize(i.local);
if(_UseSurfaceMap>.5){
    float2 uv=float2(atan2(p.z,p.x)/(2*PI)+.5,asin(clamp(p.y,-1,1))/PI+.5);
    // Explicit wrapped derivatives avoid a dark mip seam at the longitude boundary.
    float2 dx=ddx(uv),dy=ddy(uv);dx.x-=round(dx.x);dy.x-=round(dy.x);
    float3 mapped=SAMPLE_TEXTURE2D_GRAD(_SurfaceMap,sampler_SurfaceMap,uv,dx,dy).rgb;
    if(_Star>.5)return half4(mapped*1.65,1);
    float light=saturate(dot(n,normalize(_GalaxySunPosition-i.world+float3(.0001,0,0))));
    return half4(mapped*(.09+light*1.35),1);
}
float terrain=fbm(p*5.5+_Tint.rgb*13);
if(_Star>.5){float cells=fbm(p*24+_Time.y*.035);float bands=sin(p.y*32+terrain*12)*.5+.5;return half4(lerp(float3(1,.22,.025),float3(1,.87,.38),cells*.8+bands*.2)*1.5,1);}
float land=smoothstep(.46,.56,terrain);float polar=smoothstep(.82,.98,abs(p.y));
float3 sea=_Tint.rgb*.35;float3 continent=lerp(_Tint.rgb,float3(.5,.43,.29),.45)*(terrain+.45);
float clouds=smoothstep(.62,.76,fbm(p*11+float3(4,2,8)));
float3 albedo=lerp(lerp(sea,continent,land),float3(.85,.91,.92),max(polar,clouds*.72));
float3 towardSun=normalize(_GalaxySunPosition-i.world+float3(.0001,0,0));float day=saturate(dot(n,towardSun));
float3 color=albedo*(.10+day*1.35);return half4(color,1);
}
ENDHLSL } } }
