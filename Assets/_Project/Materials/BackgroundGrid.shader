Shader "Custom/BackgroundGrid"
{
    Properties
    {
        _GridColor ("Grid Color", Color) = (0.2, 0.2, 0.2, 1)
        _LineColor ("Line Color", Color) = (0.3, 0.3, 0.3, 1)
        _MainLineColor ("Main Line Color", Color) = (0.5, 0.5, 0.5, 1)
        _GridSize ("Grid Size", Float) = 1.0
        _LineWidth ("Line Width", Float) = 0.02
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Background" }
        LOD 100

        Pass
        {
            Name "Unlit"
            HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _GridColor;
                float4 _LineColor;
                float4 _MainLineColor;
                float _GridSize;
                float _LineWidth;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                // 픽셀당 월드 단위 거리 계산
                float2 dx = ddx(input.positionWS.xy);
                float2 dy = ddy(input.positionWS.xy);
                float2 invScale = 1.0 / sqrt(dx*dx + dy*dy); // 픽셀당 거리의 역수

                // 1. 일반 격자
                float2 coord = input.positionWS.xy / _GridSize;
                float2 grid = abs(frac(coord - 0.5) - 0.5) / fwidth(coord);
                float lineVal = min(grid.x, grid.y);
                
                // 두께를 픽셀 단위로 제어 (기본 1.0 픽셀 보장)
                float lineAlpha = 1.0 - smoothstep(1.0, 1.5, lineVal);
                float4 color = lerp(_GridColor, _LineColor, lineAlpha);
                
                // 2. 주요 격자 (Major lines - 5단위)
                float2 majorCoord = input.positionWS.xy / (_GridSize * 5.0);
                float2 majorGrid = abs(frac(majorCoord - 0.5) - 0.5) / fwidth(majorCoord);
                float majorLineVal = min(majorGrid.x, majorGrid.y);
                float majorLineAlpha = 1.0 - smoothstep(1.5, 2.0, majorLineVal);
                color = lerp(color, _MainLineColor, majorLineAlpha);
                
                // 3. 축 표시 (X=0 or Y=0)
                // 현재 좌표가 0 근처인지 확인 (픽셀 단위 거리 계산)
                float2 distFromZero = abs(input.positionWS.xy) * invScale;
                float axisX = 1.0 - smoothstep(2.0, 2.5, distFromZero.x);
                float axisY = 1.0 - smoothstep(2.0, 2.5, distFromZero.y);
                
                color = lerp(color, float4(0.8, 0.3, 0.3, 1), axisY); // X 축 (가로선, Red)
                color = lerp(color, float4(0.3, 0.3, 0.8, 1), axisX); // Y 축 (세로선, Blue)
                
                return color;
            }
            ENDHLSL

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDHLSL
        }
    }
}
