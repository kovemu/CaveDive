using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Replaces the old single entrance cone with a bank of downward sunlight beams.
// Any part of the cave that is open to the top can receive sunlight; the existing
// rock ShadowCaster2D geometry blocks it wherever a ceiling/overhang is present.
[DefaultExecutionOrder(-150)]
public sealed class SurfaceSunlightRuntime : MonoBehaviour
{
    private const int BeamCount = 19;
    private const float BeamIntensity = 0.22f;
    private const float BeamInnerAngle = 20f;
    private const float BeamOuterAngle = 34f;
    private const float BeamFalloff = 0.82f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (GameObject.Find("SurfaceSunlightRuntime") != null)
            return;

        GameObject runtime = new GameObject("SurfaceSunlightRuntime");
        runtime.AddComponent<SurfaceSunlightRuntime>();
    }

    private IEnumerator Start()
    {
        // Let CaveLighting2DRuntime create its legacy entrance sunlight first,
        // and let MvpMapRuntime finish building the cave shadow casters.
        yield return null;
        yield return null;
        yield return null;

        ReplaceSurfaceSunlight();
    }

    private static void ReplaceSurfaceSunlight()
    {
        GameObject oldSunlight = GameObject.Find("Surface Sunlight 2D");
        if (oldSunlight != null)
        {
            oldSunlight.SetActive(false);
            Object.Destroy(oldSunlight);
        }

        if (GameObject.Find("Surface Sunlight Bank") != null)
            return;

        float worldWidth = MvpMapRuntime.WorldWidth;
        float worldHeight = MvpMapRuntime.WorldHeight;
        float surfaceY = worldHeight * 0.5f + 0.75f;

        // Sunlight is deliberately limited to the upper part of the cave. Deeper water
        // remains dark even when a shaft is geometrically open all the way upward.
        float sunlightReach = Mathf.Min(worldHeight * 0.42f, 20f);

        GameObject root = new GameObject("Surface Sunlight Bank");

        float left = -worldWidth * 0.5f;
        float step = worldWidth / (BeamCount - 1);

        for (int i = 0; i < BeamCount; i++)
        {
            float x = left + step * i;

            GameObject beamObject = new GameObject($"Sun Beam {i + 1:00}");
            beamObject.transform.SetParent(root.transform, false);
            beamObject.transform.position = new Vector3(x, surfaceY, -0.25f);

            // Light2D point cones aim along local +Y. 180 degrees points them straight down,
            // approximating parallel sunlight while keeping each beam narrow enough that rock
            // ceilings block the light rather than letting it bend far around corners.
            beamObject.transform.rotation = Quaternion.Euler(0f, 0f, 180f);

            Light2D sunlight = beamObject.AddComponent<Light2D>();
            sunlight.lightType = Light2D.LightType.Point;
            sunlight.color = new Color(0.68f, 0.86f, 0.94f, 1f);
            sunlight.intensity = BeamIntensity;
            sunlight.pointLightInnerRadius = 0.35f;
            sunlight.pointLightOuterRadius = sunlightReach;
            sunlight.pointLightInnerAngle = BeamInnerAngle;
            sunlight.pointLightOuterAngle = BeamOuterAngle;
            sunlight.falloffIntensity = BeamFalloff;
            sunlight.overlapOperation = Light2D.OverlapOperation.Additive;
            sunlight.shadowsEnabled = true;
            sunlight.shadowIntensity = 1f;
            sunlight.shadowVolumeIntensity = 1f;
        }
    }
}
