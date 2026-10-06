Shader "Clustr/Galaxy Atmosphere" {
Properties { _Tint("Limb color",Color)=(.2,.5,1,1) _Strength("Intensity",Float)=.3 }
SubShader { Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
Pass { Blend SrcAlpha One ZWrite Off Cull Back
HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;};struct V {float4 positionCS:SV_POSITION;float3 normal:TEXCOORD0;float3 world:TEXCOORD1;};
CBUFFER_START(UnityPerMaterial)
float4 _Tint;float _Strength;
CBUFFER_END
V vert(A v){V o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.normal=TransformObjectToWorldNormal(v.normalOS);o.world=TransformObjectToWorld(v.positionOS.xyz);return o;}
half4 frag(V i):SV_Target {float rim=pow(1-saturate(dot(normalize(i.normal),normalize(GetWorldSpaceViewDir(i.world)))),3);return half4(_Tint.rgb,rim*_Strength);}
ENDHLSL } } }
