using UnityEngine;
using UnityEngine.Rendering.Universal;

[DefaultExecutionOrder(-400)]
public sealed class CaveLighting2DRuntime : MonoBehaviour
{
    private Transform diver;
    private Light2D flashlight;
    private Light2D halo;
    private Material litMaterial;
    private Material diverUnlitMaterial;
    private float findRetryTimer;
    private float baseFlashlightIntensity = 1.35f;
    private bool configured;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (GameObject.Find("CaveLighting2DRuntime") != null)
            return;

        GameObject go = new GameObject("CaveLighting2DRuntime");
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

        CaveVisibilityRuntime oldVisibility = Object.FindFirstObjectByType<CaveVisibilityRuntime>();
        if (oldVisibility != null)
            oldVisibility.enabled = false;

        ConfigureWorldFor2DLighting();
        ConfigureDiverReadability(diverObject);
        BuildFlashlight();
        configured = true;
    }

    private void ConfigureWorldFor2DLighting()
    {
        Camera camera = Camera.main;
        if (camera != null)
            camera.backgroundColor = Color.black;

        Shader litShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
        if (litShader != null)
            litMaterial = new Material(litShader) { name = "Runtime Cave Sprite Lit" };

        GameObject prototypeRoot = GameObject.Find("CaveDivePrototype");
        if (prototypeRoot != null && litMaterial != null)
        {
            SpriteRenderer[] renderers = prototypeRoot.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].sharedMaterial = litMaterial;
        }

        BuildWaterPlane();

        // MVP rule: there is no readable cave silhouette outside the diver's lights.
        // Any global light would reveal the map shape, so turn it fully off.
        Light2D[] sceneLights = Object.FindObjectsByType<Light2D>(FindObjectsSortMode.None);
        for (int i = 0; i < sceneLights.Length; i++)
        {
            Light2D light = sceneLights[i];
            if (light != null && light.lightType == Light2D.LightType.Global)
            {
                light.intensity = 0f;
                light.color = Color.black;
            }
        }
    }

    private void ConfigureDiverReadability(GameObject diverObject)
    {
        // The cave should disappear in darkness, but losing the diver itself makes control
        // needlessly confusing. Render only the diver artwork unlit so the player always knows
        // their position/orientation without revealing any surrounding geometry.
        Shader unlitShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (unlitShader == null)
            unlitShader = Shader.Find("Sprites/Default");

        if (unlitShader == null)
            return;

        diverUnlitMaterial = new Material(unlitShader)
        {
            name = "Runtime Diver Readable Unlit"
        };

        SpriteRenderer[] diverRenderers = diverObject.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < diverRenderers.Length; i++)
            diverRenderers[i].sharedMaterial = diverUnlitMaterial;
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
        water.transform.position = new Vector3(0f, 0f, 0.5f);
        water.transform.localScale = new Vector3(32f, 32f, 1f);

        SpriteRenderer renderer = water.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = new Color(0.045f, 0.18f, 0.25f, 1f);
        renderer.sortingOrder = -50;
        if (litMaterial != null)
            renderer.sharedMaterial = litMaterial;
    }

    private void BuildFlashlight()
    {
        GameObject beamObject = new GameObject("Diver Flashlight 2D");
        beamObject.transform.SetParent(diver, false);
        beamObject.transform.localPosition = new Vector3(0.55f, 0f, -0.2f);

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

        // Light2D's wedge is centred on local +Y; the diver faces local +X.
        beamObject.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);

        // Keep only a tiny local glow. The diver itself is now readable via an unlit
        // material, so this halo no longer needs to illuminate surrounding cave walls.
        GameObject haloObject = new GameObject("Diver Local Halo 2D");
        haloObject.transform.SetParent(diver, false);
        haloObject.transform.localPosition = Vector3.zero;

        halo = haloObject.AddComponent<Light2D>();
        halo.lightType = Light2D.LightType.Point;
        halo.color = new Color(0.42f, 0.62f, 0.68f, 1f);
        halo.intensity = 0.05f;
        halo.pointLightInnerRadius = 0.05f;
        halo.pointLightOuterRadius = 0.55f;
        halo.pointLightInnerAngle = 360f;
        halo.pointLightOuterAngle = 360f;
        halo.falloffIntensity = 0.95f;
        halo.overlapOperation = Light2D.OverlapOperation.Additive;
    }
}
