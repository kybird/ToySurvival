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
                output.positionWS = TransformWorldToObject(input.positionOS.xyz); // We want world pos
                // Wait, TransformWorldToObject is wrong for getting world space. 
                // We use TransformObjectToWorld to get from local to world.
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float2 coord = input.positionWS.xz / _GridSize;
                float2 grid = abs(frac(coord - 0.5) - 0.5) / fwidth(coord);
                float lineVal = min(grid.x, grid.y);
                
                // Base grid color
                float4 color = _GridColor;
                
                // Normal lines
                float lineAlpha = 1.0 - smoothstep(0.0, _LineWidth * 10.0, lineVal);
                color = lerp(color, _LineColor, lineAlpha);
                
                // Major lines (every 5 units)
                float2 majorCoord = input.positionWS.xz / (_GridSize * 5.0);
                float2 majorGrid = abs(frac(majorCoord - 0.5) - 0.5) / fwidth(majorCoord);
                float majorLineVal = min(majorGrid.x, majorGrid.y);
                float majorLineAlpha = 1.0 - smoothstep(0.0, _LineWidth * 15.0, majorLineVal);
                color = lerp(color, _MainLineColor, majorLineAlpha);
                
                // Axis lines (X=0 or Z=0)
                float axisX = 1.0 - smoothstep(0.0, _LineWidth * 20.0, abs(input.positionWS.x) / fwidth(input.positionWS.x));
                float axisZ = 1.0 - smoothstep(0.0, _LineWidth * 20.0, abs(input.positionWS.z) / fwidth(input.positionWS.z));
                color = lerp(color, float4(0.8, 0.3, 0.3, 1), axisZ); // X Axis (Reddish)
                color = lerp(color, float4(0.3, 0.3, 0.8, 1), axisX); // Z Axis (Blueish)
                
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
