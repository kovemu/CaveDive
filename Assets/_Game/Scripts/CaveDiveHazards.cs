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

        Color previousColor = GUI.color;
        float normalized = Mathf.Clamp01(exposure);

        float veilAlpha = Mathf.Lerp(0.05f, 0.94f, Mathf.SmoothStep(0f, 1f, normalized));
        GUI.color = new Color(0.14f, 0.13f, 0.11f, veilAlpha);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);

        int speckCount = Mathf.RoundToInt(Mathf.Lerp(8f, 54f, normalized));
        float t = Time.time;
        GUI.color = new Color(0.62f, 0.59f, 0.49f, Mathf.Lerp(0.04f, 0.22f, normalized));

        for (int i = 0; i < speckCount; i++)
        {
            float seedA = Mathf.Sin(i * 12.9898f) * 43758.5453f;
            float seedB = Mathf.Sin(i * 78.233f + 1.7f) * 19341.731f;
            float x = Mathf.Repeat(seedA + t * (7f + i % 5), Screen.width);
            float y = Mathf.Repeat(seedB + t * (3f + i % 3), Screen.height);
            float size = 1.5f + (i % 4) * 0.8f;
            GUI.DrawTexture(new Rect(x, y, size, size), Texture2D.whiteTexture);
        }

        if (normalized > 0.72f)
        {
            var warning = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.88f, 0.86f, 0.78f) }
            };

            GUI.Label(new Rect(0f, Screen.height * 0.54f, Screen.width, 32f),
                "SILT-OUT  -  FIND YOUR GUIDELINE", warning);
        }

        GUI.color = previousColor;
    }
}
