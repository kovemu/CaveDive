using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-500)]
public sealed class CaveDiveHazardsRuntime : MonoBehaviour
{
    private sealed class SiltCloud
    {
        public Vector2 Position;
        public float BornAt;
        public float Strength;
    }

    private readonly List<SiltCloud> clouds = new List<SiltCloud>();

    private GameObject diver;
    private DiverMotor2D motor;
    private Collider2D diverCollider;
    private GuidelineTrail guideline;

    private float findRetryTimer;
    private float spawnCooldown;
    private float exposure;
    private bool guidelineFrozen;

    private static readonly Vector2 PrototypeTarget = new Vector2(20f, -1.8f);
    private static readonly Vector2 PrototypeEntrance = new Vector2(-11.5f, 0f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (GameObject.Find("CaveDiveHazardsRuntime") != null)
            return;

        var runtime = new GameObject("CaveDiveHazardsRuntime");
        runtime.AddComponent<CaveDiveHazardsRuntime>();
    }

    private void Update()
    {
        if (!EnsureReferences())
            return;

        UpdateGuidelineReturnBehaviour();
        UpdateClouds();
        UpdateSiltGeneration();
        exposure = CalculateExposure();
    }

    private bool EnsureReferences()
    {
        if (diver != null && motor != null && diverCollider != null)
            return true;

        findRetryTimer -= Time.deltaTime;
        if (findRetryTimer > 0f)
            return false;

        findRetryTimer = 0.25f;
        diver = GameObject.Find("Diver");
        if (diver == null)
            return false;

        motor = diver.GetComponent<DiverMotor2D>();
        diverCollider = diver.GetComponent<Collider2D>();
        guideline = Object.FindFirstObjectByType<GuidelineTrail>();
        return motor != null && diverCollider != null;
    }

    private void UpdateGuidelineReturnBehaviour()
    {
        if (guideline == null)
            guideline = Object.FindFirstObjectByType<GuidelineTrail>();

        if (guideline == null)
            return;

        Vector2 position = diver.transform.position;

        // The line is paid out only on the inward journey. Once the target is reached,
        // it becomes a fixed reference that the diver must follow back out.
        if (!guidelineFrozen && Vector2.Distance(position, PrototypeTarget) < 1.35f)
        {
            guideline.enabled = false;
            guidelineFrozen = true;
        }

        if (guidelineFrozen && Vector2.Distance(position, PrototypeEntrance) < 0.35f)
        {
            guideline.enabled = true;
            guidelineFrozen = false;
            clouds.Clear();
            exposure = 0f;
        }
    }

    private void UpdateSiltGeneration()
    {
        spawnCooldown -= Time.deltaTime;

        if (!motor.enabled || spawnCooldown > 0f)
            return;

        bool movingHard = motor.InputAmount > 0.55f && motor.Speed > 1.8f;
        if (!movingHard || !IsNearFloor())
            return;

        clouds.Add(new SiltCloud
        {
            Position = (Vector2)diver.transform.position + Vector2.down * 0.35f,
            BornAt = Time.time,
            Strength = Mathf.Lerp(0.55f, 0.9f, Mathf.InverseLerp(1.8f, 4f, motor.Speed))
        });

        spawnCooldown = 0.55f;
    }

    private bool IsNearFloor()
    {
        Vector2 origin = (Vector2)diver.transform.position + Vector2.down * 0.34f;
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, Vector2.down, 1.15f);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i].collider;
            if (hit == null || hit == diverCollider || hit.isTrigger)
                continue;

            return true;
        }

        return false;
    }

    private void UpdateClouds()
    {
        for (int i = clouds.Count - 1; i >= 0; i--)
        {
            if (Time.time - clouds[i].BornAt > 50f)
                clouds.RemoveAt(i);
        }
    }

    private float CalculateExposure()
    {
        if (diver == null)
            return 0f;

        Vector2 position = diver.transform.position;
        float total = 0f;

        for (int i = 0; i < clouds.Count; i++)
        {
            SiltCloud cloud = clouds[i];
            float age = Time.time - cloud.BornAt;

            float radius = Mathf.Min(4.3f, 0.85f + age * 0.45f);
            float distance = Vector2.Distance(position, cloud.Position);
            if (distance >= radius)
                continue;

            float spatial = 1f - distance / radius;
            spatial *= spatial;

            float rise = Mathf.Clamp01(age / 1.25f);
            float fade = age < 38f ? 1f : Mathf.Clamp01((50f - age) / 12f);
            total += spatial * rise * fade * cloud.Strength;
        }

        return Mathf.Clamp(total, 0f, 1.15f);
    }

    private void OnGUI()
    {
        if (exposure <= 0.015f)
            return;

        // The hazard veil must sit behind the HUD, which uses the default GUI depth (0).
        GUI.depth = 12;

        Color previousColor = GUI.color;
        float normalized = Mathf.Clamp01(exposure);

        // Flashlight darkness is handled separately. Silt should feel like suspended sediment,
        // not like the entire monitor has simply faded to black.
        float veilAlpha = Mathf.Lerp(0.03f, 0.72f, Mathf.SmoothStep(0f, 1f, normalized));
        GUI.color = new Color(0.17f, 0.145f, 0.105f, veilAlpha);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);

        int speckCount = Mathf.RoundToInt(Mathf.Lerp(10f, 78f, normalized));
        float t = Time.time;
        GUI.color = new Color(0.70f, 0.64f, 0.48f, Mathf.Lerp(0.05f, 0.30f, normalized));

        for (int i = 0; i < speckCount; i++)
        {
            float seedA = Mathf.Abs(Mathf.Sin(i * 12.9898f) * 43758.5453f);
            float seedB = Mathf.Abs(Mathf.Sin(i * 78.233f + 1.7f) * 19341.731f);
            float x = Mathf.Repeat(seedA + t * (7f + i % 5), Screen.width);
            float y = Mathf.Repeat(seedB + t * (3f + i % 3), Screen.height);
            float size = 1.5f + (i % 4) * 0.9f;
            GUI.DrawTexture(new Rect(x, y, size, size), Texture2D.whiteTexture);
        }

        if (normalized > 0.82f)
        {
            var warning = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.88f, 0.86f, 0.78f, 0.85f) }
            };

            GUI.Label(new Rect(0f, Screen.height * 0.54f, Screen.width, 32f),
                "SILT-OUT  -  FIND YOUR GUIDELINE", warning);
        }

        GUI.color = previousColor;
    }
}

[DefaultExecutionOrder(-450)]
public sealed class CaveVisibilityRuntime : MonoBehaviour
{
    private Transform diver;
    private Texture2D flashlightMask;
    private float findRetryTimer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (GameObject.Find("CaveVisibilityRuntime") != null)
            return;

        var runtime = new GameObject("CaveVisibilityRuntime");
        runtime.AddComponent<CaveVisibilityRuntime>();
    }

    private void Awake()
    {
        flashlightMask = BuildFlashlightMask(256);
    }

    private void Update()
    {
        if (diver != null)
            return;

        findRetryTimer -= Time.deltaTime;
        if (findRetryTimer > 0f)
            return;

        findRetryTimer = 0.25f;
        GameObject diverObject = GameObject.Find("Diver");
        if (diverObject != null)
            diver = diverObject.transform;
    }

    private void OnGUI()
    {
        if (diver == null || flashlightMask == null || Camera.main == null)
            return;

        // Keep the world dark while leaving the prototype HUD legible on top.
        GUI.depth = 20;

        Vector3 screenPoint = Camera.main.WorldToScreenPoint(diver.position);
        Vector2 pivot = new Vector2(screenPoint.x, Screen.height - screenPoint.y);

        float size = Mathf.Max(Screen.width, Screen.height) * 2.35f;
        Rect rect = new Rect(pivot.x - size * 0.5f, pivot.y - size * 0.5f, size, size);

        Matrix4x4 previousMatrix = GUI.matrix;
        Color previousColor = GUI.color;

        // Unity world rotation is counter-clockwise with +Y up; IMGUI has +Y down.
        GUIUtility.RotateAroundPivot(-diver.eulerAngles.z, pivot);

        float flicker = 0.97f + Mathf.Sin(Time.time * 8.7f) * 0.015f + Mathf.Sin(Time.time * 3.1f) * 0.01f;
        GUI.color = new Color(1f, 1f, 1f, flicker);
        GUI.DrawTexture(rect, flashlightMask, ScaleMode.StretchToFill, true);

        GUI.matrix = previousMatrix;
        GUI.color = previousColor;
    }

    private static Texture2D BuildFlashlightMask(int size)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "RuntimeFlashlightMask",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color[] pixels = new Color[size * size];
        float half = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - half) / half;
                float dy = (y - half) / half;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);

                float angle = Mathf.Abs(Mathf.Atan2(dy, dx) * Mathf.Rad2Deg);
                float cone = 1f - Mathf.SmoothStep(26f, 47f, angle);
                float range = 1f - Mathf.SmoothStep(0.50f, 0.97f, distance);
                float beam = Mathf.Clamp01(cone * range);

                // A tiny amount of local visibility prevents the diver itself disappearing
                // whenever the light is pointed into a wall.
                float near = 1f - Mathf.SmoothStep(0.06f, 0.20f, distance);
                float visibility = Mathf.Max(beam, near * 0.88f);

                float alpha = Mathf.Lerp(0.88f, 0.055f, visibility);
                pixels[y * size + x] = new Color(0.015f, 0.025f, 0.03f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }
}
