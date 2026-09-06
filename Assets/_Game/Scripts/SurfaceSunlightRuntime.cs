using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// A single distant sunlight source above the cave.
// Because it is placed far above the map, its rays are nearly parallel across the cave.
// Existing rock ShadowCaster2D geometry blocks the light, so only areas with a clear
// line toward the open surface receive daylight.
[DefaultExecutionOrder(-150)]
public sealed class SurfaceSunlightRuntime : MonoBehaviour
{
    private const float SunHeightAboveMap = 80f;
    private const float SunIntensity = 0.82f;
    private const float SunFalloff = 0.78f;

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
        // Wait until the MVP cave has built its collision/shadow caster geometry.
        yield return null;
        yield return null;
        yield return null;

        BuildDistantSunlight();
    }

    private static void BuildDistantSunlight()
    {
        // Retire both previous sunlight experiments if they exist during domain reload/play.
        GameObject oldSingle = GameObject.Find("Surface Sunlight 2D");
        if (oldSingle != null)
            Object.Destroy(oldSingle);

        GameObject oldBank = GameObject.Find("Surface Sunlight Bank");
        if (oldBank != null)
            Object.Destroy(oldBank);

        if (GameObject.Find("Distant Surface Sunlight 2D") != null)
            return;

        float worldWidth = MvpMapRuntime.WorldWidth;
        float worldHeight = MvpMapRuntime.WorldHeight;

        // Put one source far above the center of the map. At this distance the cone sides
        // are almost parallel, which reads much more like sunlight than repeated spotlights.
        float sunX = 0f;
        float sunY = worldHeight * 0.5f + SunHeightAboveMap;

        // Cover the entire top edge with a small margin. Rock ceilings decide which shafts
        // actually receive light; we do not pre-select openings by position.
        float halfLitWidth = worldWidth * 0.58f;
        float halfAngle = Mathf.Atan2(halfLitWidth, SunHeightAboveMap) * Mathf.Rad2Deg;
        float outerAngle = Mathf.Clamp(halfAngle * 2f + 4f, 28f, 52f);
        float innerAngle = Mathf.Max(outerAngle - 8f, 20f);

        // Reach slightly beyond the bottom of the map so a truly open vertical shaft can
        // receive weak daylight at depth. Normal falloff keeps deep areas much darker.
        float outerRadius = SunHeightAboveMap + worldHeight + 8f;

        GameObject sunlightObject = new GameObject("Distant Surface Sunlight 2D");
        sunlightObject.transform.position = new Vector3(sunX, sunY, -0.25f);
        sunlightObject.transform.rotation = Quaternion.Euler(0f, 0f, 180f);

        Light2D sunlight = sunlightObject.AddComponent<Light2D>();
        sunlight.lightType = Light2D.LightType.Point;
        sunlight.color = new Color(0.66f, 0.84f, 0.94f, 1f);
        sunlight.intensity = SunIntensity;
        sunlight.pointLightInnerRadius = SunHeightAboveMap * 0.72f;
        sunlight.pointLightOuterRadius = outerRadius;
        sunlight.pointLightInnerAngle = innerAngle;
        sunlight.pointLightOuterAngle = outerAngle;
        sunlight.falloffIntensity = SunFalloff;
        sunlight.overlapOperation = Light2D.OverlapOperation.Additive;
        sunlight.shadowsEnabled = true;
        sunlight.shadowIntensity = 1f;
        sunlight.shadowVolumeIntensity = 1f;
    }
}
