float4 NW_NE_SW_SE[9], N_E_S_W[9];
// Upgrade NOTE: excluded shader from DX11, OpenGL ES 2.0 because it uses unsized arrays
#pragma exclude_renderers d3d11 gles

struct RoadData {
    bool N[9], E[9], S[9], W[9];
    bool NW[9], NE[9], SW[9], SE[9];
};

RoadData GetRoadData() {
    RoadData d;

    for (int i = 0; i < 9; i++) {
        d.N[i] = N_E_S_W[i].x > 0.5;
        d.E[i] = N_E_S_W[i].y > 0.5;
        d.S[i] = N_E_S_W[i].z > 0.5;
        d.W[i] = N_E_S_W[i].w > 0.5;

        d.NW[i] = NW_NE_SW_SE[i].x > 0.5;
        d.NE[i] = NW_NE_SW_SE[i].y > 0.5;
        d.SW[i] = NW_NE_SW_SE[i].z > 0.5;
        d.SE[i] = NW_NE_SW_SE[i].w > 0.5;
    }

    /*d.N = N_E_S_W.x > 0.5;
    d.E = N_E_S_W.y > 0.5;
    d.S = N_E_S_W.z > 0.5;
    d.W = N_E_S_W.w > 0.5;

    d.NW = NW_NE_SW_SE.x > 0.5;
    d.NE = NW_NE_SW_SE.y > 0.5;
    d.SW = NW_NE_SW_SE.z > 0.5;
    d.SE = NW_NE_SW_SE.w > 0.5;*/

    return d;
}

float2 NearestPointInLine(float2 pos, float2 lineStart, float2 lineEnd) {
    float2 l = (lineEnd - lineStart);
    float len = length(l);
    l = normalize(l);

    float2 v = pos - lineStart;
    float2 d = dot(v, l);
    d = clamp(d, 0.0, len);

    return lineStart + l * d;
}

float GetRoadSegmentWeight(float2 pos, float2 roadStart, float2 roadEnd) {
    float2 roadPt = NearestPointInLine(pos, roadStart, roadEnd);
    float2 dir = normalize(pos - roadPt);
    float2 samplePos = roadPt + dir * 15.0;
    samplePos -= (floor(samplePos / terrainSize) * terrainSize);
    samplePos /= terrainSize;

    /*float freq = 0.05;
    float n1 = Perlin2D(samplePos * freq);
    float n2 = Perlin2D(samplePos * freq * 0.5) * 0.7;
    float noise = saturate((n1 + n2) * 0.5 + 0.5);*/
    float noise = tileableNoise.SampleLevel(bm_linear_clamp_sampler, samplePos, 0);

    float maxDist = lerp(20.0, 40.0, noise);
    float thres = lerp(2.5, 3.0, noise);

    float dist = distance(roadPt, pos);
    float w = saturate((dist - thres) / maxDist);

    return 1.0 - w;
}

float GetRoadWeightForMapPixel(float2 pos, int2 mpOffset) {
    int i = (mpOffset.x + 1) + (mpOffset.y + 1) * 3;

    RoadData rd = GetRoadData();
    float f = terrainSize.x;
    float h = f * 0.5;
    float2 tp = terrainPosition + mpOffset * terrainSize * float2(1, -1);

    float2 n = tp + float2(h, f);
    float2 e = tp + float2(f, h);
    float2 s = tp + float2(h, 0);
    float2 w = tp + float2(0, h);

    float2 nw = tp + float2(0, f);
    float2 ne = tp + float2(f, f);
    float2 sw = tp + float2(0, 0);
    float2 se = tp + float2(f, 0);

    float2 c = tp + float2(h, h);

    float weight = 0.0;

    if (rd.N[i]) {
        weight += GetRoadSegmentWeight(pos, n, c);
    }
    if (rd.E[i]) {
        weight += GetRoadSegmentWeight(pos, e, c);
    }
    if (rd.S[i]) {
        weight += GetRoadSegmentWeight(pos, s, c);
    }
    if (rd.W[i]) {
        weight += GetRoadSegmentWeight(pos, w, c);
    }

    if (rd.NW[i]) {
        weight += GetRoadSegmentWeight(pos, nw, c);
    }
    if (rd.NE[i]) {
        weight += GetRoadSegmentWeight(pos, ne, c);
    }
    if (rd.SW[i]) {
        weight += GetRoadSegmentWeight(pos, sw, c);
    }
    if (rd.SE[i]) {
        weight += GetRoadSegmentWeight(pos, se, c);
    }

    return saturate(weight);
}

float GetRoadWeight(float2 pos) {
    float weight = 0;

    for (int x = -1; x <= 1; x++) {
        for (int y = -1; y <= 1; y++) {
            weight += GetRoadWeightForMapPixel(pos, int2(x, y));
        }
    }

    return saturate(weight);
}