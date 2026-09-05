using UnityEngine;
using UnityEngine.Rendering.Universal;

[DefaultExecutionOrder(-400)]
public sealed class CaveLighting2DRuntime : MonoBehaviour
{
    private Transform diver;
    private Light2D flashlight;
    private Light2D sourceGlow;
    private Material litMaterial;
    private Material diverUnlitMaterial;
    private float findRetryTimer;
    private float baseFlashlightIntensity = 1.35f;
    private bool configured;

    private static readonly Vector2 LampLocalPosition = new Vector2(0.55f, 0f);

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
        ConfigureDiverSilhouette(diverObject);
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

    private void ConfigureDiverSilhouette(GameObject diverObject)
    {
        Shader unlitShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (unlitShader == null)
            unlitShader = Shader.Find("Sprites/Default");
        if (unlitShader == null)
            return;

        diverUnlitMaterial = new Material(unlitShader)
        {
            name = "Runtime Diver Silhouette Unlit"
        };

        SpriteRenderer[] renderers = diverObject.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            renderer.sharedMaterial = diverUnlitMaterial;

            Vector2 local = renderer.transform.localPosition;
            float distance = Vector2.Distance(local, LampLocalPosition);
            float nearFactor = 1f - Mathf.Clamp01(Mathf.InverseLerp(0.08f, 1.45f, distance));
            float brightness = Mathf.Lerp(0.18f, 0.66f, nearFactor);
            Color baseColor = renderer.color;
            renderer.color = new Color(
                baseColor.r * brightness,
                baseColor.g * brightness,
                baseColor.b * brightness,
                0.97f);
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
        beamObject.transform.localPosition = new Vector3(LampLocalPosition.x, LampLocalPosition.y, -0.2f);

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
        flashlight.shadowsEnabled = true;
        flashlight.shadowIntensity = 1f;
        flashlight.shadowVolumeIntensity = 1f;
        beamObject.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);

        GameObject sourceGlowObject = new GameObject("Flashlight Source Glow 2D");
        sourceGlowObject.transform.SetParent(diver, false);
        sourceGlowObject.transform.localPosition = new Vector3(LampLocalPosition.x, LampLocalPosition.y, -0.18f);

        sourceGlow = sourceGlowObject.AddComponent<Light2D>();
        sourceGlow.lightType = Light2D.LightType.Point;
        sourceGlow.color = new Color(0.58f, 0.76f, 0.80f, 1f);
        sourceGlow.intensity = 0.09f;
        sourceGlow.pointLightInnerRadius = 0.03f;
        sourceGlow.pointLightOuterRadius = 0.48f;
        sourceGlow.pointLightInnerAngle = 360f;
        sourceGlow.pointLightOuterAngle = 360f;
        sourceGlow.falloffIntensity = 0.98f;
        sourceGlow.overlapOperation = Light2D.OverlapOperation.Additive;
        sourceGlow.shadowsEnabled = true;
        sourceGlow.shadowIntensity = 1f;
        sourceGlow.shadowVolumeIntensity = 1f;
    }
}
