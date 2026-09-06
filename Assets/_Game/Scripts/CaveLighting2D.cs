using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

[DefaultExecutionOrder(-400)]
public sealed class CaveLighting2DRuntime : MonoBehaviour
{
    private Transform diver;
    private Transform flashlightTransform;
    private Light2D flashlight;
    private Light2D sourceGlow;
    private Material litMaterial;
    private Material diverUnlitMaterial;
    private float findRetryTimer;
    private float baseFlashlightIntensity = 1.35f;
    private float currentFlashlightRange = 9.0f;
    private bool configured;

    private const float BaseFlashlightRange = 9.0f;
    private static readonly Vector2 LampLocalPosition = new Vector2(0.55f, 0f);

    public static Vector2 CurrentAimDirection { get; private set; } = Vector2.right;
    public static Vector2 CurrentLampWorldPosition { get; private set; }

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

    private void LateUpdate()
    {
        if (!configured || diver == null || flashlightTransform == null)
            return;

        AimFlashlightAtMouse();
        ApplySiltAttenuation();
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

        // Surface sunlight is built separately by SurfaceSunlightRuntime so the cave uses
        // one distant source instead of repeated spotlight-like cones.
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
        water.transform.localScale = new Vector3(64f, 64f, 1f);

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
        flashlightTransform = beamObject.transform;

        flashlight = beamObject.AddComponent<Light2D>();
        flashlight.lightType = Light2D.LightType.Point;
        flashlight.color = new Color(0.70f, 0.90f, 0.94f, 1f);
        flashlight.intensity = baseFlashlightIntensity;
        flashlight.pointLightInnerRadius = 0.35f;
        flashlight.pointLightOuterRadius = BaseFlashlightRange;
        flashlight.pointLightInnerAngle = 28f;
        flashlight.pointLightOuterAngle = 58f;
        flashlight.falloffIntensity = 0.55f;
        flashlight.overlapOperation = Light2D.OverlapOperation.Additive;
        flashlight.shadowsEnabled = true;
        flashlight.shadowIntensity = 1f;
        flashlight.shadowVolumeIntensity = 1f;

        SetFlashlightDirection(diver.right);

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

    private void AimFlashlightAtMouse()
    {
        Camera camera = Camera.main;
        Mouse mouse = Mouse.current;
        if (camera == null || mouse == null)
            return;

        Vector2 screenPosition = mouse.position.ReadValue();
        Vector3 mouseWorld3 = camera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, 0f));
        Vector2 lampWorldPosition = flashlightTransform.position;
        Vector2 aim = (Vector2)mouseWorld3 - lampWorldPosition;

        if (aim.sqrMagnitude < 0.0001f)
            return;

        SetFlashlightDirection(aim.normalized);
    }

    private void ApplySiltAttenuation()
    {
        if (flashlight == null)
            return;

        Vector2 source = CurrentLampWorldPosition;
        Vector2 direction = CurrentAimDirection.normalized;

        const float step = 0.18f;
        float transmission = 1f;
        float targetRange = BaseFlashlightRange;

        for (float distance = 0.45f; distance <= BaseFlashlightRange; distance += step)
        {
            Vector2 samplePoint = source + direction * distance;
            float density = SiltCloudVisual.GetOpticalDensityAtPoint(samplePoint);
            if (density <= 0.01f)
                continue;

            transmission *= Mathf.Exp(-density * 1.18f * step);

            if (density >= 0.95f)
            {
                targetRange = Mathf.Min(targetRange, distance + 0.35f);
                break;
            }

            if (transmission <= 0.26f)
            {
                targetRange = Mathf.Min(targetRange, distance + 0.25f);
                break;
            }
        }

        // Restore 25% of the range that silt would otherwise remove.
        // The cloud still blocks visibility, but the cutoff is less punishing.
        targetRange = Mathf.Lerp(targetRange, BaseFlashlightRange, 0.25f);

        currentFlashlightRange = Mathf.MoveTowards(
            currentFlashlightRange,
            targetRange,
            Time.deltaTime * (targetRange < currentFlashlightRange ? 18f : 8f));

        flashlight.pointLightOuterRadius = Mathf.Clamp(currentFlashlightRange, 0.9f, BaseFlashlightRange);
        flashlight.pointLightInnerRadius = Mathf.Min(0.35f, flashlight.pointLightOuterRadius * 0.35f);

        float rangeLoss = 1f - Mathf.InverseLerp(0.9f, BaseFlashlightRange, flashlight.pointLightOuterRadius);
        baseFlashlightIntensity = Mathf.Lerp(1.35f, 0.92f, rangeLoss);
    }

    private void SetFlashlightDirection(Vector2 direction)
    {
        if (flashlightTransform == null || direction.sqrMagnitude < 0.0001f)
            return;

        direction.Normalize();
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        flashlightTransform.rotation = Quaternion.Euler(0f, 0f, angle);

        CurrentAimDirection = direction;
        CurrentLampWorldPosition = flashlightTransform.position;
    }
}
