#ifndef ELEMENTAL_LUDO_TERRAIN_ANIMATION_INCLUDED
#define ELEMENTAL_LUDO_TERRAIN_ANIMATION_INCLUDED

// Animation data stored in TEXCOORD1:
// x = animation kind, yzw = kind-specific parameters.
#define LUDO_ANIMATION_WATER_RIPPLE 1.0
#define LUDO_ANIMATION_TREE 2.0
#define LUDO_ANIMATION_LIGHTNING 3.0
#define LUDO_ANIMATION_CLOUD 4.0
#define LUDO_ANIMATION_LAVA 5.0

float LudoTerrainHash(float value)
{
    return frac(sin(value * 12.9898 + 78.233) * 43758.5453);
}

void ApplyLudoTerrainAnimation(
    inout float3 positionOS,
    float4 animationData,
    out half visibility,
    out half brightness)
{
    visibility = 1.0h;
    brightness = 1.0h;

    float animationKind = animationData.x;
    float time = _Time.y;

    if (abs(animationKind - LUDO_ANIMATION_WATER_RIPPLE) < 0.25)
    {
        float cycle = frac(time * 0.20 + animationData.y);
        float2 center = animationData.zw;
        float2 fromCenter = positionOS.xy - center;
        float originalRadius = max(length(fromCenter), 0.001);
        float ringThicknessOffset = originalRadius - 0.52;
        float animatedRadius = lerp(0.52, 0.82, cycle)
            + ringThicknessOffset;
        positionOS.xy = center
            + fromCenter * (animatedRadius / originalRadius);
        positionOS.z -= sin(cycle * PI) * 0.0025;
        visibility = (half)(saturate(sin(cycle * PI)) * 0.35);
        brightness = (half)(1.0 + visibility * 0.025);
    }
    else if (abs(animationKind - LUDO_ANIMATION_TREE) < 0.25)
    {
        // The board surface is z=-0.30 and negative Z points upward.
        float height = max(0.0, -0.30 - positionOS.z);
        float swayWeight = saturate(height / 2.25);
        float phase = positionOS.x * 0.73 + positionOS.y * 1.17;
        float gust = sin(time * 1.35 + phase) * 0.72
            + sin(time * 2.35 + phase * 0.61) * 0.28;
        positionOS.x += gust * swayWeight * 0.16;
        positionOS.y += cos(time * 1.10 + phase * 0.83) * swayWeight * 0.055;
    }
    else if (abs(animationKind - LUDO_ANIMATION_LIGHTNING) < 0.25)
    {
        float stormTime = time * 0.48 + sin(time * 0.11) * 0.23;
        float stormStep = floor(stormTime);
        float cycle = frac(stormTime);
        float selectedCloud = floor(
            LudoTerrainHash(stormStep + 12.37) * 5.0);
        float selected = 1.0
            - step(0.45, abs(animationData.y - selectedCloud));
        float stormHasStrike = step(
            0.16,
            LudoTerrainHash(stormStep + 47.91));
        float strikeProgress = saturate(cycle / 0.27);
        float heightAlongBolt = saturate(
            (animationData.z - positionOS.z) * animationData.w);
        float active = selected
            * stormHasStrike
            * (1.0 - step(0.70, cycle));
        visibility = (half)(active * step(heightAlongBolt, strikeProgress));

        float jitter = sin(
            time * 36.0
            + animationData.y * 31.0
            + stormStep * 7.0) * 0.022;
        positionOS.x += jitter * visibility;
        positionOS.y -= jitter * 0.45 * visibility;
        brightness = (half)(1.0 + visibility * 0.30);
    }
    else if (abs(animationKind - LUDO_ANIMATION_CLOUD) < 0.25)
    {
        float phase = animationData.y * TWO_PI;
        float colorTime = time * animationData.z * TWO_PI;

        // Two slow waves with unrelated periods create a continuous,
        // non-repeating-feeling grey transition for each cloud.
        float primaryTone = sin(colorTime + phase);
        float secondaryTone = sin(
            colorTime * 0.63 + phase * 2.37 + 1.19);
        float tone = primaryTone * 0.68 + secondaryTone * 0.32;
        // The broader range remains readable after the cel-lighting bands,
        // while the sine blend keeps the change soft rather than flickering.
        brightness = (half)(1.0 + tone * 0.26);

        // Only the five large banks receive a non-zero radius. Their two
        // overlapping orbits keep the movement rounded, gentle and irregular.
        float driftTime = time * (0.22 + animationData.y * 0.08);
        float2 broadOrbit = float2(
            cos(driftTime + phase),
            sin(driftTime * 0.87 + phase * 1.31));
        float2 smallWobble = float2(
            sin(driftTime * 1.73 + phase * 2.11),
            cos(driftTime * 1.41 - phase * 1.67));
        positionOS.xy += animationData.w
            * (broadOrbit * 0.76 + smallWobble * 0.24);
    }
    else if (abs(animationKind - LUDO_ANIMATION_LAVA) < 0.25)
    {
        float flow = sin(positionOS.x * 3.1 + positionOS.y * 2.4 - time * 3.0);
        float flicker = sin(time * 5.2 + positionOS.x * 1.7) * 0.5 + 0.5;
        positionOS.z -= flow * 0.004;
        brightness = (half)(0.94 + flicker * 0.16);
    }
}

#endif
