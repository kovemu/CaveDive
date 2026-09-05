using System.Collections.Generic;
using UnityEngine;

// MVP silt rule:
// 1) touching rock creates a disturbance and briefly shows a yellow '!'
// 2) the disturbance is initially invisible
// 3) suspended sediment develops over tens of seconds
// 4) returning through that same area later produces a strong silt-out
// 5) the guideline remains colour-readable through the haze
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
            // Fresh run after R restart.
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

        // Sliding along one wall should build one growing disturbance rather than
        // creating hundreds of overlapping clouds every physics frame.
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

            // Sediment slowly spreads out from the exact place the diver touched.
            float radius = Mathf.Lerp(0.65f, 3.75f, maturity);
            float distance = Vector2.Distance(diverPosition, disturbance.Position);
            if (distance >= radius)
                continue;

            float spatial = 1f - distance / radius;
            spatial = spatial * spatial * (3f - 2f * spatial); // smoothstep

            float lateFade = age <= 140f ? 1f : Mathf.Clamp01((180f - age) / 40f);
            total += spatial * maturity * disturbance.Strength * lateFade;
        }

        return Mathf.Clamp(total, 0f, 1.15f);
    }

    private static float SiltMaturity(float age)
    {
        // Nothing obvious at first: the mistake is made on the inward trip,
        // while the consequence is waiting for the player on the return trip.
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

        // IMGUI is deliberately used only for the murky optical layer. It sits over
        // the 2D lighting, so even a working flashlight becomes hazy in mature silt.
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

        // In a silt-out, the cave disappears first. The guideline remains the one
        // colour reference the diver can still identify through suspended sediment.
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
        float alpha = Mathf.Lerp(0.25f, 0.88f, normalized);
        GUI.color = new Color(0.76f, 0.66f, 0.28f, alpha);

        Vector2 diverPosition = diver.transform.position;
        for (int i = 0; i < count - 1; i++)
        {
            Vector3 aWorld = positions[i];
            Vector3 bWorld = positions[i + 1];
            Vector2 midpoint = ((Vector2)aWorld + (Vector2)bWorld) * 0.5f;

            // Do not turn the line into an x-ray map of the whole cave. Only the nearby
            // part of the cord gets the special silt readability treatment.
            if (Vector2.Distance(midpoint, diverPosition) > 6.8f)
                continue;

            Vector3 aScreen3 = Camera.main.WorldToScreenPoint(aWorld);
            Vector3 bScreen3 = Camera.main.WorldToScreenPoint(bWorld);
            if (aScreen3.z <= 0f || bScreen3.z <= 0f)
                continue;

            Vector2 a = new Vector2(aScreen3.x, Screen.height - aScreen3.y);
            Vector2 b = new Vector2(bScreen3.x, Screen.height - bScreen3.y);
            DrawGuiLine(a, b, 1.6f);
        }

        GUI.color = oldColor;
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

// Lives on the diver so Unity can deliver collision contacts from static cave geometry.
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

// Retained as a type because CaveLighting2DRuntime disables the old visibility
// experiment if it finds one. The actual MVP visibility now comes entirely from
// Universal 2D lights, so this class intentionally does nothing.
public sealed class CaveVisibilityRuntime : MonoBehaviour
{
}
