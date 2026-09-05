using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Replaces the prototype breadcrumb-looking guideline with a taut reel line.
// The line pays out on the inward dive, removes unnecessary slack when there is
// clear line-of-sight, and keeps only the points needed to wrap around cave bends.
[DefaultExecutionOrder(500)]
public sealed class GuidelineRigRuntime : MonoBehaviour
{
    private readonly List<Vector3> points = new List<Vector3>();
    private readonly List<Vector3> visualPoints = new List<Vector3>();

    private Transform diver;
    private DiverMotor2D motor;
    private Collider2D diverCollider;
    private GuidelineTrail prototypeTrail;
    private LineRenderer line;

    private bool frozen;
    private bool completedAtEntrance;
    private Vector3 previousDiverPosition;

    private const float SampleSpacing = 0.38f;
    private const float TargetRadius = 1.35f;
    private const float EntranceRadius = 1.0f;
    private const float CordWidth = 0.022f;

    private static readonly Vector2 PrototypeTarget = new Vector2(20f, -1.8f);
    private static readonly Vector2 PrototypeEntrance = new Vector2(-11.5f, 0f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (GameObject.Find("GuidelineRigRuntime") != null)
            return;

        GameObject runtime = new GameObject("GuidelineRigRuntime");
        runtime.AddComponent<GuidelineRigRuntime>();
    }

    private IEnumerator Start()
    {
        yield return null;
        yield return null;
        FindReferences();
        ResetPath();
    }

    private void LateUpdate()
    {
        if (!FindReferences())
            return;

        Vector3 current = diver.position;
        current.z = 0f;

        if (Vector3.Distance(current, previousDiverPosition) > 3f &&
            Vector2.Distance(current, PrototypeEntrance) < EntranceRadius)
        {
            ResetPath();
        }

        if (!frozen)
        {
            PayOutLine(current);

            if (Vector2.Distance(current, PrototypeTarget) < TargetRadius)
            {
                CommitCurrentPoint(current);
                SimplifyTail(current);
                frozen = true;
            }
        }
        else
        {
            if (Vector2.Distance(current, PrototypeEntrance) < EntranceRadius && motor != null && !motor.enabled)
                completedAtEntrance = true;

            if (completedAtEntrance && motor != null && motor.enabled)
                ResetPath();
        }

        DrawLine(current);
        previousDiverPosition = current;
    }

    private bool FindReferences()
    {
        if (diver != null && line != null)
            return true;

        GameObject diverObject = GameObject.Find("Diver");
        GameObject guidelineObject = GameObject.Find("Guideline");
        if (diverObject == null || guidelineObject == null)
            return false;

        diver = diverObject.transform;
        motor = diverObject.GetComponent<DiverMotor2D>();
        diverCollider = diverObject.GetComponent<Collider2D>();
        line = guidelineObject.GetComponent<LineRenderer>();
        prototypeTrail = guidelineObject.GetComponent<GuidelineTrail>();

        if (prototypeTrail != null)
            prototypeTrail.enabled = false;

        ConfigureCordRenderer();
        return diver != null && line != null;
    }

    private void ConfigureCordRenderer()
    {
        if (line == null)
            return;

        // Keep this deliberately simple. A cave guideline should read as a thin cord,
        // not as a textured ribbon or a glowing game trail.
        line.widthMultiplier = 1f;
        line.startWidth = CordWidth;
        line.endWidth = CordWidth;
        line.numCornerVertices = 1;
        line.numCapVertices = 1;
        line.textureMode = LineTextureMode.Stretch;
        line.sortingOrder = 3;

        Color cord = new Color(0.63f, 0.59f, 0.40f, 0.82f);
        line.startColor = cord;
        line.endColor = new Color(0.58f, 0.55f, 0.38f, 0.80f);

        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        if (shader != null)
        {
            Material material = new Material(shader)
            {
                name = "RuntimeGuidelineCordMaterial"
            };
            line.material = material;
        }
    }

    private void PayOutLine(Vector3 current)
    {
        if (points.Count == 0)
        {
            points.Add(new Vector3(PrototypeEntrance.x, PrototypeEntrance.y, 0f));
            return;
        }

        Vector3 last = points[points.Count - 1];
        if (Vector3.Distance(last, current) < SampleSpacing)
            return;

        points.Add(current);
        SimplifyTail(current);
    }

    private void SimplifyTail(Vector3 current)
    {
        bool changed = true;
        int safety = 0;

        while (changed && points.Count >= 3 && safety++ < 32)
        {
            changed = false;
            int aIndex = points.Count - 3;
            int middleIndex = points.Count - 2;

            if (!RockBetween(points[aIndex], current))
            {
                points.RemoveAt(middleIndex);
                changed = true;
            }
        }
    }

    private bool RockBetween(Vector2 from, Vector2 to)
    {
        RaycastHit2D[] hits = Physics2D.LinecastAll(from, to);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i].collider;
            if (hit == null || hit == diverCollider || hit.isTrigger || !hit.enabled)
                continue;
            return true;
        }

        return false;
    }

    private void CommitCurrentPoint(Vector3 current)
    {
        if (points.Count == 0 || Vector3.Distance(points[points.Count - 1], current) > 0.08f)
            points.Add(current);
    }

    private void DrawLine(Vector3 current)
    {
        if (line == null || points.Count == 0)
            return;

        visualPoints.Clear();

        for (int i = 0; i < points.Count - 1; i++)
            AppendCordSpan(points[i], points[i + 1], false);

        if (!frozen)
        {
            Vector3 last = points[points.Count - 1];
            if (Vector3.Distance(last, current) > 0.02f)
                AppendCordSpan(last, current, true);
            else if (visualPoints.Count == 0)
                visualPoints.Add(last);
        }
        else if (visualPoints.Count == 0)
        {
            visualPoints.Add(points[0]);
        }

        line.positionCount = visualPoints.Count;
        if (visualPoints.Count > 0)
            line.SetPositions(visualPoints.ToArray());
    }

    private void AppendCordSpan(Vector3 a, Vector3 b, bool liveEnd)
    {
        float length = Vector3.Distance(a, b);
        int subdivisions = Mathf.Clamp(Mathf.CeilToInt(length / 1.0f), 2, 12);

        // Only a tiny amount of slack. In water the guideline is not perfectly rigid,
        // but it also should not hang like a heavy rope.
        float sag = Mathf.Min(liveEnd ? 0.012f : 0.035f,
            length * (liveEnd ? 0.0025f : 0.006f));

        for (int s = 0; s <= subdivisions; s++)
        {
            if (visualPoints.Count > 0 && s == 0)
                continue;

            float t = s / (float)subdivisions;
            Vector3 p = Vector3.Lerp(a, b, t);
            float arc = 4f * t * (1f - t);
            p.y -= sag * arc;
            p.z = 0f;
            visualPoints.Add(p);
        }
    }

    private void ResetPath()
    {
        if (!FindReferences())
            return;

        frozen = false;
        completedAtEntrance = false;
        points.Clear();
        points.Add(new Vector3(PrototypeEntrance.x, PrototypeEntrance.y, 0f));

        Vector3 current = diver.position;
        current.z = 0f;
        previousDiverPosition = current;
        DrawLine(current);

        if (prototypeTrail != null)
            prototypeTrail.enabled = false;
    }
}
