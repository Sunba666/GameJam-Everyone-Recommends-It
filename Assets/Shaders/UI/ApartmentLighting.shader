Shader "EveryoneRecommends/UI/ApartmentLighting"
{
    Properties
    {
        [PerRendererData] _MainTex ("Background", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Night ("Night blend", Range(0,1)) = 0
        _Dusk ("Dusk blend", Range(0,1)) = 0
        _LampStrength ("Lamp strength", Range(0,2)) = 1
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="False" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata { float4 vertex:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0; };
            struct v2f { float4 vertex:SV_POSITION;fixed4 color:COLOR;float2 uv:TEXCOORD0;float4 world:TEXCOORD1; };
            sampler2D _MainTex;
            fixed4 _Color,_TextureSampleAdd;
            float4 _ClipRect;
            float _Night,_Dusk,_LampStrength;
            v2f vert(appdata input)
            {
                v2f output;output.world=input.vertex;output.vertex=UnityObjectToClipPos(input.vertex);
                output.uv=input.uv;output.color=input.color*_Color;return output;
            }
            float room(float2 p,float4 bounds)
            {
                float2 low=smoothstep(bounds.xy-10,bounds.xy+10,p);
                float2 high=1-smoothstep(bounds.zw-10,bounds.zw+10,p);
                return low.x*low.y*high.x*high.y;
            }
            float cone(float2 p,float2 bulb,float height,float width)
            {
                float dy=p.y-bulb.y;float radius=22+max(dy,0)*width/height;
                float horizontal=exp(-pow((p.x-bulb.x)/radius,2)*2);
                return horizontal*smoothstep(-5,12,dy)*(1-smoothstep(height*.65,height,dy));
            }
            float halo(float2 p,float2 bulb,float2 radius)
            {
                float2 delta=(p-bulb)/radius;return exp(-dot(delta,delta)*2);
            }
            float lamp(float2 p,float2 bulb,float height,float width)
            {
                return cone(p,bulb,height,width)*.7+halo(p,bulb+float2(0,25),float2(width*.8,65))*.45;
            }
            fixed4 frag(v2f input):SV_Target
            {
                fixed4 sampleColor=(tex2D(_MainTex,input.uv)+_TextureSampleAdd)*input.color;
                float2 p=float2(input.uv.x*1672,(1-input.uv.y)*941);
                float indoors=max(room(p,float4(149,149,1088,325)),max(room(p,float4(150,382,1088,584)),room(p,float4(150,620,1088,833))));
                float light=0,glow=0;
                light+=lamp(p,float2(397,196),145,110);glow+=halo(p,float2(397,196),float2(17,11));
                light+=lamp(p,float2(632,194),145,110);glow+=halo(p,float2(632,194),float2(17,11));
                light+=lamp(p,float2(846,199),145,105);glow+=halo(p,float2(846,199),float2(17,11));
                light+=lamp(p,float2(1016,216),145,85);glow+=halo(p,float2(1016,216),float2(17,11));
                light+=lamp(p,float2(595,415),165,110);glow+=halo(p,float2(595,415),float2(17,11));
                light+=lamp(p,float2(674,408),165,110);glow+=halo(p,float2(674,408),float2(17,11));
                light+=lamp(p,float2(1007,421),145,85);glow+=halo(p,float2(1007,421),float2(17,11));
                light+=lamp(p,float2(478,524),75,65);glow+=halo(p,float2(478,524),float2(20,15));
                light+=lamp(p,float2(763,533),65,55);glow+=halo(p,float2(763,533),float2(17,13));
                light+=lamp(p,float2(214,698),125,95);glow+=halo(p,float2(214,698),float2(23,16));
                light+=lamp(p,float2(596,654),170,110);glow+=halo(p,float2(596,654),float2(16,10));
                light+=lamp(p,float2(657,654),170,110);glow+=halo(p,float2(657,654),float2(16,10));
                float3 nightAmbient=lerp(float3(.24,.31,.48),float3(.55,.50,.43),indoors);
                float3 illumination=nightAmbient+min(light,1.35)*float3(.51,.39,.23)*_LampStrength*indoors;
                float3 result=sampleColor.rgb*lerp(float3(1,1,1),illumination,_Night);
                result=lerp(result,result*float3(1.03,.85,.73)+float3(.045,.015,0),_Dusk*.45);
                result+=float3(1,.65,.24)*min(glow,1)*(_Night*.38+_Dusk*.06)*_LampStrength;
                sampleColor.rgb=saturate(result);
                #ifdef UNITY_UI_CLIP_RECT
                sampleColor.a*=UnityGet2DClipping(input.world.xy,_ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(sampleColor.a-.001);
                #endif
                return sampleColor;
            }
            ENDCG
        }
    }
}
