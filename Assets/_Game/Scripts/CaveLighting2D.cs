using UnityEngine;
using UnityEngine.Rendering.Universal;

[DefaultExecutionOrder(-400)]
public sealed class CaveLighting2DRuntime : MonoBehaviour
{
    private Transform diver;
    private Light2D flashlight;
    private Light2D halo;
    private Material litMaterial;
    private float findRetryTimer;
    private float baseFlashlightIntensity = 1.35f;
    private bool configured;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (GameObject.Find("CaveLighting2DRuntime") != null)
            return;

        var go = new GameObject("CaveLighting2DRuntime");
        go.AddComponent<CaveLighting2DRuntime>();
    }

    private void Update()
    {
        if (!configured)
        {
            findRetryTimer -= Time.deltaTime;
            if (findRetryTimer <= 0f)
            {
                findRetryTimer = 0.15f;
                TryConfigure();
            }
            return;
        }

        if (flashlight != null)
        {
            float flicker = 1f
                + Mathf.Sin(Time.time * 7.7f) * 0.018f
                + Mathf.Sin(Time.time * 2.9f) * 0.012f;
            flashlight.intensity = baseFlashlightIntensity * flicker;
        }
    }

    private void TryConfigure()
    {
        GameObject diverObject = GameObject.Find("Diver");
        if (diverObject == null)
            return;

        diver = diverObject.transform;

        // Retire the earlier IMGUI darkness experiment. It did not create a reliable
        // visible beam on every Game view / resolution. Universal 2D lighting does.
        CaveVisibilityRuntime oldVisibility = Object.FindFirstObjectByType<CaveVisibilityRuntime>();
        if (oldVisibility != null)
            oldVisibility.enabled = false;

        ConfigureWorldFor2DLighting();
        BuildFlashlight();
        configured = true;
    }

    private void ConfigureWorldFor2DLighting()
    {
        Camera camera = Camera.main;
        if (camera != null)
            camera.backgroundColor = new Color(0.004f, 0.010f, 0.014f, 1f);

        Shader litShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
        if (litShader != null)
            litMaterial = new Material(litShader) { name = "Runtime Cave Sprite Lit" };

        // Make the runtime prototype sprites react to the actual URP 2D lights.
        GameObject prototypeRoot = GameObject.Find("CaveDivePrototype");
        if (prototypeRoot != null && litMaterial != null)
        {
            SpriteRenderer[] renderers = prototypeRoot.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].sharedMaterial = litMaterial;
        }

        // The camera background cannot receive a 2D light, so add a large lit water plane
        // behind the cave. This is what makes the flashlight cone visible in open water.
        BuildWaterPlane();

        Light2D[] sceneLights = Object.FindObjectsByType<Light2D>(FindObjectsSortMode.None);
        for (int i = 0; i < sceneLights.Length; i++)
        {
            Light2D light = sceneLights[i];
            if (light != null && light.lightType == Light2D.LightType.Global)
            {
                light.intensity = 0.075f;
                light.color = new Color(0.23f, 0.42f, 0.50f, 1f);
            }
        }
    }

    private void BuildWaterPlane()
    {
        if (GameObject.Find("RuntimeWaterPlane") != null)
            return;

        Texture2D pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        pixel.name = "RuntimeWaterPixel";
        pixel.SetPixel(0, 0, Color.white);
        pixel.Apply(false, true);

        Sprite sprite = Sprite.Create(pixel, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        sprite.name = "RuntimeWaterSprite";

        GameObject water = new GameObject("RuntimeWaterPlane");
        water.transform.position = new Vector3(4f, 0f, 0.5f);
        water.transform.localScale = new Vector3(62f, 20f, 1f);

        SpriteRenderer renderer = water.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = new Color(0.055f, 0.22f, 0.30f, 1f);
        renderer.sortingOrder = -50;
        if (litMaterial != null)
            renderer.sharedMaterial = litMaterial;
    }

    private void BuildFlashlight()
    {
        GameObject beamObject = new GameObject("Diver Flashlight 2D");
        beamObject.transform.SetParent(diver, false);
        beamObject.transform.localPosition = new Vector3(0.55f, 0f, -0.2f);

        // A 2D Point light with a restricted angle is the URP 2D 'Spot' light.
        flashlight = beamObject.AddComponent<Light2D>();
        flashlight.lightType = Light2D.LightType.Point;
        flashlight.color = new Color(0.70f, 0.90f, 0.94f, 1f);
        flashlight.intensity = baseFlashlightIntensity;
        flashlight.pointLightInnerRadius = 0.35f;
        flashlight.pointLightOuterRadius = 9.0f;
        flashlight.pointLightInnerAngle = 28f;
        flashlight.pointLightOuterAngle = 58f;
        flashlight.falloffIntensity = 0.55f;
        flashlight.overlapOperation = Light2D.OverlapOperation.Additive;

        // Light2D's wedge is centred on local +Y. The diver artwork faces local +X.
        beamObject.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);

        GameObject haloObject = new GameObject("Diver Local Halo 2D");
        haloObject.transform.SetParent(diver, false);
        haloObject.transform.localPosition = Vector3.zero;

        halo = haloObject.AddComponent<Light2D>();
        halo.lightType = Light2D.LightType.Point;
        halo.color = new Color(0.48f, 0.72f, 0.78f, 1f);
        halo.intensity = 0.32f;
        halo.pointLightInnerRadius = 0.15f;
        halo.pointLightOuterRadius = 1.65f;
        halo.pointLightInnerAngle = 360f;
        halo.pointLightOuterAngle = 360f;
        halo.falloffIntensity = 0.75f;
        halo.overlapOperation = Light2D.OverlapOperation.Additive;
    }
}
