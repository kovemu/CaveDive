using System.Collections.Generic;
using UnityEngine;

// MVP silt rule:
// 1) touching rock creates a disturbance and briefly shows a yellow '!'
// 2) the disturbance is initially invisible
// 3) suspended sediment develops over tens of seconds
// 4) returning through that same area later produces a strong silt-out
// 5) the guideline keeps its colour only where the diver's light actually reaches it
[DefaultExecutionOrder(-500)]
public sealed class CaveDiveHazardsRuntime : MonoBehaviour
{
    private sealed class SiltDisturbance
    {
        public Vector2 Position;
        public float BornAt;
        public float Strength;
    }

    private readonly List<SiltDisturbance> disturbances = new List<SiltDisturbance>();

    private GameObject diver;
    private DiverMotor2D motor;
    private Collider2D diverCollider;
    private LineRenderer guidelineRenderer;
    private DiverSiltContactSensor contactSensor;

    private float findRetryTimer;
    private float contactCooldown;
    private float exposure;
    private float warningUntil;
    private Vector2 warningWorldPosition;
    private Vector3 previousDiverPosition;

    private const float FlashlightRange = 9.0f;
    private const float FlashlightHalfAngle = 29f;
    private const float FlashlightSourceOffset = 0.55f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (GameObject.Find("CaveDiveHazardsRuntime") != null)
            return;

        GameObject runtime = new GameObject("CaveDiveHazardsRuntime");
        runtime.AddComponent<CaveDiveHazardsRuntime>();
    }

    private void Update()
    {
        if (!EnsureReferences())
            return;

        contactCooldown -= Time.deltaTime;
        RemoveExpiredDisturbances();
        exposure = CalculateExposure();

        Vector3 current = diver.transform.position;
        if (Vector3.Distance(current, previousDiverPosition) > 3f &&
            Vector2.Distance(current, MvpMissionRuntime.StartPosition) < 1.4f &&
            motor != null && motor.enabled)
        {
            disturbances.Clear();
            exposure = 0f;
        }
        previousDiverPosition = current;
    }

    private bool EnsureReferences()
    {
        if (diver != null && motor != null && diverCollider != null)
            return true;

        findRetryTimer -= Time.deltaTime;
        if (findRetryTimer > 0f)
            return false;

        findRetryTimer = 0.20f;
        diver = GameObject.Find("Diver");
        if (diver == null)
            return false;

        motor = diver.GetComponent<DiverMotor2D>();
        diverCollider = diver.GetComponent<Collider2D>();

        GameObject guidelineObject = GameObject.Find("Guideline");
        if (guidelineObject != null)
            guidelineRenderer = guidelineObject.GetComponent<LineRenderer>();

        contactSensor = diver.GetComponent<DiverSiltContactSensor>();
        if (contactSensor == null)
            contactSensor = diver.AddComponent<DiverSiltContactSensor>();
        contactSensor.Runtime = this;

        previousDiverPosition = diver.transform.position;
        return motor != null && diverCollider != null;
    }

    public void RegisterWallContact(Vector2 contactPoint, float contactSeverity)
    {
        if (diver == null || motor == null || !motor.enabled)
            return;

        if (contactCooldown > 0f)
            return;

        contactCooldown = 0.42f;
        warningWorldPosition = contactPoint;
        warningUntil = Time.time + 0.62f;

        SiltDisturbance nearby = FindNearbyDisturbance(contactPoint, 0.85f);
        if (nearby != null)
        {
            nearby.Strength = Mathf.Min(1.55f, nearby.Strength + Mathf.Lerp(0.08f, 0.20f, contactSeverity));
            nearby.Position = Vector2.Lerp(nearby.Position, contactPoint, 0.18f);
            return;
        }

        disturbances.Add(new SiltDisturbance
        {
            Position = contactPoint,
            BornAt = Time.time,
            Strength = Mathf.Lerp(0.58f, 0.90f, contactSeverity)
        });
    }

    private SiltDisturbance FindNearbyDisturbance(Vector2 point, float radius)
    {
        float sqrRadius = radius * radius;
        for (int i = 0; i < disturbances.Count; i++)
        {
            if ((disturbances[i].Position - point).sqrMagnitude <= sqrRadius)
                return disturbances[i];
        }
        return null;
    }

    private void RemoveExpiredDisturbances()
    {
        for (int i = disturbances.Count - 1; i >= 0; i--)
        {
            if (Time.time - disturbances[i].BornAt > 180f)
                disturbances.RemoveAt(i);
        }
    }

    private float CalculateExposure()
    {
        if (diver == null)
            return 0f;

        Vector2 diverPosition = diver.transform.position;
        float total = 0f;

        for (int i = 0; i < disturbances.Count; i++)
        {
            SiltDisturbance disturbance = disturbances[i];
            float age = Time.time - disturbance.BornAt;
            float maturity = SiltMaturity(age);
            if (maturity <= 0f)
                continue;

            float radius = Mathf.Lerp(0.65f, 3.75f, maturity);
            float distance = Vector2.Distance(diverPosition, disturbance.Position);
            if (distance >= radius)
                continue;

            float spatial = 1f - distance / radius;
            spatial = spatial * spatial * (3f - 2f * spatial);

            float lateFade = age <= 140f ? 1f : Mathf.Clamp01((180f - age) / 40f);
            total += spatial * maturity * disturbance.Strength * lateFade;
        }

        return Mathf.Clamp(total, 0f, 1.15f);
    }

    private static float SiltMaturity(float age)
    {
        if (age < 5f)
            return 0f;
        if (age < 15f)
            return Mathf.Lerp(0f, 0.18f, Mathf.InverseLerp(5f, 15f, age));
        if (age < 30f)
            return Mathf.Lerp(0.18f, 0.58f, Mathf.InverseLerp(15f, 30f, age));
        if (age < 55f)
            return Mathf.Lerp(0.58f, 1f, Mathf.InverseLerp(30f, 55f, age));
        return 1f;
    }

    private void OnGUI()
    {
        DrawSiltHaze();
        DrawContactWarning();
    }

    private void DrawSiltHaze()
    {
        if (exposure <= 0.015f)
            return;

        GUI.depth = 12;

        Color previousColor = GUI.color;
        Matrix4x4 previousMatrix = GUI.matrix;
        float normalized = Mathf.Clamp01(exposure);

        float veilAlpha = Mathf.Lerp(0.02f, 0.62f, Mathf.SmoothStep(0f, 1f, normalized));
        GUI.color = new Color(0.48f, 0.49f, 0.42f, veilAlpha);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);

        int speckCount = Mathf.RoundToInt(Mathf.Lerp(4f, 105f, normalized));
        float t = Time.time;
        GUI.color = new Color(0.74f, 0.72f, 0.61f, Mathf.Lerp(0.025f, 0.28f, normalized));

        for (int i = 0; i < speckCount; i++)
        {
            float seedA = Mathf.Abs(Mathf.Sin(i * 12.9898f + 0.37f) * 43758.5453f);
            float seedB = Mathf.Abs(Mathf.Sin(i * 78.233f + 1.70f) * 19341.731f);
            float x = Mathf.Repeat(seedA + t * (3f + i % 5), Screen.width);
            float y = Mathf.Repeat(seedB - t * (1.2f + i % 3), Screen.height);
            float size = 1.1f + (i % 5) * 0.65f;
            GUI.DrawTexture(new Rect(x, y, size, size), Texture2D.whiteTexture);
        }

        DrawGuidelineThroughSilt(normalized);

        GUI.matrix = previousMatrix;
        GUI.color = previousColor;
    }

    private void DrawGuidelineThroughSilt(float normalized)
    {
        if (guidelineRenderer == null || Camera.main == null || diver == null || normalized < 0.12f)
            return;

        int count = guidelineRenderer.positionCount;
        if (count < 2)
            return;

        Vector3[] positions = new Vector3[count];
        guidelineRenderer.GetPositions(positions);

        Color oldColor = GUI.color;
        float alpha = Mathf.Lerp(0.22f, 0.84f, normalized);
        GUI.color = new Color(0.76f, 0.66f, 0.28f, alpha);

        // The silt readability pass is not an x-ray. Sample every line span and redraw
        // only the pieces that lie inside the current flashlight cone and are not hidden
        // behind solid cave geometry. Turning away from the guideline makes it disappear.
        for (int i = 0; i < count - 1; i++)
        {
            Vector3 aWorld = positions[i];
            Vector3 bWorld = positions[i + 1];
            float length = Vector3.Distance(aWorld, bWorld);
            int subdivisions = Mathf.Clamp(Mathf.CeilToInt(length / 0.35f), 2, 24);

            Vector3 previous = aWorld;
            bool previousVisible = IsInsideFlashlight(previous);

            for (int s = 1; s <= subdivisions; s++)
            {
                float u = s / (float)subdivisions;
                Vector3 current = Vector3.Lerp(aWorld, bWorld, u);
                bool currentVisible = IsInsideFlashlight(current);

                if (previousVisible && currentVisible)
                    DrawWorldGuiLine(previous, current, 1.55f);

                previous = current;
                previousVisible = currentVisible;
            }
        }

        GUI.color = oldColor;
    }

    private bool IsInsideFlashlight(Vector2 worldPoint)
    {
        if (diver == null)
            return false;

        Vector2 forward = diver.transform.right.normalized;
        Vector2 source = (Vector2)diver.transform.position + forward * FlashlightSourceOffset;
        Vector2 toPoint = worldPoint - source;
        float distance = toPoint.magnitude;

        // The tiny source glow around the lamp itself is also real illumination.
        if (distance <= 0.75f)
            return HasLightLineOfSight(source, worldPoint);

        if (distance > FlashlightRange || distance < 0.001f)
            return false;

        float angle = Vector2.Angle(forward, toPoint / distance);
        if (angle > FlashlightHalfAngle)
            return false;

        return HasLightLineOfSight(source, worldPoint);
    }

    private bool HasLightLineOfSight(Vector2 source, Vector2 target)
    {
        RaycastHit2D[] hits = Physics2D.LinecastAll(source, target);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i].collider;
            if (hit == null || hit == diverCollider || hit.isTrigger || !hit.enabled)
                continue;

            return false;
        }

        return true;
    }

    private static void DrawWorldGuiLine(Vector3 aWorld, Vector3 bWorld, float thickness)
    {
        if (Camera.main == null)
            return;

        Vector3 aScreen3 = Camera.main.WorldToScreenPoint(aWorld);
        Vector3 bScreen3 = Camera.main.WorldToScreenPoint(bWorld);
        if (aScreen3.z <= 0f || bScreen3.z <= 0f)
            return;

        Vector2 a = new Vector2(aScreen3.x, Screen.height - aScreen3.y);
        Vector2 b = new Vector2(bScreen3.x, Screen.height - bScreen3.y);
        DrawGuiLine(a, b, thickness);
    }

    private static void DrawGuiLine(Vector2 a, Vector2 b, float thickness)
    {
        Vector2 delta = b - a;
        float length = delta.magnitude;
        if (length < 0.5f)
            return;

        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        Matrix4x4 oldMatrix = GUI.matrix;
        GUIUtility.RotateAroundPivot(angle, a);
        GUI.DrawTexture(new Rect(a.x, a.y - thickness * 0.5f, length, thickness), Texture2D.whiteTexture);
        GUI.matrix = oldMatrix;
    }

    private void DrawContactWarning()
    {
        if (Time.time > warningUntil || Camera.main == null)
            return;

        Vector3 screen = Camera.main.WorldToScreenPoint(warningWorldPosition);
        if (screen.z <= 0f)
            return;

        float remaining = Mathf.Clamp01((warningUntil - Time.time) / 0.62f);
        float pop = 1f + Mathf.Sin((1f - remaining) * Mathf.PI) * 0.22f;
        float size = 34f * pop;
        Rect rect = new Rect(screen.x - size * 0.5f, Screen.height - screen.y - size * 0.65f, size, size);

        GUI.depth = -5;
        GUIStyle warningStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = Mathf.RoundToInt(30f * pop),
            fontStyle = FontStyle.Bold
        };

        Color previous = GUI.color;
        warningStyle.normal.textColor = new Color(0f, 0f, 0f, remaining * 0.82f);
        GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), "!", warningStyle);

        warningStyle.normal.textColor = new Color(1f, 0.82f, 0.12f, remaining);
        GUI.Label(rect, "!", warningStyle);
        GUI.color = previous;
    }
}

internal sealed class DiverSiltContactSensor : MonoBehaviour
{
    public CaveDiveHazardsRuntime Runtime { get; set; }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        Report(collision, 1f);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        float speed = rb == null ? 0f : rb.linearVelocity.magnitude;
        float severity = Mathf.Clamp01(Mathf.InverseLerp(0.25f, 3.5f, speed));
        Report(collision, Mathf.Lerp(0.35f, 1f, severity));
    }

    private void Report(Collision2D collision, float severity)
    {
        if (Runtime == null || collision.contactCount <= 0)
            return;

        Runtime.RegisterWallContact(collision.GetContact(0).point, severity);
    }
}

public sealed class CaveVisibilityRuntime : MonoBehaviour
{
}
