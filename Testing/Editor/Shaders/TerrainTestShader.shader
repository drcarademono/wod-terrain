Shader "Monobelisk/Terrain Tester"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        _DerivMap("Texture", 2D) = "white" {}
    }
        SubShader
    {
        Tags { "RenderType" = "Opaque" "LightMode" = "ForwardBase" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma geometry geom
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            float newHeight;
            float tileSize;
            float2 offset;
            int res;
            sampler2D _DerivMap;
            float4 _DerivMap_ST;
            float warp;

            #include "UnityCG.cginc"
            #include "../../../Assets/Shaders/noises.cginc"
            #include "../../../Assets/Shaders/noiseParams.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

            struct v2g
            {
                float4 vertex : SV_POSITION;
                float3 normal : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

            struct g2f
            {
                float3 normal : TEXCOORD0;
                float2 uv : TEXCOORD1;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            float HeightSample(float2 pos) {
                pos = pos + offset;
                float2 worldUv = pos / (float2(1000, 500) * 129);
                float3 dm = tex2Dlod(_DerivMap, float4(worldUv, 0, 0));
                dm.xy = normalize(float3(dm.xy, 1)).xy;
                dm.xy = dm.xy * 2 - 1;

                // Swiss Folds
                /*SwissParams p = SwissFolded(pos);
                float sm = saturate(SwissMountains(p, dm.xy));
                return saturate(sm) * newHeight;*/

                // Swiss faults
                SwissParams p = SwissFaults(pos);
                return SwissTime(p) * newHeight;

                //Jordan mnt
                /*JordanParams p = JordanFolded(pos);
                return saturate(JordanMountains(p)) * newHeight;*/

                // IQ Mnt
                /*SwissParams p = IQMountain(pos);
                return IQMountains(p) * newHeight;*/

                // Base noise
                /*PerlinParams duneParams = PerlinDune(pos + offset);
                return max(newHeight * PositivePerlin(duneParams), 0);*/

                // Base bumps
                //PerlinParams p = PerlinBump(pos);
                //return SimplePerlin(p) * newHeight;

                // Rocky terrain
                /*SwissParams p = SwissCell(round(pos + dm * warp));
                return SwissCellNoise(p) * newHeight;*/
            }

            v2g vert(appdata v)
            {
                v2g o;
                o.vertex = v.vertex;
                o.uv = v.uv;

                float vertDist = tileSize / float(res);
                float3 vert = o.vertex;

                float2 pos = o.vertex.xz / tileSize * res;

                float2 worldUv = pos / (float2(1000, 500) * 129);


                float2 posN = pos + float2(0, 1);
                //float2 posS = pos + float2(0, -1);
                //float2 posW = pos + float2(-1, 0);
                float2 posE = pos + float2(1, 0);

                //float3 d;
                //SwissParams p = SwissFolded(pos + offset);
                o.vertex.y = HeightSample(pos);

                float3 vertN = vert + float3(vertDist * 0, 0, vertDist * 1);
                //float3 vertS = vert + float3(vertDist * 0, 0, vertDist * -1);
                //float3 vertW = vert + float3(vertDist * -1, 0, vertDist * 0);
                float3 vertE = vert + float3(vertDist * 1, 0, vertDist * 0);

                //p = SwissFolded(posN + offset);
                vertN.y = HeightSample(posN);

                //p = SwissFolded(posS + offset);
                //vertS.y = HeightSample(posS);

                //p = SwissFolded(posW + offset);
                //vertW.y = HeightSample(posW);

                //p = SwissFolded(posE + offset);
                vertE.y = HeightSample(posE);

                float3 a = vertN - o.vertex;
                float3 b = vertE - o.vertex;

                /*float2 step = vertDist.xx;
                float3 va = normalize(float3(step, o.vertex.y - vertW.y));
                float3 vb = normalize(float3(step, vertN.y - o.vertex.y));*/

                o.normal = normalize(cross(a, b));

                return o;
            }

            [maxvertexcount(3)]
            void geom(triangle v2g IN[3], inout TriangleStream<g2f> triStream)
            {
                g2f o;

                for (int i = 0; i < 3; i++)
                {
                    o.vertex = UnityObjectToClipPos(IN[i].vertex);
                    UNITY_TRANSFER_FOG(o,o.vertex);
                    o.uv = TRANSFORM_TEX(IN[i].uv, _MainTex);
                    o.normal = IN[i].normal;
                    triStream.Append(o);
                }

                triStream.RestartStrip();
            }

            float4 frag(g2f i) : SV_Target
            {
                // sample the texture
                float4 col = tex2D(_MainTex, i.uv);

                float nl = max(0, dot(i.normal, _WorldSpaceLightPos0.xyz));
                //float l = nl * _LightColor0;

                float3 grass = float3(0.5, 1, 0.3);
                float3 rock = float3(0.5, 0.5, 0.5);
                float d = dot(i.normal, float3(0, 1, 0));
                d = saturate(d);
                d = pow(d, 3);
                col.rgb = lerp(rock, grass, d);

                // apply fog
                //UNITY_APPLY_FOG(i.fogCoord, col);
                return col* nl;
            }
        ENDCG
    }
    }
}