using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Replaces the prototype breadcrumb-looking guideline with a tauter reel line.
// The line pays out on the inward dive, removes unnecessary slack when there is
// clear line-of-sight, and keeps only the points needed to wrap around cave bends.
[DefaultExecutionOrder(500)]
public sealed class GuidelineRigRuntime : MonoBehaviour
{
    private readonly List<Vector3> points = new List<Vector3>();

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
        // Prototype objects are also generated after scene load.
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

        // R-restart teleports the diver back to the entrance. Treat that as a fresh reel.
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

        // Stop the old every-step breadcrumb system from fighting this renderer.
        if (prototypeTrail != null)
            prototypeTrail.enabled = false;

        if (line != null)
        {
            line.startWidth = 0.04f;
            line.endWidth = 0.04f;
            line.numCornerVertices = 3;
            line.numCapVertices = 3;
        }

        return diver != null && line != null;
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
        // Repeatedly remove the middle point when the line can run directly from
        // the previous support point to the diver without cutting through rock.
        // This leaves supports naturally at bends rather than tracing every swim wobble.
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

            // The generated organic cave colliders and any future solid wall collider
            // count as rock. Legacy disabled colliders are ignored above.
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

        if (frozen)
        {
            line.positionCount = points.Count;
            line.SetPositions(points.ToArray());
            return;
        }

        bool needsLiveEnd = Vector3.Distance(points[points.Count - 1], current) > 0.02f;
        int count = points.Count + (needsLiveEnd ? 1 : 0);
        line.positionCount = count;

        for (int i = 0; i < points.Count; i++)
            line.SetPosition(i, points[i]);

        if (needsLiveEnd)
            line.SetPosition(count - 1, current);
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
