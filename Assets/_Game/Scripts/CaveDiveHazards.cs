using System.Collections.Generic;
using UnityEngine;

// MVP silt rule:
// 1) touching rock briefly shows a yellow '!'
// 2) that contact creates a world-space sediment disturbance
// 3) it is almost invisible at first and matures over tens of seconds
// 4) the sediment itself is lit by the diver's flashlight, so only illuminated water looks murky
// 5) the guideline is never redrawn as an x-ray; it is visible only through normal scene lighting
[DefaultExecutionOrder(-500)]
public sealed class CaveDiveHazardsRuntime : MonoBehaviour
{
    private sealed class SiltDisturbance
    {
        public Vector2 Position;
        public float BornAt;
        public float Strength;
        public SiltCloudVisual Visual;
    }

    private readonly List<SiltDisturbance> disturbances = new List<SiltDisturbance>();

    private GameObject diver;
    private DiverMotor2D motor;
    private Collider2D diverCollider;
    private DiverSiltContactSensor contactSensor;

    private float findRetryTimer;
    private float contactCooldown;
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

        Vector3 current = diver.transform.position;
        if (Vector3.Distance(current, previousDiverPosition) > 3f &&
            Vector2.Distance(current, MvpMissionRuntime.StartPosition) < 1.4f &&
            motor != null && motor.enabled)
        {
            ClearDisturbances();
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
            float boost = Mathf.Lerp(0.08f, 0.20f, contactSeverity);
            nearby.Strength = Mathf.Min(1.55f, nearby.Strength + boost);
            nearby.Position = Vector2.Lerp(nearby.Position, contactPoint, 0.18f);
            if (nearby.Visual != null)
                nearby.Visual.Boost(boost);
            return;
        }

        float strength = Mathf.Lerp(0.58f, 0.90f, contactSeverity);
        disturbances.Add(new SiltDisturbance
        {
            Position = contactPoint,
            BornAt = Time.time,
            Strength = strength,
            Visual = SiltCloudVisual.Create(contactPoint, strength)
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
            if (Time.time - disturbances[i].BornAt <= 180f)
                continue;

            if (disturbances[i].Visual != null)
                Destroy(disturbances[i].Visual.gameObject);
            disturbances.RemoveAt(i);
        }
    }

    private void ClearDisturbances()
    {
        for (int i = 0; i < disturbances.Count; i++)
        {
            if (disturbances[i].Visual != null)
                Destroy(disturbances[i].Visual.gameObject);
        }
        disturbances.Clear();
    }

    private void OnGUI()
    {
        DrawContactWarning();
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

// Kept only because the lighting bootstrap checks for the old prototype visibility component.
public sealed class CaveVisibilityRuntime : MonoBehaviour
{
}
