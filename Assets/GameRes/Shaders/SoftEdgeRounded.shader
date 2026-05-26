Shader "UI/SoftEdgeRounded"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        // 关键属性：控制边缘模糊的范围（值越大，边缘越模糊/透明）
        _SoftEdge ("Soft Edge Width", Range(0.0, 0.5)) = 0.1
        
        // 圆角半径（0为直角，0.5为半圆）
        _CornerRadius ("Corner Radius", Range(0.0, 0.5)) = 0.1

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "DEFAULT"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed _SoftEdge;
            fixed _CornerRadius;
            float4 _ClipRect;
            float4 _MainTex_TexelSize;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);

                OUT.texcoord = v.texcoord;

                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 color = tex2D(_MainTex, IN.texcoord) * IN.color;

                // 1. 计算圆角遮罩 (使用距离场思想)
                // UV范围是 0-1，我们把UV平移到 -0.5 到 0.5 的中心坐标系
                float2 uv = IN.texcoord - 0.5;
                
                // 计算当前像素到中心的距离（绝对值）
                float2 dist = abs(uv);
                
                // 计算圆角的有效范围（正方形减去圆角部分）
                // _CornerRadius 是相对于 0.5 的比例
                float radius = _CornerRadius;
                float2 cornerCenter = float2(0.5 - radius, 0.5 - radius);
                
                // 计算到四个角圆心的距离（简化版，只算右下角然后对称）
                // 实际上我们只需要算最远的那个角
                float2 d = dist - (0.5 - radius);
                
                // 如果在圆角范围内，计算到圆心的距离
                float edgeDist = length(max(d, 0.0));
                
                // 2. 计算边缘羽化（上下左右边缘）
                // 计算到四条边的距离
                float leftDist = IN.texcoord.x;
                float rightDist = 1 - IN.texcoord.x;
                float bottomDist = IN.texcoord.y;
                float topDist = 1 - IN.texcoord.y;
                
                // 取最近的那条边
                float minEdgeDist = min(min(leftDist, rightDist), min(topDist, bottomDist));
                
                // 3. 混合圆角和普通边缘
                // 圆角区域的边缘计算
                float cornerAlpha = saturate((radius - edgeDist) / _SoftEdge);
                // 普通边缘的计算
                float edgeAlpha = saturate(minEdgeDist / _SoftEdge);
                
                // 最终透明度：取两者较小值（哪个先变透明听哪个的）
                float finalAlphaMask = min(cornerAlpha, edgeAlpha);
                
                // 4. 应用到原图的 Alpha 通道
                color.a *= finalAlphaMask;

                // 裁切（UI 标准）
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                
                return color;
            }
            ENDCG
        }
    }
}